// PLAYWRIGHT_MODULE=/path/to/playwright node TourEd.Tests/Browser/local-openlayers.cjs
const assert = require('node:assert/strict');
const {readFileSync} = require('node:fs');
const {resolve, join} = require('node:path');
const {createServer} = require('node:http');
const {chromium} = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const root = resolve(__dirname, '../../Api/wwwroot');
(async () => {
    const browser = await chromium.launch({executablePath: process.env.CHROMIUM_PATH || '/usr/bin/chromium', headless:true,
        args:['--host-resolver-rules=MAP * ~NOTFOUND, EXCLUDE 127.0.0.1']});
    try {
        for (const base of ['/', '/toured/']) {
            for (const authenticated of [false, true]) {
                let apiRequests = 0;
                const server = createServer((req, res) => {
                    const path = new URL(req.url, 'http://localhost').pathname;
                    if (!path.startsWith(base)) return res.writeHead(404).end();
                    let file = path.slice(base.length);
                    const json = data => { res.setHeader('Content-Type','application/json'); res.end(JSON.stringify(data)); };
                    if (file === 'auth/session') return json({authenticated, email:authenticated ? 'test@example.test':null, expiresAt:new Date(Date.now()+3600000).toISOString()});
                    if (file.startsWith('api/')) {
                        apiRequests++;
                        if (!authenticated) return res.writeHead(401).end();
                        if (file === 'api/providers') return json({overallCount:1,totalPoints:0,visitedPoints:0,stampingProviders:[{slug:'touringen',name:'Touringen',abbreviation:'TH',isEnabled:true,isDataReady:true,totalPoints:0,visitedPoints:0}]});
                        return json({overallCount:0,stampingPoints:[]});
                    }
                    if (!file || file.endsWith('/')) file += 'index.html';
                    try {
                        res.setHeader('Content-Type', file.endsWith('.js')?'text/javascript':file.endsWith('.css')?'text/css':file.endsWith('.webmanifest')?'application/manifest+json':file.endsWith('.html')?'text/html':'application/octet-stream');
                        res.end(readFileSync(join(root,file)));
                    } catch {res.writeHead(404).end();}
                });
                await new Promise(r=>server.listen(0,'127.0.0.1',r));
                const origin = `http://127.0.0.1:${server.address().port}`;
                const context = await browser.newContext();
                try {
                    const page = await context.newPage();
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
                    await context.setOffline(true);
                    await page.reload();
                    await page.waitForFunction(()=>typeof ol.Map==='function' && document.querySelector('.ol-viewport'));
                    if(authenticated) await page.waitForFunction(()=>!document.querySelector('#appShell').inert);
                    assert.deepEqual(externalAssets,[]);
                    assert.deepEqual(errors,[]);
                    console.log(`PASS ${base} ${authenticated?'authenticated':'anonymous'} fresh install and offline reload`);
                } finally {await context.close(); await new Promise(r=>server.close(r));}
            }
        }
    } finally {await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
