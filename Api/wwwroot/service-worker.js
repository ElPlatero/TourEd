(() => {
    "use strict";

    const CACHE_NAME = "toured-shell-v1";

    const CORE_ASSETS = [
        "./",
        "css/toured.css",
        "js/toured.js",
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

    const EXTERNAL_ASSETS = [
        "https://cdn.rawgit.com/openlayers/openlayers.github.io/master/en/v5.3.0/css/ol.css",
        "https://cdn.rawgit.com/openlayers/openlayers.github.io/master/en/v5.3.0/build/ol.js"
    ];
    const CORE_URLS = CORE_ASSETS.map(asset => new URL(asset, self.location).href);
    const CACHEABLE_URLS = new Set([...CORE_URLS, ...EXTERNAL_ASSETS]);

    self.addEventListener("install", event => {
        event.waitUntil(
            (async () => {
                try {
                    const cache = await caches.open(CACHE_NAME);
                    await Promise.all(
                        [...CACHEABLE_URLS].map(async url => {
                            const request = new Request(url, {
                                mode: url.startsWith(self.location.origin) ? "same-origin" : "cors"
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

        // Security boundary: Never cache auth, api, or health endpoints
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

        // Handle exact external OpenLayers CDN assets (cache-first)
        if (EXTERNAL_ASSETS.includes(event.request.url)) {
            event.respondWith(
                (async () => {
                    const cache = await caches.open(CACHE_NAME);
                    const cached = await cache.match(event.request);
                    if (cached) {
                        return cached;
                    }
                    try {
                        const response = await fetch(event.request);
                        if (response.ok || response.type === "opaque") {
                            await cache.put(event.request, response.clone());
                        }
                        return response;
                    } catch (err) {
                        if (cached) {
                            return cached;
                        }
                        throw err;
                    }
                })()
            );
            return;
        }

        // Security boundary: Do not handle foreign origins
        if (url.origin !== self.location.origin) {
            return;
        }

        // Navigation requests: Network first, fallback to cached App Shell
        if (event.request.mode === "navigate") {
            event.respondWith(
                (async () => {
                    const cache = await caches.open(CACHE_NAME);
                    try {
                        const networkResponse = await fetch(event.request);
                        if (networkResponse.ok) {
                            return networkResponse;
                        }
                    } catch {
                        // Network error: use offline fallback
                    }

                    const cachedNav = await cache.match(event.request);
                    if (cachedNav) {
                        return cachedNav;
                    }

                    if (url.pathname.includes("/datenschutz")) {
                        const privacyFallback = await cache.match(new URL("datenschutz/index.html", self.location).href)
                            || await cache.match(new URL("datenschutz/", self.location).href);
                        if (privacyFallback) {
                            return privacyFallback;
                        }
                    }

                    const shellFallback = await cache.match(new URL("./", self.location).href)
                        || await cache.match(new URL("index.html", self.location).href);
                    if (shellFallback) {
                        return shellFallback;
                    }

                    return new Response("Offline", { status: 503, statusText: "Service Unavailable" });
                })()
            );
            return;
        }

        if (!CACHEABLE_URLS.has(event.request.url)) {
            return;
        }

        // Known static App Shell assets: Cache first, fallback to network
        event.respondWith(
            (async () => {
                const cache = await caches.open(CACHE_NAME);
                const cached = await cache.match(event.request);
                if (cached) {
                    return cached;
                }
                const response = await fetch(event.request);
                if (response.ok) {
                    await cache.put(event.request, response.clone());
                }
                return response;
            })()
        );
    });
})();
