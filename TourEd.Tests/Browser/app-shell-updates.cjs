// Real service-worker lifecycle tests; no service-worker or Cache API mocks.
const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs');
const { join, resolve } = require('node:path');
const { createServer } = require('node:http');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const root = resolve(__dirname, '../../Api/wwwroot');
const workerSource = readFileSync(process.env.TOURED_WORKER || join(root, 'service-worker.js'), 'utf8');
const ol = readFileSync(process.env.OPENLAYERS_JS, 'utf8');
let release = 'A', failInstall = false;
const hits = [];
const server = createServer((req, res) => {
    const url = new URL(req.url, 'http://localhost');
    const pathname = url.pathname.replace(/^\/toured(?=\/)/, '');
    hits.push({ path: url.pathname, search: url.search, method: req.method, release });
    res.setHeader('Cache-Control', 'no-store');
    if (pathname === '/auth/session') { res.setHeader('Content-Type', 'application/json'); return res.end('{"authenticated":false}'); }
    if (pathname === '/auth/logout') return res.end('logout');
    if (pathname === '/api/points') { res.writeHead(401); return res.end('unauthorized'); }
    if (pathname === '/health') return res.end('healthy');
    if (pathname === '/signin-google') {
        res.writeHead(302, { Location: url.pathname.startsWith('/toured/') ? '/toured/' : '/' });
        return res.end();
    }
    if (pathname === '/external/ol.js') { res.setHeader('Content-Type', 'text/javascript'); return res.end(ol); }
    if (pathname === '/external/ol.css') { res.setHeader('Content-Type', 'text/css'); return res.end(''); }
    const file = pathname === '/' ? 'index.html' : pathname === '/datenschutz/' ? 'datenschutz/index.html' : pathname.slice(1);
    try {
        if (failInstall && file === 'img/icon-512.png') { res.writeHead(500); return res.end('Injected install failure'); }
        let body = file === 'service-worker.js' ? workerSource : readFileSync(join(root, file));
        if (file.endsWith('.html')) {
            body = body.toString().replace('</head>', `<meta name="test-release" content="${release}"></head>`)
                .replace('</body>', `<div id="release-${release}" hidden></div></body>`);
        }
        if (file === 'js/toured.js') {
            body = `document.getElementById('release-${release}').dataset.script = '${release}'; window.__scriptRelease = '${release}';\n` + body;
        }
        if (file === 'css/toured.css') body += `\n:root { --test-release: ${release}; }`;
        if (file === 'service-worker.js') body = body.toString().replace(/toured-shell-v\d+/, `toured-shell-test-${release}`);
        if (file.endsWith('.html') || file === 'service-worker.js') {
            const origin = `http://127.0.0.1:${server.address().port}`;
            body = body.toString()
                .replaceAll('https://cdn.rawgit.com/openlayers/openlayers.github.io/master/en/v5.3.0/css/ol.css', `${origin}/external/ol.css`)
                .replaceAll('https://cdn.rawgit.com/openlayers/openlayers.github.io/master/en/v5.3.0/build/ol.js', `${origin}/external/ol.js`);
        }
        const type = file.endsWith('.js') ? 'text/javascript' : file.endsWith('.css') ? 'text/css' : file.endsWith('.html') ? 'text/html' : file.endsWith('.svg') ? 'image/svg+xml' : file.endsWith('.webmanifest') ? 'application/manifest+json' : 'application/octet-stream';
        res.setHeader('Content-Type', type);
        // Deliberately leave unversioned assets in HTTP cache. Worker installation
        // must reload them, independently of the previous release's HTTP lifetime.
        if (file === 'js/toured.js' || file === 'css/toured.css') res.setHeader('Cache-Control', 'public, max-age=31536000');
        res.end(body);
    } catch { res.writeHead(404).end('not found'); }
});
async function version(page, expected) {
    await page.waitForFunction(() => window.__scriptRelease || document.readyState === 'complete');
    const actual = await page.evaluate(() => ({
        html: document.querySelector('meta[name=test-release]').content,
        js: window.__scriptRelease,
        css: getComputedStyle(document.documentElement).getPropertyValue('--test-release').trim()
    }));
    assert.deepEqual(actual, { html: expected, js: expected, css: expected });
}
(async () => {
    await new Promise(r => server.listen(0, '127.0.0.1', r));
    const origin = `http://127.0.0.1:${server.address().port}`;
    const browser = await chromium.launch({ executablePath: process.env.CHROMIUM_PATH || '/usr/bin/chromium', headless: true,
        args: ['--host-resolver-rules=MAP tile.openstreetmap.org ~NOTFOUND'] });
    try {
        for (const base of ['/', '/toured/']) {
            release = 'A'; failInstall = false;
            const context = await browser.newContext();
            const page = await context.newPage();
            const errors = [];
            page.on('pageerror', error => errors.push(error.message));
            let navigations = 0;
            page.on('framenavigated', frame => { if (frame === page.mainFrame()) navigations++; });
            await page.goto(origin + base);
            await page.evaluate(() => navigator.serviceWorker.ready);
            await page.waitForFunction(() => navigator.serviceWorker.controller);
            await version(page, 'A');
            assert.equal(navigations, 1, 'First installation must not reload the page');
            assert.equal(await page.locator('#updatePrompt').isVisible(), false);
            console.log(`PASS ${base} first installation`);

            release = 'B';
            // Existing installations must retain A even when a new document is online.
            await page.reload();
            await version(page, 'A');
            await page.evaluate(async () => (await navigator.serviceWorker.getRegistration()).update());
            await page.waitForFunction(async () => !!(await navigator.serviceWorker.getRegistration()).waiting);
            await page.locator('#updateReloadButton').waitFor({ state: 'visible' });
            await page.goto(origin + base + 'index.html?provider=touringen&point=1');
            await version(page, 'A');
            await page.locator('#updateReloadButton').waitFor({ state: 'visible' });
            console.log(`PASS ${base} waiting release stays coherent, including index alias and point link`);

            const callbackHits = () => hits.filter(hit => hit.path === base + 'signin-google').length;
            let before = callbackHits();
            await page.goto(origin + base + 'signin-google?state=test-state&code=test-code');
            await version(page, 'A');
            assert.equal(callbackHits(), before + 1, 'Real OAuth callback must reach the server');
            before = callbackHits();
            await page.goto(origin + base + 'signin-google');
            await version(page, 'A');
            assert.equal(callbackHits(), before, 'Stale callback must redirect locally');
            console.log(`PASS ${base} OAuth return and stale callback`);

            await context.setOffline(true);
            await page.goto(origin + base + '?provider=touringen&point=1');
            await version(page, 'A');
            await page.goto(origin + base + 'datenschutz/?test=offline');
            assert.equal(await page.locator('meta[name=test-release]').getAttribute('content'), 'A');
            await page.goto(origin + base);
            await version(page, 'A');
            await context.setOffline(false);
            console.log(`PASS ${base} offline map and privacy navigation`);

            await page.locator('#updateReloadButton').waitFor({ state: 'visible' });
            const beforeActivation = navigations;
            await page.locator('#updateReloadButton').click();
            await page.waitForFunction(() => window.__scriptRelease === 'B');
            await version(page, 'B');
            await page.waitForFunction(() => navigator.serviceWorker.controller && document.readyState === 'complete');
            assert.equal(navigations, beforeActivation + 1, 'Activation must reload exactly once');
            assert.deepEqual(await page.evaluate(() => caches.keys()), ['toured-shell-test-B']);
            console.log(`PASS ${base} confirmed activation loads B together`);

            const apiResults = await page.evaluate(async base => Promise.all([
                fetch(base + 'auth/session').then(r => r.status),
                fetch(base + 'api/points').then(r => r.status),
                fetch(base + 'health').then(r => r.text()),
                fetch(base + 'auth/logout', { method: 'POST' }).then(r => r.text())
            ]), base);
            assert.deepEqual(apiResults, [200, 401, 'healthy', 'logout']);
            const keys = await page.evaluate(async () => (await (await caches.open('toured-shell-test-B')).keys()).map(r => r.url));
            assert.ok(!keys.some(url => /\/auth\/|\/api\/|\/health/.test(url)));
            const unknown = await page.goto(origin + base + 'unknown-page');
            assert.equal(unknown.status(), 404);
            await page.goto(origin + base);
            await version(page, 'B');
            console.log(`PASS ${base} API/auth/health exclusions and unknown route`);

            release = 'C'; failInstall = true;
            await page.evaluate(async () => {
                const registration = await navigator.serviceWorker.getRegistration();
                window.__failedInstallation = new Promise(resolve => registration.addEventListener('updatefound', () => {
                    const worker = registration.installing;
                    worker.addEventListener('statechange', () => { if (worker.state === 'redundant') resolve(); });
                }, { once: true }));
                await registration.update();
                await window.__failedInstallation;
            });
            await page.reload();
            await version(page, 'B');
            assert.ok(!(await page.evaluate(() => caches.keys())).includes('toured-shell-test-C'));
            console.log(`PASS ${base} failed installation preserves B`);

            // Cache eviction must not silently splice release C assets into B.
            const cssPath = base + 'css/toured.css';
            const cssHits = hits.filter(h => h.path === cssPath).length;
            await page.evaluate(async url => (await caches.open('toured-shell-test-B')).delete(url), origin + cssPath);
            const css = await page.evaluate(async url => {
                const response = await fetch(url); return { status: response.status, body: await response.text() };
            }, origin + cssPath);
            assert.equal(css.status, 503);
            assert.equal(hits.filter(h => h.path === cssPath).length, cssHits);
            assert.deepEqual(errors, []);
            console.log(`PASS ${base} missing release asset fails without fetching a different release`);
            await context.close();
        }
    } finally { await browser.close(); server.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; server.close(); });
