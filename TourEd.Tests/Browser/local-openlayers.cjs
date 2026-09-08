// PLAYWRIGHT_MODULE=/path/to/playwright node TourEd.Tests/Browser/local-openlayers.cjs
const assert = require('node:assert/strict');
const {readFileSync} = require('node:fs');
const {resolve, join} = require('node:path');
const {createServer} = require('node:http');
const {chromium} = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const root = resolve(__dirname, '../../Api/wwwroot');
// Both points are visible in the initial viewport and far enough apart to avoid clustering.
const points = [false, true].map((isVisited, index) => ({
    id: index + 1, number: index + 1, name: isVisited ? 'Visited point' : 'Open point',
    providerSlug: 'touringen', provider: {slug:'touringen',name:'Touringen',abbreviation:'TH'},
    series: {slug:'standard',name:'Standard'},
    position: {latitude:50.972,longitude:11.79 + index * 0.05},
    isVisited, visitedOn:null, visitedAt:null, countsTowardProgress:true, tours:[]
}));
async function markersRendered(page) {
    // Inspect actual map pixels, not just loaded images or features in a source.
    await page.waitForFunction(() => {
        const positions = [[], []];
        for (const canvas of document.querySelectorAll('.ol-viewport canvas')) {
            const pixels = canvas.getContext('2d').getImageData(0, 0, canvas.width, canvas.height).data;
            for (let i = 0; i < pixels.length; i += 4) {
                if (pixels[i + 3] !== 255) continue;
                if (pixels[i] === 22 && pixels[i + 1] === 139 && pixels[i + 2] === 210) positions[0].push((i / 4) % canvas.width);
                if (pixels[i] === 18 && pixels[i + 1] === 62 && pixels[i + 2] === 101) positions[1].push((i / 4) % canvas.width);
            }
        }
        return positions.every(xs => xs.length > 50) &&
            Math.min(...positions[1]) - Math.max(...positions[0]) > 44;
    });
}
(async () => {
    const browser = await chromium.launch({executablePath: process.env.CHROMIUM_PATH || '/usr/bin/chromium', headless:true,
        args:['--host-resolver-rules=MAP * ~NOTFOUND, EXCLUDE 127.0.0.1']});
    try {
        for (const base of ['/', '/toured/']) {
            for (const authenticated of [false, true]) {
                let apiRequests = 0;
                const server = createServer((req, res) => {
                    const url = new URL(req.url, 'http://localhost');
                    const path = url.pathname;
                    res.setHeader('Cache-Control', 'no-store');
                    if (!path.startsWith(base)) return res.writeHead(404).end();
                    let file = path.slice(base.length);
                    const json = data => { res.setHeader('Content-Type','application/json'); res.end(JSON.stringify(data)); };
                    if (file === 'auth/session') return json({authenticated, email:authenticated ? 'test@example.test':null, expiresAt:new Date(Date.now()+3600000).toISOString()});
                    if (file.startsWith('api/')) {
                        apiRequests++;
                        if (!authenticated) return res.writeHead(401).end();
                        if (file === 'api/providers') return json({overallCount:1,totalPoints:2,visitedPoints:1,stampingProviders:[{slug:'touringen',name:'Touringen',abbreviation:'TH',isEnabled:true,isDataReady:true,totalPoints:2,visitedPoints:1}]});
                        const selected = points.filter(point => String(point.isVisited) === url.searchParams.get('vis'));
                        return json({overallCount:selected.length,stampingPoints:selected});
                    }
                    if (!file || file.endsWith('/')) file += 'index.html';
                    try {
                        res.setHeader('Content-Type', file.endsWith('.js')?'text/javascript':file.endsWith('.css')?'text/css':file.endsWith('.webmanifest')?'application/manifest+json':file.endsWith('.html')?'text/html':file.endsWith('.svg')?'image/svg+xml':'application/octet-stream');
                        res.end(readFileSync(file === 'js/toured.js' && process.env.TOURED_SCRIPT ? process.env.TOURED_SCRIPT : join(root,file)));
                    } catch {res.writeHead(404).end();}
                });
                await new Promise(r=>server.listen(0,'127.0.0.1',r));
                const origin = `http://127.0.0.1:${server.address().port}`;
                const context = await browser.newContext();
                try {
                    const page = await context.newPage();
                    // Disable and clear the ordinary HTTP cache; Cache Storage remains intact.
                    const cdp = await context.newCDPSession(page);
                    await cdp.send('Network.enable');
                    await cdp.send('Network.setCacheDisabled', {cacheDisabled:true});
                    const errors=[];
                    page.on('pageerror',e=>errors.push(e.message));
                    const externalAssets=[];
                    page.on('request',r=>{if (new URL(r.url()).origin!==origin && ['script','stylesheet'].includes(r.resourceType())) externalAssets.push(r.url());});
                    await page.goto(origin+base);
                    await page.waitForFunction(()=>typeof ol.Map==='function' && document.querySelector('.ol-viewport'));
                    await page.waitForFunction(()=>navigator.serviceWorker.controller);
                    if (authenticated) await page.waitForFunction(()=>!document.querySelector('#appShell').inert);
                    else { await page.locator('#authBarrier').waitFor({state:'visible'}); assert.equal(apiRequests,0); }
                    const urls=await page.evaluate(async()=> (await (await caches.open((await caches.keys()).find(k=>k.startsWith('toured-shell-')))).keys()).map(r=>r.url));
                    for (const file of ['ol.js','ol.css','LICENSE.md']) assert(urls.includes(origin+base+'vendor/openlayers/5.3.0/'+file));
                    assert(urls.every(u=>new URL(u).origin===origin && !new URL(u).pathname.includes('/api/') && !new URL(u).pathname.includes('/auth/')));
                    const pinUrls = ['neutral', 'visited'].map(state => origin+base+'img/pin_icon_'+state+'.svg');
                    if (authenticated) {
                        await markersRendered(page);
                        for (const url of pinUrls) assert(urls.includes(url), 'Pin must belong to the release cache');
                        // Wait for the durable snapshot before disconnecting.
                        await page.waitForFunction(() => new Promise(resolve => {
                            const request = indexedDB.open('toured-db', 1);
                            request.onsuccess = () => {
                                const db = request.result;
                                const read = db.transaction('snapshots').objectStore('snapshots').get('current');
                                read.onsuccess = () => { db.close(); resolve(!!read.result); };
                            };
                        }));
                    }
                    await cdp.send('Network.clearBrowserCache');
                    const pinResponses = [];
                    page.on('response', response => {
                        if (response.request().resourceType() === 'image' && /pin_icon_/.test(response.url())) pinResponses.push(response);
                    });
                    await context.setOffline(true);
                    await page.reload();
                    await page.waitForFunction(()=>typeof ol.Map==='function' && document.querySelector('.ol-viewport'));
                    if(authenticated) {
                        await page.waitForFunction(()=>!document.querySelector('#appShell').inert);
                        await markersRendered(page);
                        assert.deepEqual(pinResponses.map(response => response.url()).sort(), [...pinUrls].sort());
                        for (const response of pinResponses) {
                            assert.equal(response.status(), 200);
                            assert.equal(response.fromServiceWorker(), true, 'Offline pin must come from the service worker');
                            assert.deepEqual(await response.body(), readFileSync(join(root, 'img', new URL(response.url()).pathname.split('/').pop())));
                        }
                    }
                    assert.deepEqual(externalAssets,[]);
                    assert.deepEqual(errors,[]);
                    console.log(`PASS ${base} ${authenticated?'authenticated':'anonymous'} fresh install and offline reload${authenticated ? ' with both rendered single pins from release cache' : ''}`);
                } finally {await context.close(); await new Promise(r=>server.close(r));}
            }
        }
    } finally {await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
