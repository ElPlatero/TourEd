# PWA release-coherence regressions (Issue #81)

This standalone test uses Node.js, Playwright, Chromium and the exact OpenLayers script referenced by the app. It adds no frontend build or production dependency:

```bash
npm install --prefix /tmp/toured-browser-tests playwright
curl -fsSL https://cdn.rawgit.com/openlayers/openlayers.github.io/master/en/v5.3.0/build/ol.js -o /tmp/toured-browser-tests/ol.js
PLAYWRIGHT_MODULE=/tmp/toured-browser-tests/node_modules/playwright OPENLAYERS_JS=/tmp/toured-browser-tests/ol.js node TourEd.Tests/Browser/app-shell-updates.cjs
```

`CHROMIUM_PATH` defaults to `/usr/bin/chromium`. The test starts a temporary HTTP server and real Chromium with real service-worker registration, Cache Storage, HTTP caching and network-offline behavior. It closes both when finished. Tiles are blocked through Chromium's hostname resolver; there is no Playwright request routing that would disable the browser HTTP cache.

The server simulates releases A, B and a failing C. It serves the production HTML, JavaScript, CSS and worker, with these fixture changes only:

- HTML contains a release-specific element; JavaScript accesses that exact element and records its release. Mixing releases throws immediately. CSS supplies an independently checked release marker.
- The worker cache name varies by release.
- The two exact OpenLayers URLs are redirected in the served HTML and worker source to local fixture endpoints. No external account or service is used.
- Unversioned local CSS and JavaScript have deliberately long HTTP-cache lifetimes, testing that worker installation fetches fresh assets.

Each sequence runs at `/` and `/toured/` and covers:

1. First installation claims the page without automatically reloading it or showing an update prompt.
2. Deploying B keeps A's HTML, JavaScript and CSS together during navigation, including `index.html` and a point-link query, while B waits.
3. A callback containing OAuth state reaches the server; a stale callback without state redirects locally to the cached app.
4. Offline navigation loads the active map and privacy page.
5. Clicking the real update button activates B, reloads once, and loads B's HTML, JavaScript and CSS together.
6. Auth, API, health and non-GET requests remain uncached; unknown page routes retain the server's 404 response.
7. Failure to install C leaves B usable and removes C's partial cache.
8. A missing cached local asset returns 503 without fetching a different release's asset from the network.

For the negative control, `TOURED_WORKER=/path/to/old/service-worker.js` substitutes the original worker. It fails after B is deployed because the new HTML is combined with A's cached assets. The test deliberately uses an incompatible DOM change instead of relying only on response-byte comparisons.
