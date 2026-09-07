// Run: PLAYWRIGHT_MODULE=/path/to/playwright OPENLAYERS_JS=/path/to/ol.js node TourEd.Tests/Browser/snapshot-initialization.cjs
// Two pages in one BrowserContext deliberately share cookies and real IndexedDB.
// Separate Playwright BrowserContexts would isolate storage and cannot reproduce this race.
const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs');
const { join, resolve } = require('node:path');
const { createServer } = require('node:http');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const root = resolve(__dirname, '../../Api/wwwroot');
const ol = readFileSync(process.env.OPENLAYERS_JS, 'utf8');
let script = readFileSync(process.env.TOURED_SCRIPT || join(root, 'js/toured.js'), 'utf8');
// Pause immediately before the production write transaction, after any earlier reads.
script = script.replace('const updateStoredSnapshot =', 'const performStoredSnapshotUpdate =')
    .replace('    const clearStoredSnapshot =', `    const updateStoredSnapshot = async mutation => {
        await window.__beforeSnapshotWrite?.();
        return performStoredSnapshotUpdate(mutation);
    };
    const clearStoredSnapshot =`)
    .replace(/(            (?:let initializedSnapshot =|const currentSnapshot = await getStoredSnapshot\(\);))/, '            window.__initializationReady = true;\n$1');
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
    for (let i = 0; i < 150; i++) {
        const value = await probe();
        if (predicate(value)) return value;
        await new Promise(r => setTimeout(r, 20));
    }
    throw new Error('Timed out waiting for expected snapshot state');
}
(async () => {
    await new Promise(r => server.listen(0, '127.0.0.1', r));
    const origin = `http://127.0.0.1:${server.address().port}`;
    const browser = await chromium.launch({ executablePath: process.env.CHROMIUM_PATH || '/usr/bin/chromium', headless: true });
    try {
        for (const scenario of (process.env.SCENARIOS?.split(',') || ['queued', 'completed', 'completed-old', 'logout', 'account-change', 'generation', 'expired'])) {
            const context = await browser.newContext({ serviceWorkers: 'block' });
            const errors = [];
            context.on('page', page => page.on('pageerror', error => errors.push(error.message)));
            let email = 'test@example.invalid';
            let authenticated = true;
            let visited = false;
            let writes = 0;
            const putStarted = deferred(), allowPut = deferred();
            const point = () => ({ id: 1, number: 1, name: 'Testpunkt', providerSlug: 'touringen',
                provider: { slug: 'touringen', name: 'Touringen', abbreviation: 'TH' }, series: { slug: 'standard', name: 'Standard' },
                position: { latitude: 50.7, longitude: 10.7 }, isVisited: visited,
                visitedOn: null, visitedAt: null, countsTowardProgress: true, tours: [] });
            await context.route('**/*', async route => {
                const url = new URL(route.request().url());
                if (url.origin !== origin) return route.fulfill({ body: url.pathname.endsWith('ol.js') ? ol : '', contentType: 'text/javascript' });
                const json = body => route.fulfill({ json: body });
                if (url.pathname === '/auth/session') return json({ authenticated, email, expiresAt: new Date(Date.now() + 3600000).toISOString() });
                if (url.pathname === '/auth/logout') { authenticated = false; return json({}); }
                if (url.pathname === '/api/providers') return json({ overallCount: 1, totalPoints: 1, visitedPoints: +visited,
                    stampingProviders: [{ slug: 'touringen', name: 'Touringen', abbreviation: 'TH', isEnabled: true, isDataReady: true, totalPoints: 1, visitedPoints: +visited }] });
                if (url.pathname.endsWith('/state')) {
                    writes++;
                    putStarted.resolve();
                    await allowPut.promise;
                    visited = true;
                    const desired = route.request().postDataJSON().desired;
                    return json({ ...desired, id: 1, providerSlug: 'touringen' });
                }
                if (url.pathname === '/api/points') return json({ overallCount: (url.searchParams.get('vis') === String(visited)) ? 1 : 0,
                    stampingPoints: (url.searchParams.get('vis') === String(visited)) ? [point()] : [] });
                return route.continue();
            });
            const b = await context.newPage();
            await b.goto(`${origin}/?provider=touringen&point=1`);
            await b.locator('#visitNowButton').waitFor({ state: 'visible' });
            await until(() => readSnapshot(b), s => s?.pendingActions?.length === 0);
            if (scenario === 'completed-old') {
                await b.locator('#visitNowButton').click();
                await putStarted.promise;
            }
            const a = await context.newPage();
            await a.addInitScript(() => {
                // Disable notifications to exercise the durable guard even when delivery is delayed.
                window.BroadcastChannel = class { addEventListener() {} postMessage() {} };
                window.__beforeSnapshotWrite = async () => {
                    if (!window.__initializationReady) return;
                    window.__writePaused = true;
                    await new Promise(resolve => window.__resumeWrite = resolve);
                    window.__beforeSnapshotWrite = null;
                };
            });
            await a.goto(origin);
            await a.waitForFunction(() => window.__writePaused);
            if (scenario === 'queued' || scenario === 'completed') {
                await b.locator('#visitNowButton').click();
                await putStarted.promise;
                await until(() => readSnapshot(b), s => s?.pendingActions?.length === 1);
                if (scenario === 'completed') {
                    allowPut.resolve();
                    await until(() => readSnapshot(b), s => s?.pendingActions?.length === 0);
                }
            } else if (scenario === 'completed-old') {
                allowPut.resolve();
                await until(() => readSnapshot(b), s => s?.pendingActions?.length === 0);
            } else if (scenario === 'logout' || scenario === 'account-change') {
                await b.locator('#accountMenuButton').click();
                await b.locator('#logoutButton').click();
                await until(() => readSnapshot(b), s => s === null);
                await b.locator('#authBarrierLoginButton').waitFor({ state: 'visible' });
                if (scenario === 'account-change') {
                    email = 'other@example.invalid';
                    authenticated = true;
                    await b.reload();
                    await until(() => readSnapshot(b), s => s?.email === email);
                }
            } else if (scenario === 'generation') {
                await a.locator('#accountMenuButton').click();
                await a.locator('#logoutButton').click();
                await until(() => readSnapshot(b), s => s === null);
            } else {
                // Advance the tab's clock beyond the server-confirmed session expiry.
                await a.evaluate(() => { Date.now = () => new Date().getTime() + 7200000; });
            }
            await a.evaluate(() => window.__resumeWrite());
            if (scenario === 'queued') {
                await a.locator('#mapStatus').filter({ hasText: 'geladen' }).waitFor();
                assert.equal((await readSnapshot(b)).pendingActions.length, 1);
                allowPut.resolve();
                await until(() => readSnapshot(b), s => s?.pendingActions?.length === 0);
                assert.equal((await readSnapshot(b)).visitedPoints.stampingPoints[0].id, 1);
                assert.equal(writes, 1);
            } else if (scenario === 'completed' || scenario === 'completed-old') {
                await a.locator('#mapStatus').filter({ hasText: 'geladen' }).waitFor();
                assert.equal((await readSnapshot(b)).pendingActions.length, 0);
                assert.equal(writes, 1);
            } else {
                await a.locator('#authBarrier').waitFor({ state: 'visible' });
                const snapshot = await readSnapshot(b);
                if (scenario === 'account-change') assert.equal(snapshot.email, email);
                if (scenario === 'logout' || scenario === 'generation') assert.equal(snapshot, null);
            }
            assert.deepEqual(errors, []);
            console.log(`PASS ${scenario}`);
            await context.close();
        }
    } finally { await browser.close(); server.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; server.close(); });
