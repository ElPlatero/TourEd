// Two real Chromium tabs share one BrowserContext, cookie state and IndexedDB.
const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs');
const { join, resolve } = require('node:path');
const { createServer } = require('node:http');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const root = resolve(__dirname, '../../Api/wwwroot');

let script = readFileSync(process.env.TOURED_SCRIPT || join(root, 'js/toured.js'), 'utf8');
// Expose production functions solely to await completion and trigger reconnection.
script = script.replace(/    initialize\(\);\r?\n\}\)\(\);/, `    window.__syncTest = {
        app, synchronizePendingActions, refreshFromStoredSnapshot, clearRetryTimer,
        acquireSyncLease, releaseSyncLease, finishPendingAction
    };
    initialize();
})();`);
const server = createServer((req, res) => {
    const path = new URL(req.url, 'http://localhost').pathname;
    const file = path === '/' ? 'index.html' : path.slice(1);
    try {
        const body = file === 'js/toured.js' ? script : readFileSync(join(root, file));
        res.setHeader('Content-Type', file.endsWith('.js') ? 'text/javascript' : file.endsWith('.css') ? 'text/css' : 'text/html');
        res.end(body);
    } catch { res.writeHead(404).end(); }
});
const deferred = () => { let resolve; const promise = new Promise(r => resolve = r); return { promise, resolve }; };
const readSnapshot = page => page.evaluate(() => new Promise(resolve => {
    const request = indexedDB.open('toured-db', 1);
    request.onsuccess = () => {
        const db = request.result;
        const read = db.transaction('snapshots').objectStore('snapshots').get('current');
        read.onsuccess = () => { resolve(read.result ?? null); db.close(); };
    };
}));
async function until(probe, predicate) {
    for (let i = 0; i < 250; i++) {
        const value = await probe();
        if (predicate(value)) return value;
        await new Promise(r => setTimeout(r, 20));
    }
    throw new Error('Timed out waiting for expected state');
}
const finishSync = page => page.evaluate(async () => {
    await window.__syncTest.app.syncPromise;
    window.__syncTest.clearRetryTimer();
});
(async () => {
    await new Promise(r => server.listen(0, '127.0.0.1', r));
    const origin = `http://127.0.0.1:${server.address().port}`;
    const browser = await chromium.launch({ executablePath: process.env.CHROMIUM_PATH || '/usr/bin/chromium', headless: true });
    try {
        for (const scenario of (process.env.SCENARIOS?.split(',') || ['takeover', 'newer-action', 'account-change', 'late-401', 'generation', 'legacy-action', 'duplicate-finish', 'lease-generation', 'expired'])) {
            const context = await browser.newContext({ serviceWorkers: 'block' });
            const errors = [];
            context.on('page', page => page.on('pageerror', error => errors.push(error.message)));
            let email = 'test@example.invalid', authenticated = true;
            let state = { isVisited: false, visitedOn: null, visitedAt: null };
            let writes = 0, effects = 0;
            const firstStarted = deferred(), allowFirst = deferred(), thirdStarted = deferred(), allowThird = deferred();
            const point = id => ({ id, number: id, name: `Testpunkt ${id}`,
                provider: { slug: 'touringen', name: 'Touringen', abbreviation: 'TH' }, series: { slug: 'standard', name: 'Standard' },
                position: { latitude: 50.7 + id / 100, longitude: 10.7 },
                ...(id === 1 ? state : { isVisited: false, visitedOn: null, visitedAt: null }), countsTowardProgress: true, tours: [] });
            await context.route('**/*', async route => {
                const url = new URL(route.request().url());
                if (url.origin !== origin) return route.abort();
                const json = body => route.fulfill({ json: body });
                if (url.pathname === '/auth/session') return json({ authenticated, email, expiresAt: new Date(Date.now() + 3600000).toISOString() });
                if (url.pathname === '/auth/logout') { authenticated = false; return json({}); }
                if (url.pathname === '/api/providers') return json({ overallCount: 1, totalPoints: 3, visitedPoints: +state.isVisited,
                    stampingProviders: [{ slug: 'touringen', name: 'Touringen', abbreviation: 'TH', isEnabled: true, isDataReady: true, totalPoints: 3, visitedPoints: +state.isVisited }] });
                if (url.pathname.endsWith('/state')) {
                    const index = ++writes;
                    const desired = route.request().postDataJSON().desired;
                    if (JSON.stringify(state) !== JSON.stringify(desired)) effects++;
                    state = desired;
                    if (index === 1) { firstStarted.resolve(); await allowFirst.promise; }
                    if (index === 3) { thirdStarted.resolve(); await allowThird.promise; }
                    if (index === 1 && scenario === 'late-401') return route.fulfill({ status: 401 });
                    return json(desired);
                }
                if (url.pathname === '/api/points') {
                    const points = [1, 2, 3].map(point).filter(p => String(p.isVisited) === url.searchParams.get('vis'));
                    return json({ overallCount: points.length, stampingPoints: points });
                }
                return route.continue();
            });
            const a = await context.newPage();
            // Delay notifications to ensure that durable account/lease checks provide protection.
            await a.addInitScript(() => { window.BroadcastChannel = class { addEventListener() {} postMessage() {} }; });
            const b = await context.newPage();
            for (const page of [a, b]) {
                await page.goto(`${origin}/?provider=touringen&point=1`);
                await page.locator('#visitNowButton').waitFor({ state: 'visible' });
                await until(() => readSnapshot(page), s => s?.pendingActions?.length === 0);
            }
            if (scenario === 'legacy-action' || scenario === 'duplicate-finish' || scenario === 'lease-generation') {
                // Stage an old schema-3 queue, retaining real IndexedDB transactions.
                await a.evaluate(() => new Promise(resolve => {
                    const request = indexedDB.open('toured-db', 1);
                    request.onsuccess = () => {
                        const db = request.result, tx = db.transaction('snapshots', 'readwrite'), store = tx.objectStore('snapshots');
                        const read = store.get('current');
                        read.onsuccess = () => {
                            const snapshot = read.result;
                            snapshot.pendingActions = [{ pointId: 1, providerSlug: 'touringen', countsTowardProgress: true,
                                expected: { isVisited: false, visitedOn: null, visitedAt: null }, desired: { isVisited: true, visitedOn: null, visitedAt: null },
                                utcOffsetMinutes: null, createdAt: new Date().toISOString() }];
                            store.put(snapshot);
                        };
                        tx.oncomplete = () => { db.close(); resolve(); };
                    };
                }));
                if (scenario === 'duplicate-finish' || scenario === 'lease-generation') {
                    const result = await a.evaluate(async scenario => {
                        const api = window.__syncTest;
                        let sync = { token: crypto.randomUUID(), email: api.app.sessionEmail.toLowerCase(), generation: api.app.loadGeneration };
                        if (!await api.acquireSyncLease(sync)) throw new Error('Lease acquisition failed');
                        await api.refreshFromStoredSnapshot();
                        const action = api.app.pendingActions.get(1);
                        if (scenario === 'lease-generation') {
                            const previous = sync;
                            sync = { ...sync, token: crypto.randomUUID() };
                            if (!await api.acquireSyncLease(sync)) throw new Error('New lease acquisition failed');
                            if (await api.finishPendingAction(previous, action, action.desired)) throw new Error('Old lease applied a result');
                            const released = await api.releaseSyncLease(previous);
                            if (released.snapshot.syncLease.token !== sync.token) throw new Error('Old run released a newer lease');
                        }
                        const first = await api.finishPendingAction(sync, action, action.desired);
                        const second = await api.finishPendingAction(sync, action, action.desired);
                        await api.releaseSyncLease(sync);
                        return { first, second };
                    }, scenario);
                    assert.deepEqual(result, { first: true, second: false });
                    const snapshot = await readSnapshot(a);
                    assert.equal(snapshot.providers.visitedPoints, 1);
                    assert.equal(snapshot.pendingActions.length, 0);
                    assert.deepEqual(errors, []);
                    console.log(`PASS ${scenario}`);
                    await context.close();
                    continue;
                }
                await a.evaluate(() => { window.__syncTest.synchronizePendingActions(); });
            } else {
                await a.locator('#visitNowButton').click();
            }
            await firstStarted.promise;
            const original = await readSnapshot(a);
            // Advance the clock beyond the real 30-second lease without slowing the suite.
            for (const page of [a, b]) await page.evaluate(() => { const now = Date.now; Date.now = () => now() + 31000; });
            if (scenario === 'account-change' || scenario === 'late-401') {
                b.on('dialog', dialog => dialog.accept());
                await b.locator('#accountMenuButton').click();
                await b.locator('#logoutButton').click();
                await b.locator('#authBarrierLoginButton').waitFor({ state: 'visible' });
                email = 'other@example.invalid'; authenticated = true;
                state = { isVisited: false, visitedOn: null, visitedAt: null };
                await b.goto(`${origin}/?provider=touringen&point=1`);
                await b.locator('#visitNowButton').waitFor({ state: 'visible' });
                await until(() => readSnapshot(b), s => s?.email === email);
            } else if (scenario === 'expired') {
                // No takeover: an expired owner must also leave the action pending.
            } else if (scenario === 'generation') {
                // A newer initialization generation invalidates an in-flight run even
                // if no other tab took the lease. Restore clocks to isolate this guard.
                await a.evaluate(() => { window.__syncTest.app.loadGeneration++; Date.now = () => new Date().getTime(); });
            } else {
                await b.evaluate(() => window.__syncTest.synchronizePendingActions());
                await until(() => readSnapshot(b), s => s?.pendingActions?.length === 0);
                assert.equal(writes, 2);
                assert.equal(effects, 1, 'Backend target state must be effective only once');
                if (scenario === 'newer-action') {
                    await b.evaluate(() => window.__syncTest.refreshFromStoredSnapshot());
                    await b.locator('#deleteVisitButton').click();
                    await b.locator('#confirmDeleteVisitButton').click();
                    await thirdStarted.promise;
                    // Force equal timestamps: the durable UUID must distinguish actions.
                    await b.evaluate(createdAt => new Promise(resolve => {
                        const request = indexedDB.open('toured-db', 1);
                        request.onsuccess = () => {
                            const db = request.result, tx = db.transaction('snapshots', 'readwrite'), store = tx.objectStore('snapshots');
                            const read = store.get('current');
                            read.onsuccess = () => { const snapshot = read.result; snapshot.pendingActions[0].createdAt = createdAt; store.put(snapshot); };
                            tx.oncomplete = () => { db.close(); resolve(); };
                        };
                    }), original.pendingActions[0].createdAt);
                }
            }
            const beforeLateResponse = await readSnapshot(b);
            allowFirst.resolve();
            await finishSync(a);
            const afterLateResponse = await readSnapshot(b);
            if (scenario === 'generation' || scenario === 'expired') delete beforeLateResponse.syncLease;
            assert.deepEqual(afterLateResponse, beforeLateResponse, 'Late response must not mutate personal state or a newer lease');
            if (scenario === 'takeover' || scenario === 'legacy-action') {
                assert.equal(afterLateResponse.providers.visitedPoints, 1);
                assert.equal(afterLateResponse.providers.stampingProviders[0].visitedPoints, 1);
                assert.equal(afterLateResponse.visitedPoints.stampingPoints.length, 1);
            }
            if (scenario === 'newer-action') {
                assert.notEqual(afterLateResponse.pendingActions[0].actionId, original.pendingActions[0].actionId);
                assert.equal(afterLateResponse.pendingActions[0].desired.isVisited, false);
                assert.equal(afterLateResponse.visitedPoints.stampingPoints.length, 0);
                allowThird.resolve();
                await finishSync(b);
                const final = await readSnapshot(b);
                assert.equal(final.providers.visitedPoints, 0);
                assert.equal(final.pendingActions.length, 0);
            }
            assert.deepEqual(errors, []);
            console.log(`PASS ${scenario}`);
            await context.close();
        }
    } finally { await browser.close(); server.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; server.close(); });
