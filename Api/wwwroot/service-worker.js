(() => {
    "use strict";

    const CACHE_NAME = "toured-shell-v18";

    const CORE_ASSETS = [
        "./",
        "css/toured.css",
        "js/toured.js",
        "vendor/openlayers/5.3.0/ol.js",
        "vendor/openlayers/5.3.0/ol.css",
        "vendor/openlayers/5.3.0/LICENSE.md",
        "manifest.webmanifest",
        "img/toured-logo-transparent.svg",
        "img/pin_icon_neutral.svg",
        "img/pin_icon_visited.svg",
        "img/icon-192.png",
        "img/icon-512.png",
        "img/icon-maskable-512.png",
        "img/apple-touch-icon.png",
        "favicon.ico",
        "datenschutz/",
        "datenschutz/index.html"
    ];

    const CORE_URLS = CORE_ASSETS.map(asset => new URL(asset, self.location).href);
    const CACHEABLE_URLS = new Set(CORE_URLS);

    self.addEventListener("install", event => {
        event.waitUntil(
            (async () => {
                try {
                    const cache = await caches.open(CACHE_NAME);
                    await Promise.all(
                        [...CACHEABLE_URLS].map(async url => {
                            const request = new Request(url, {
                                mode: "same-origin",
                                cache: "reload"
                            });
                            const response = await fetch(request);
                            if (!response.ok && response.type !== "opaque") {
                                throw new Error(`Failed to fetch ${url} during SW install: ${response.status}`);
                            }
                            await cache.put(request, response);
                        })
                    );
                } catch (error) {
                    await caches.delete(CACHE_NAME);
                    throw error;
                }
            })()
        );
    });

    self.addEventListener("activate", event => {
        event.waitUntil(
            (async () => {
                const keys = await caches.keys();
                await Promise.all(
                    keys
                        .filter(key => key.startsWith("toured-") && key !== CACHE_NAME)
                        .map(key => caches.delete(key))
                );
                await self.clients.claim();
            })()
        );
    });

    self.addEventListener("message", event => {
        if (event.data && event.data.type === "SKIP_WAITING") {
            event.waitUntil(self.skipWaiting());
        }
    });

    self.addEventListener("fetch", event => {
        if (event.request.method !== "GET") {
            return;
        }

        const url = new URL(event.request.url);

        // Older app versions could leave a standalone window on the callback path.
        // Redirect only a callback without OAuth state; valid callbacks still reach the backend.
        if (url.pathname.endsWith("/signin-google")) {
            if (event.request.mode === "navigate" && !url.searchParams.has("state")) {
                event.respondWith(Response.redirect(new URL("./", url).href, 302));
            }
            return;
        }

        // Security boundary: Never handle auth, api, or health endpoints
        if (url.pathname.includes("/auth/") ||
            url.pathname.includes("/api/") ||
            url.pathname.endsWith("/health") ||
            url.pathname.includes("/health/")) {
            return;
        }

        // Strict requirement: Never cache OpenStreetMap tiles in SW
        if (url.hostname === "tile.openstreetmap.org") {
            return;
        }

        // Security boundary: Do not handle foreign origins
        if (url.origin !== self.location.origin) {
            return;
        }

        // The active worker owns the complete release. Query parameters select
        // application state, never a different HTML version. Unknown routes still
        // reach the server instead of being replaced by the application shell.
        if (event.request.mode === "navigate") {
            const shellUrl = new URL("./", self.location);
            const indexUrl = new URL("index.html", self.location);
            const privacyUrl = new URL("datenschutz/", self.location);
            const privacyIndexUrl = new URL("datenschutz/index.html", self.location);
            let cachedUrl;
            if (url.pathname === shellUrl.pathname || url.pathname === indexUrl.pathname) {
                cachedUrl = shellUrl.href;
            } else if (url.pathname === privacyUrl.pathname || url.pathname === privacyIndexUrl.pathname) {
                cachedUrl = privacyUrl.href;
            } else {
                return;
            }
            event.respondWith(readReleaseAsset(cachedUrl));
            return;
        }

        if (!CACHEABLE_URLS.has(event.request.url)) {
            return;
        }

        // Never fetch an unversioned local asset into an already installed release.
        event.respondWith(readReleaseAsset(event.request));
    });

    async function readReleaseAsset(request) {
        const cache = await caches.open(CACHE_NAME);
        return await cache.match(request)
            || new Response("App-Version nicht vollständig verfügbar. Bitte lade die App neu.", {
                status: 503,
                statusText: "Service Unavailable",
                headers: { "Content-Type": "text/plain; charset=utf-8" }
            });
    }
})();
