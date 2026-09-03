(() => {
    "use strict";

    const elements = {
        accountMenuButton: document.getElementById("accountMenuButton"),
        accountPanel: document.getElementById("accountPanel"),
        appShell: document.getElementById("appShell"),
        authBarrier: document.getElementById("authBarrier"),
        authBarrierDesc: document.getElementById("authBarrierDesc"),
        authBarrierLoading: document.getElementById("authBarrierLoading"),
        authBarrierLoginButton: document.getElementById("authBarrierLoginButton"),
        authBarrierNotice: document.getElementById("authBarrierNotice"),
        cancelDeleteVisitButton: document.getElementById("cancelDeleteVisitButton"),
        cancelVisitButton: document.getElementById("cancelVisitButton"),
        closeDeleteVisitButton: document.getElementById("closeDeleteVisitButton"),
        closeInfoButton: document.getElementById("closeInfoButton"),
        closeProgressPanelButton: document.getElementById("closeProgressPanelButton"),
        closeProviderInfoButton: document.getElementById("closeProviderInfoButton"),
        confirmDeleteVisitButton: document.getElementById("confirmDeleteVisitButton"),
        copyPointLinkButton: document.getElementById("copyPointLinkButton"),
        deleteVisitButton: document.getElementById("deleteVisitButton"),
        deleteVisitDialog: document.getElementById("deleteVisitDialog"),
        deleteVisitMessage: document.getElementById("deleteVisitMessage"),
        editVisitButton: document.getElementById("editVisitButton"),
        existingVisitActions: document.getElementById("existingVisitActions"),
        infoCard: document.getElementById("infoCard"),
        locateButton: document.getElementById("locateButton"),
        loginLink: document.getElementById("loginLink"),
        logoutButton: document.getElementById("logoutButton"),
        map: document.getElementById("map"),
        mapLegend: document.getElementById("mapLegend"),
        mapStatus: document.getElementById("mapStatus"),
        newVisitActions: document.getElementById("newVisitActions"),
        offlineBadge: document.getElementById("offlineBadge"),
        offlineNotice: document.getElementById("offlineNotice"),
        openVisitFormButton: document.getElementById("openVisitFormButton"),
        pointName: document.getElementById("pointName"),
        pointNumber: document.getElementById("pointNumber"),
        pointProvider: document.getElementById("pointProvider"),
        pointShareControls: document.getElementById("pointShareControls"),
        pointShareStatus: document.getElementById("pointShareStatus"),
        pointStatus: document.getElementById("pointStatus"),
        pointTours: document.getElementById("pointTours"),
        pointVisited: document.getElementById("pointVisited"),
        pendingVisitIndicator: document.getElementById("pendingVisitIndicator"),
        progressButton: document.getElementById("progressButton"),
        progressButtonCount: document.getElementById("progressButtonCount"),
        progressButtonFill: document.getElementById("progressButtonFill"),
        progressButtonSrPercent: document.getElementById("progressButtonSrPercent"),
        progressList: document.getElementById("progressList"),
        progressOverview: document.getElementById("progressOverview"),
        progressPanel: document.getElementById("progressPanel"),
        providerInfoDescription: document.getElementById("providerInfoDescription"),
        providerInfoDialog: document.getElementById("providerInfoDialog"),
        providerInfoDisclaimer: document.getElementById("providerInfoDisclaimer"),
        providerInfoName: document.getElementById("providerInfoName"),
        providerInfoWebsite: document.getElementById("providerInfoWebsite"),
        providerDataAttribution: document.getElementById("providerDataAttribution"),
        providerDataDownload: document.getElementById("providerDataDownload"),
        providerDataLicenseLink: document.getElementById("providerDataLicenseLink"),
        providerDataSource: document.getElementById("providerDataSource"),
        providerDataSourceLink: document.getElementById("providerDataSourceLink"),
        providerMenuButton: document.getElementById("providerMenuButton"),
        providerOptions: document.getElementById("providerOptions"),
        providerPanel: document.getElementById("providerPanel"),
        searchMenuButton: document.getElementById("searchMenuButton"),
        searchPanel: document.getElementById("searchPanel"),
        searchResults: document.getElementById("searchResults"),
        searchResultsStatus: document.getElementById("searchResultsStatus"),
        saveVisitButton: document.getElementById("saveVisitButton"),
        selectAllProvidersButton: document.getElementById("selectAllProvidersButton"),
        selectNoProvidersButton: document.getElementById("selectNoProvidersButton"),
        sessionStatus: document.getElementById("sessionStatus"),
        tileErrorBanner: document.getElementById("tileErrorBanner"),
        updatePrompt: document.getElementById("updatePrompt"),
        updateReloadButton: document.getElementById("updateReloadButton"),
        stampingPointSearchInput: document.getElementById("stampingPointSearchInput"),
        userSession: document.getElementById("userSession"),
        visitActionStatus: document.getElementById("visitActionStatus"),
        visitControls: document.getElementById("visitControls"),
        visitedAtInput: document.getElementById("visitedAtInput"),
        visitedOnInput: document.getElementById("visitedOnInput"),
        visitForm: document.getElementById("visitForm"),
        visitFilterButton: document.getElementById("visitFilterButton"),
        visitLoginLink: document.getElementById("visitLoginLink"),
        visitNowButton: document.getElementById("visitNowButton")
    };

    if (typeof ol === "undefined") {
        elements.mapStatus.dataset.state = "error";
        elements.mapStatus.textContent = "Die Kartenbibliothek konnte nicht geladen werden.";
        return;
    }

    const DB_NAME = "toured-db";
    const DB_VERSION = 1;
    const STORE_NAME = "snapshots";
    const SNAPSHOT_KEY = "current";
    const SNAPSHOT_SCHEMA_VERSION = 3;
    const SYNC_LEASE_MILLISECONDS = 30000;
    const RETRY_MAX_MILLISECONDS = 60000;
    const TAB_ID = crypto.randomUUID?.() ?? `${Date.now()}-${Math.random()}`;
    let snapshotOperations = Promise.resolve();

    const readInitialNavigation = () => {
        const params = new URLSearchParams(window.location.search);
        const registration = params.get("registration");
        const providerSlug = params.get("provider")?.trim().toLocaleLowerCase("de-DE") ?? "";
        const pointId = Number(params.get("point"));
        const pointLink = /^[a-z0-9-]+$/.test(providerSlug)
            && Number.isSafeInteger(pointId)
            && pointId > 0
            ? { providerSlug, pointId }
            : null;

        const canonicalParams = new URLSearchParams();
        if (pointLink) {
            canonicalParams.set("provider", pointLink.providerSlug);
            canonicalParams.set("point", String(pointLink.pointId));
        }
        const canonicalSearch = canonicalParams.toString();
        const canonicalUrl = `${window.location.pathname}${canonicalSearch ? `?${canonicalSearch}` : ""}${window.location.hash}`;
        if (`${window.location.pathname}${window.location.search}${window.location.hash}` !== canonicalUrl) {
            window.history.replaceState(null, "", canonicalUrl);
        }

        return { registration, pointLink };
    };

    const initialNavigation = readInitialNavigation();

    const queueSnapshotOperation = operation => {
        const result = snapshotOperations.then(operation, operation);
        snapshotOperations = result.catch(() => {});
        return result;
    };

    const openDatabase = () => new Promise(resolve => {
        if (!("indexedDB" in window)) {
            resolve(null);
            return;
        }
        try {
            const request = indexedDB.open(DB_NAME, DB_VERSION);
            request.onupgradeneeded = () => {
                const db = request.result;
                if (!db.objectStoreNames.contains(STORE_NAME)) {
                    db.createObjectStore(STORE_NAME, { keyPath: "key" });
                }
            };
            request.onsuccess = () => resolve(request.result);
            request.onerror = () => resolve(null);
        } catch {
            resolve(null);
        }
    });

    const readStoredSnapshot = async () => {
        const db = await openDatabase();
        if (!db) return null;
        return new Promise(resolve => {
            try {
                const tx = db.transaction(STORE_NAME, "readonly");
                const store = tx.objectStore(STORE_NAME);
                const request = store.get(SNAPSHOT_KEY);
                request.onsuccess = () => resolve(request.result || null);
                request.onerror = () => resolve(null);
            } catch {
                resolve(null);
            }
        });
    };

    const writeStoredSnapshot = async snapshot => {
        const db = await openDatabase();
        if (!db) return false;
        return new Promise(resolve => {
            try {
                const tx = db.transaction(STORE_NAME, "readwrite");
                const store = tx.objectStore(STORE_NAME);
                store.put({ ...snapshot, key: SNAPSHOT_KEY });
                tx.oncomplete = () => resolve(true);
                tx.onerror = () => resolve(false);
                tx.onabort = () => resolve(false);
            } catch {
                resolve(false);
            }
        });
    };

    const mutateStoredSnapshot = async mutation => {
        const db = await openDatabase();
        if (!db) return { ok: false, snapshot: null };
        return new Promise(resolve => {
            try {
                const tx = db.transaction(STORE_NAME, "readwrite");
                const store = tx.objectStore(STORE_NAME);
                const request = store.get(SNAPSHOT_KEY);
                let nextSnapshot = null;
                request.onsuccess = () => {
                    nextSnapshot = mutation(request.result || null);
                    if (nextSnapshot) {
                        store.put({ ...nextSnapshot, key: SNAPSHOT_KEY });
                    }
                };
                request.onerror = () => tx.abort();
                tx.oncomplete = () => resolve({ ok: true, snapshot: nextSnapshot });
                tx.onerror = () => resolve({ ok: false, snapshot: null });
                tx.onabort = () => resolve({ ok: false, snapshot: null });
            } catch {
                resolve({ ok: false, snapshot: null });
            }
        });
    };

    const deleteStoredSnapshot = async () => {
        const db = await openDatabase();
        if (!db) return;
        return new Promise(resolve => {
            try {
                const tx = db.transaction(STORE_NAME, "readwrite");
                const store = tx.objectStore(STORE_NAME);
                store.delete(SNAPSHOT_KEY);
                tx.oncomplete = () => resolve();
                tx.onerror = () => resolve();
            } catch {
                resolve();
            }
        });
    };

    const normalizeSnapshot = snapshot => {
        if (!snapshot || typeof snapshot !== "object") return snapshot;
        if (snapshot.schemaVersion === 1) {
            return { ...snapshot, schemaVersion: SNAPSHOT_SCHEMA_VERSION, pendingActions: [] };
        }
        if (snapshot.schemaVersion === 2) {
            return {
                ...snapshot,
                schemaVersion: SNAPSHOT_SCHEMA_VERSION,
                pendingActions: Array.isArray(snapshot.pendingActions) ? snapshot.pendingActions : []
            };
        }
        if (snapshot.schemaVersion === SNAPSHOT_SCHEMA_VERSION && !Array.isArray(snapshot.pendingActions)) {
            return { ...snapshot, pendingActions: [] };
        }
        return snapshot;
    };

    const getStoredSnapshot = () => queueSnapshotOperation(async () => {
        const stored = await readStoredSnapshot();
        const normalized = normalizeSnapshot(stored);
        if (stored && stored.schemaVersion !== SNAPSHOT_SCHEMA_VERSION && normalized) {
            await writeStoredSnapshot(normalized);
        }
        return normalized;
    });

    const updateStoredSnapshot = mutation => queueSnapshotOperation(
        () => mutateStoredSnapshot(snapshot => mutation(normalizeSnapshot(snapshot))));

    const clearStoredSnapshot = () => queueSnapshotOperation(deleteStoredSnapshot);

    const isStoredVisitStateValid = state => {
        if (!state || typeof state !== "object" || typeof state.isVisited !== "boolean") return false;
        const visitedOnValid = state.visitedOn === null ||
            (typeof state.visitedOn === "string" && /^\d{4}-\d{2}-\d{2}$/.test(state.visitedOn));
        const visitedAtValid = state.visitedAt === null ||
            (typeof state.visitedAt === "string" && /^\d{2}:\d{2}(:\d{2})?$/.test(state.visitedAt));
        if (!visitedOnValid || !visitedAtValid) return false;
        if (!state.isVisited) return state.visitedOn === null && state.visitedAt === null;
        return state.visitedAt === null || state.visitedOn !== null;
    };

    const isPendingActionValid = action => action &&
        typeof action === "object" &&
        Number.isInteger(action.pointId) && action.pointId > 0 &&
        typeof action.providerSlug === "string" && action.providerSlug.length > 0 &&
        (action.countsTowardProgress === undefined || typeof action.countsTowardProgress === "boolean") &&
        isStoredVisitStateValid(action.expected) &&
        isStoredVisitStateValid(action.desired) &&
        (action.utcOffsetMinutes === null ||
            (Number.isInteger(action.utcOffsetMinutes) &&
                action.utcOffsetMinutes >= -840 && action.utcOffsetMinutes <= 840)) &&
        typeof action.createdAt === "string" && Number.isFinite(Date.parse(action.createdAt));

    const isSnapshotValid = snapshot => {
        if (!snapshot || typeof snapshot !== "object") return false;
        if (snapshot.schemaVersion !== SNAPSHOT_SCHEMA_VERSION) return false;
        if (!snapshot.email || typeof snapshot.email !== "string") return false;
        if (!snapshot.expiresAt) return false;
        const expiresDate = new Date(snapshot.expiresAt);
        if (isNaN(expiresDate.getTime()) || expiresDate <= new Date()) return false;
        if (!snapshot.providers || !Array.isArray(snapshot.providers.stampingProviders)) return false;
        if (!snapshot.unvisitedPoints || !Array.isArray(snapshot.unvisitedPoints.stampingPoints)) return false;
        if (!snapshot.visitedPoints || !Array.isArray(snapshot.visitedPoints.stampingPoints)) return false;
        if (!Array.isArray(snapshot.pendingActions) ||
            !snapshot.pendingActions.every(isPendingActionValid)) return false;
        if (new Set(snapshot.pendingActions.map(action => action.pointId)).size !==
            snapshot.pendingActions.length) return false;
        return true;
    };

    let waitingWorker = null;
    let updateReloadRequested = false;

    const showUpdatePrompt = worker => {
        waitingWorker = worker;
        if (elements.updatePrompt) {
            elements.updatePrompt.hidden = false;
        }
    };

    if (elements.updateReloadButton) {
        elements.updateReloadButton.addEventListener("click", () => {
            if (waitingWorker) {
                updateReloadRequested = true;
                elements.updateReloadButton.disabled = true;
                waitingWorker.postMessage({ type: "SKIP_WAITING" });
            }
        });
    }

    if ("serviceWorker" in navigator) {
        let refreshing = false;
        navigator.serviceWorker.addEventListener("controllerchange", () => {
            if (updateReloadRequested && !refreshing) {
                refreshing = true;
                window.location.reload();
            }
        });

        const swUrl = new URL("service-worker.js", document.baseURI || window.location.href).href;
        navigator.serviceWorker.register(swUrl, { scope: "./" }).then(registration => {
            if (registration.waiting && navigator.serviceWorker.controller) {
                showUpdatePrompt(registration.waiting);
            }

            registration.addEventListener("updatefound", () => {
                const newWorker = registration.installing;
                if (!newWorker) return;

                newWorker.addEventListener("statechange", () => {
                    if (newWorker.state === "installed" && navigator.serviceWorker.controller) {
                        showUpdatePrompt(newWorker);
                    }
                });
            });
        }).catch(() => {});
    }

    const finePointer = window.matchMedia("(hover: hover) and (pointer: fine)");
    const reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)");
    const SearchResultLimit = 30;
    const VisitState = Object.freeze({
        unknown: "unknown",
        open: "open",
        visited: "visited"
    });
    const VisitFilter = Object.freeze({
        all: "all",
        open: "open",
        visited: "visited"
    });
    const VisitFilterOrder = [VisitFilter.all, VisitFilter.open, VisitFilter.visited];
    const syncChannel = "BroadcastChannel" in window
        ? new BroadcastChannel("toured-offline-sync")
        : null;

    const createMarkerStyle = iconSource => new ol.style.Style({
        image: new ol.style.Icon({
            anchor: [0.5, 1],
            src: iconSource,
            scale: 0.32
        })
    });

    const markerStyles = {
        [VisitState.unknown]: createMarkerStyle("img/pin_icon_neutral.svg?v=3"),
        [VisitState.open]: createMarkerStyle("img/pin_icon_neutral.svg?v=3"),
        [VisitState.visited]: createMarkerStyle("img/pin_icon_visited.svg?v=3")
    };
    const clusterStyleCache = new Map();
    const markerSource = new ol.source.Vector();
    const clusterSource = new ol.source.Cluster({
        distance: 44,
        source: markerSource
    });

    const getClusterSize = count => count < 10 ? 40 : count < 50 ? 46 : 52;

    const getClusterVisitState = features => {
        const visitedCount = features.filter(feature => feature.visitState === VisitState.visited).length;
        if (visitedCount === features.length) {
            return VisitState.visited;
        }
        return visitedCount === 0 ? VisitState.open : "mixed";
    };

    const createClusterIconSource = (displayCount, visitState, size) => {
        const center = size / 2;
        const outerRadius = center - 1;
        const innerRadius = center - 8;
        const fill = visitState === "mixed"
            ? "url(#mixed)"
            : visitState === VisitState.visited ? "#123e65" : "#279cdf";
        const mixedDefinition = visitState === "mixed"
            ? [
                '<defs><linearGradient id="mixed" x1="0" y1="1" x2="1" y2="0">',
                '<stop offset="0%" stop-color="#123e65"/>',
                '<stop offset="50%" stop-color="#123e65"/>',
                '<stop offset="50%" stop-color="#279cdf"/>',
                '<stop offset="100%" stop-color="#279cdf"/>',
                "</linearGradient></defs>"
            ].join("")
            : "";
        const check = visitState === VisitState.visited
            ? `<circle cx="${size - 9}" cy="${size - 9}" r="7" fill="#123e65" stroke="#fff" stroke-width="1.5"/><path d="M${size - 13} ${size - 9}l3 3 5-6" fill="none" stroke="#fff" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"/>`
            : "";
        const fontSize = displayCount.length > 2 ? 12 : 14;
        const svg = [
            `<svg xmlns="http://www.w3.org/2000/svg" width="${size}" height="${size}" viewBox="0 0 ${size} ${size}">`,
            mixedDefinition,
            `<circle cx="${center}" cy="${center}" r="${outerRadius}" fill="${fill}" stroke="#123e65" stroke-width="1.5"/>`,
            `<circle cx="${center}" cy="${center}" r="${innerRadius}" fill="#fff"/>`,
            `<text x="${center}" y="${center + 0.5}" fill="#123e65" font-family="system-ui,sans-serif" font-size="${fontSize}" font-weight="700" text-anchor="middle" dominant-baseline="middle">${displayCount}</text>`,
            check,
            "</svg>"
        ].join("");
        return `data:image/svg+xml;charset=utf-8,${encodeURIComponent(svg)}`;
    };

    const getClusterStyle = features => {
        const displayCount = features.length > 99 ? "99+" : String(features.length);
        const visitState = getClusterVisitState(features);
        const size = getClusterSize(features.length);
        const cacheKey = `${visitState}-${displayCount}-${size}`;
        if (!clusterStyleCache.has(cacheKey)) {
            clusterStyleCache.set(cacheKey, new ol.style.Style({
                image: new ol.style.Icon({
                    src: createClusterIconSource(displayCount, visitState, size)
                })
            }));
        }
        return clusterStyleCache.get(cacheKey);
    };

    const markerLayer = new ol.layer.Vector({
        source: clusterSource,
        style: feature => {
            const features = feature.get("features") ?? [];
            return features.length === 1
                ? markerStyles[features[0].visitState]
                : getClusterStyle(features);
        }
    });

    const locationSource = new ol.source.Vector();
    const accuracyFeature = new ol.Feature();
    const positionFeature = new ol.Feature();

    positionFeature.setStyle(
        new ol.style.Style({
            image: new ol.style.Circle({
                radius: 7,
                fill: new ol.style.Fill({
                    color: "rgba(39, 156, 223, 0.85)"
                }),
                stroke: new ol.style.Stroke({
                    color: "#123e65",
                    width: 2.5
                })
            })
        })
    );

    accuracyFeature.setStyle(
        new ol.style.Style({
            fill: new ol.style.Fill({
                color: "rgba(39, 156, 223, 0.12)"
            }),
            stroke: new ol.style.Stroke({
                color: "rgba(18, 62, 101, 0.3)",
                width: 1
            })
        })
    );

    locationSource.addFeatures([accuracyFeature, positionFeature]);

    const userLocationLayer = new ol.layer.Vector({
        source: locationSource
    });

    const app = {
        activeFeature: null,
        authenticated: false,
        isOffline: false,
        sessionEmail: null,
        sessionExpiresAt: null,
        centerOnNextPosition: false,
        infoLocked: false,
        infoPixel: null,
        loadGeneration: 0,
        pendingActions: new Map(),
        pendingPointLink: initialNavigation.pointLink,
        pendingRegistration: initialNavigation.registration,
        providerInfoTrigger: null,
        hasCompleteProviderCatalog: false,
        markerLayer,
        markerSource,
        pointCache: {
            [VisitState.unknown]: [],
            [VisitState.open]: [],
            [VisitState.visited]: []
        },
        providers: [],
        selectedProviderSlugs: new Set(),
        userLocationLayer,
        backOnlineNoticePending: false,
        retryAttempt: 0,
        retryTimer: null,
        syncPromise: null,
        visitFilter: VisitFilter.all
    };

    if (initialNavigation.pointLink) {
        const returnUrl = `${window.location.pathname}${window.location.search}`;
        const loginHref = `auth/login?returnUrl=${encodeURIComponent(returnUrl)}`;
        elements.loginLink.href = loginHref;
        elements.visitLoginLink.href = loginHref;
        elements.authBarrierLoginButton.href = loginHref;
    }

    const broadcastSyncEvent = type => syncChannel?.postMessage({ type, sender: TAB_ID });

    const osmSource = new ol.source.OSM({
        url: "https://tile.openstreetmap.org/{z}/{x}/{y}.png",
        attributions: [
            ol.source.OSM.ATTRIBUTION,
            '<a class="footer-link" href="https://github.com/ElPlatero/TourEd" target="_blank" rel="noopener noreferrer" aria-label="TourEd-Quellcode auf GitHub (AGPL-3.0)" title="TourEd-Quellcode auf GitHub (AGPL-3.0)">&copy; TourEd</a> · <a class="footer-link" href="datenschutz/">Datenschutz</a>'
        ],
        maxZoom: 18
    });

    osmSource.on("tileloaderror", () => {
        if (app.isOffline || !navigator.onLine) {
            if (elements.tileErrorBanner) {
                elements.tileErrorBanner.hidden = false;
            }
        }
    });

    osmSource.on("tileloadend", () => {
        if (elements.tileErrorBanner && !app.isOffline && navigator.onLine) {
            elements.tileErrorBanner.hidden = true;
        }
    });

    app.map = new ol.Map({
        controls: ol.control.defaults({ attribution: false, zoom: false }).extend([new ol.control.Attribution({
            collapsible: false
        })]),
        interactions: ol.interaction.defaults({
            altShiftDragRotate: false,
            pinchRotate: false
        }),
        layers: [
            new ol.layer.Tile({
                source: osmSource
            }),
            userLocationLayer,
            app.markerLayer
        ],
        target: elements.map,
        view: new ol.View({
            center: ol.proj.fromLonLat([11.816394330314203, 50.972084944877366]),
            enableRotation: false,
            maxZoom: 18,
            zoom: 12
        })
    });

    const setMapStatus = (message, state) => {
        elements.mapStatus.textContent = message;
        elements.mapStatus.dataset.state = state ?? "loading";
        elements.mapStatus.hidden = !message;
    };

    const geolocation = new ol.Geolocation({
        tracking: false,
        trackingOptions: {
            enableHighAccuracy: true
        },
        projection: app.map.getView().getProjection()
    });

    geolocation.on("change:position", () => {
        const coordinates = geolocation.getPosition();
        if (coordinates) {
            positionFeature.setGeometry(new ol.geom.Point(coordinates));
            if (app.centerOnNextPosition) {
                app.centerOnNextPosition = false;
                setMapStatus("");
                const view = app.map.getView();
                if (!reducedMotion.matches) {
                    view.animate({
                        center: coordinates,
                        zoom: Math.max(view.getZoom() ?? 0, 14),
                        duration: 500
                    });
                } else {
                    view.setCenter(coordinates);
                    view.setZoom(Math.max(view.getZoom() ?? 0, 14));
                }
            }
        }
    });

    geolocation.on("change:accuracyGeometry", () => {
        const geometry = geolocation.getAccuracyGeometry();
        accuracyFeature.setGeometry(geometry ?? undefined);
    });

    geolocation.on("error", () => {
        geolocation.setTracking(false);
        positionFeature.setGeometry(undefined);
        accuracyFeature.setGeometry(undefined);
        if (app.centerOnNextPosition) {
            app.centerOnNextPosition = false;
            setMapStatus("Standort konnte nicht ermittelt werden.", "error");
            setTimeout(() => {
                if (elements.mapStatus.textContent === "Standort konnte nicht ermittelt werden.") {
                    setMapStatus("");
                }
            }, 4000);
        }
    });

    const showAuthBarrier = () => {
        elements.authBarrier.hidden = false;
        elements.appShell.setAttribute("inert", "");
        elements.appShell.setAttribute("aria-hidden", "true");
        if (elements.authBarrierLoginButton?.hidden !== true) {
            elements.authBarrierLoginButton?.focus({ preventScroll: true });
        }
    };

    const hideAuthBarrier = () => {
        elements.authBarrier.hidden = true;
        elements.appShell.removeAttribute("inert");
        elements.appShell.removeAttribute("aria-hidden");
    };

    let sessionExpiryTimer = null;

    const scheduleSessionExpiry = expiresAt => {
        if (sessionExpiryTimer !== null) {
            window.clearTimeout(sessionExpiryTimer);
            sessionExpiryTimer = null;
        }

        const expiresAtMilliseconds = Date.parse(expiresAt ?? "");
        if (!Number.isFinite(expiresAtMilliseconds)) {
            return;
        }

        const remainingMilliseconds = expiresAtMilliseconds - Date.now();
        if (remainingMilliseconds <= 0) {
            window.queueMicrotask(() => {
                if (app.isOffline) {
                    showOfflineUnavailable("Deine gespeicherte Sitzung ist abgelaufen. Bitte verbinde dich mit dem Internet und melde dich erneut an.");
                } else {
                    initialize();
                }
            });
            return;
        }

        sessionExpiryTimer = window.setTimeout(() => {
            sessionExpiryTimer = null;
            if (app.isOffline) {
                showOfflineUnavailable("Deine gespeicherte Sitzung ist abgelaufen. Bitte verbinde dich mit dem Internet und melde dich erneut an.");
            } else {
                initialize();
            }
        }, remainingMilliseconds);
    };

    const setSession = (session) => {
        const authenticated = session?.authenticated === true;
        app.authenticated = authenticated;
        app.sessionEmail = authenticated ? session.email : null;
        app.sessionExpiresAt = authenticated ? session.expiresAt ?? null : null;
        elements.sessionStatus.textContent = authenticated ? session.email : "Nicht angemeldet";
        elements.loginLink.hidden = authenticated;
        elements.logoutButton.hidden = !authenticated;
        elements.mapLegend.hidden = !authenticated;
        if (elements.progressOverview) {
            elements.progressOverview.hidden = !authenticated;
        }
        scheduleSessionExpiry(app.sessionExpiresAt);
    };

    const setOfflineMode = offline => {
        app.isOffline = offline;
        if (elements.offlineBadge) {
            elements.offlineBadge.hidden = !offline;
        }
        if (!offline) {
            if (elements.tileErrorBanner) {
                elements.tileErrorBanner.hidden = true;
            }
        }
        if (elements.offlineNotice && app.activeFeature && !elements.infoCard.hidden) {
            elements.offlineNotice.hidden = !app.infoLocked || !offline;
        }
    };

    const showOfflineUnavailable = async message => {
        ++app.loadGeneration;
        await clearPersonalData();
        hideInfo(true);
        resetPointCache();
        clearMarkers();
        setSession({ authenticated: false });
        setOfflineMode(true);
        elements.authBarrierLoginButton.hidden = true;
        if (elements.authBarrierLoading) elements.authBarrierLoading.hidden = true;
        elements.authBarrierDesc.hidden = true;
        if (elements.authBarrierNotice) {
            elements.authBarrierNotice.className = "auth-barrier__notice";
            elements.authBarrierNotice.textContent = "";
            const strong = document.createElement("strong");
            strong.textContent = "Offline nicht verfügbar";
            elements.authBarrierNotice.appendChild(strong);
            elements.authBarrierNotice.appendChild(document.createTextNode(message));
            elements.authBarrierNotice.hidden = false;
        }
        showAuthBarrier();
        setMapStatus("Keine Internetverbindung.", "error");
    };

    const closeProgressMenu = (restoreFocus = false) => {
        elements.progressPanel.hidden = true;
        elements.progressButton.setAttribute("aria-expanded", "false");
        updateProgressSummaryAria();
        if (restoreFocus) {
            elements.progressButton.focus({ preventScroll: true });
        }
    };

    const toggleProgressMenu = () => {
        const opening = elements.progressPanel.hidden;
        if (opening) {
            closeSearchMenu();
            closeProviderMenu();
            closeAccountMenu();
        }
        elements.progressPanel.hidden = !opening;
        elements.progressButton.setAttribute("aria-expanded", opening.toString());
        updateProgressSummaryAria();
        if (opening) {
            const firstButton = elements.progressPanel.querySelector("button:not([hidden])");
            firstButton?.focus({ preventScroll: true });
        }
    };

    const closeAccountMenu = (restoreFocus = false) => {
        elements.accountPanel.hidden = true;
        elements.accountMenuButton.setAttribute("aria-expanded", "false");
        elements.accountMenuButton.setAttribute("aria-label", "Kontomenü öffnen");
        if (restoreFocus) {
            elements.accountMenuButton.focus({ preventScroll: true });
        }
    };

    const closeSearchMenu = (restoreFocus = false) => {
        elements.searchPanel.hidden = true;
        elements.searchMenuButton.setAttribute("aria-expanded", "false");
        elements.searchMenuButton.setAttribute("aria-label", "Stempelstellensuche öffnen");
        if (restoreFocus) {
            elements.searchMenuButton.focus({ preventScroll: true });
        }
    };

    const closeProviderMenu = (restoreFocus = false) => {
        elements.providerPanel.hidden = true;
        elements.providerMenuButton.setAttribute("aria-expanded", "false");
        elements.providerMenuButton.setAttribute("aria-label", "Anbieterfilter öffnen");
        if (restoreFocus) {
            elements.providerMenuButton.focus({ preventScroll: true });
        }
    };

    const toggleAccountMenu = () => {
        const opening = elements.accountPanel.hidden;
        if (opening) {
            closeSearchMenu();
            closeProviderMenu();
            closeProgressMenu();
        }
        elements.accountPanel.hidden = !opening;
        elements.accountMenuButton.setAttribute("aria-expanded", opening.toString());
        elements.accountMenuButton.setAttribute(
            "aria-label",
            opening ? "Kontomenü schließen" : "Kontomenü öffnen");
        if (opening) {
            const action = elements.accountPanel.querySelector("a:not([hidden]), button:not([hidden])");
            action?.focus({ preventScroll: true });
        }
    };

    const toggleProviderMenu = () => {
        const opening = elements.providerPanel.hidden;
        if (opening) {
            closeSearchMenu();
            closeAccountMenu();
            closeProgressMenu();
        }
        elements.providerPanel.hidden = !opening;
        elements.providerMenuButton.setAttribute("aria-expanded", opening.toString());
        elements.providerMenuButton.setAttribute(
            "aria-label",
            opening ? "Anbieterfilter schließen" : "Anbieterfilter öffnen");
        if (opening) {
            elements.providerOptions.querySelector("input")?.focus({ preventScroll: true });
        }
    };

    const toggleSearchMenu = () => {
        const opening = elements.searchPanel.hidden;
        if (opening) {
            closeProviderMenu();
            closeAccountMenu();
            closeProgressMenu();
        }
        elements.searchPanel.hidden = !opening;
        elements.searchMenuButton.setAttribute("aria-expanded", opening.toString());
        elements.searchMenuButton.setAttribute(
            "aria-label",
            opening ? "Stempelstellensuche schließen" : "Stempelstellensuche öffnen");
        if (opening) {
            renderSearchResults();
            elements.stampingPointSearchInput.focus({ preventScroll: true });
        }
    };

    const clearMarkers = () => {
        app.markerSource.clear();
    };

    const resetPointCache = () => {
        app.pointCache[VisitState.unknown] = [];
        app.pointCache[VisitState.open] = [];
        app.pointCache[VisitState.visited] = [];
    };

    const isVisitStateVisible = visitState => app.visitFilter === VisitFilter.all
        || app.visitFilter === visitState;

    const updateVisitFilterButton = () => {
        const filterDetails = {
            [VisitFilter.all]: {
                label: "Alle Stempelstellen",
                next: "Nur offene Stempelstellen"
            },
            [VisitFilter.open]: {
                label: "Nur offene Stempelstellen",
                next: "Nur gestempelte Stempelstellen"
            },
            [VisitFilter.visited]: {
                label: "Nur gestempelte Stempelstellen",
                next: "Alle Stempelstellen"
            }
        };
        const details = filterDetails[app.visitFilter];
        elements.visitFilterButton.dataset.visitFilter = app.visitFilter;
        elements.visitFilterButton.title = `${details.label} anzeigen`;
        elements.visitFilterButton.setAttribute(
            "aria-label",
            `Besuchsfilter: ${details.label}. Nächster Zustand: ${details.next}.`);
    };

    const getSafeExternalUrl = value => {
        if (typeof value !== "string") {
            return null;
        }
        try {
            const url = new URL(value);
            return url.protocol === "http:" || url.protocol === "https:" ? url.href : null;
        } catch {
            return null;
        }
    };

    const closeProviderInfo = () => {
        if (elements.providerInfoDialog.open) {
            elements.providerInfoDialog.close();
        }
    };

    const openProviderInfo = (provider, trigger) => {
        elements.providerInfoName.textContent = provider.name;
        elements.providerInfoDescription.textContent = provider.description
            || "Für diesen Anbieter sind noch keine weiteren Informationen hinterlegt.";
        const websiteUrl = getSafeExternalUrl(provider.websiteUrl);
        elements.providerInfoWebsite.hidden = !websiteUrl;
        if (websiteUrl) {
            elements.providerInfoWebsite.href = websiteUrl;
        } else {
            elements.providerInfoWebsite.removeAttribute("href");
        }
        const sourceUrl = getSafeExternalUrl(provider.dataSourceUrl);
        const licenseUrl = getSafeExternalUrl(provider.dataLicenseUrl);
        const hasDataSource = Boolean(
            provider.dataSourceAttribution
            && provider.dataLicenseName
            && sourceUrl
            && licenseUrl);
        elements.providerDataSource.hidden = !hasDataSource;
        if (hasDataSource) {
            elements.providerDataAttribution.textContent = provider.dataSourceAttribution;
            elements.providerDataSourceLink.href = sourceUrl;
            elements.providerDataLicenseLink.href = licenseUrl;
            elements.providerDataLicenseLink.textContent = provider.dataLicenseName;
            if (provider.hasPublicDataDownload) {
                elements.providerDataDownload.hidden = false;
                elements.providerDataDownload.href = `api/providers/${encodeURIComponent(provider.slug)}/points.geojson`;
                elements.providerDataDownload.download = `${provider.slug}-stempelstellen.geojson`;
            } else {
                elements.providerDataDownload.hidden = true;
                elements.providerDataDownload.removeAttribute("href");
                elements.providerDataDownload.removeAttribute("download");
            }
        } else {
            elements.providerDataAttribution.textContent = "";
            elements.providerDataSourceLink.removeAttribute("href");
            elements.providerDataLicenseLink.removeAttribute("href");
            elements.providerDataLicenseLink.textContent = "";
            elements.providerDataDownload.hidden = true;
            elements.providerDataDownload.removeAttribute("href");
            elements.providerDataDownload.removeAttribute("download");
        }
        elements.providerInfoTrigger = trigger;
        elements.providerInfoDialog.showModal();
        elements.closeProviderInfoButton.focus({ preventScroll: true });
    };

    const getFilterableProviders = () => app.providers.filter(provider =>
        app.hasCompleteProviderCatalog
            ? provider.isEnabled && provider.isDataReady
            : provider.isAnonymousAccessAllowed === true);

    const renderProviderOptions = () => {
        elements.providerOptions.replaceChildren();
        const enabledReadyProviders = getFilterableProviders();
        if (enabledReadyProviders.length === 0) {
            const status = document.createElement("p");
            status.className = "provider-options-status";
            status.textContent = "Keine Stempelanbieter verfügbar.";
            elements.providerOptions.appendChild(status);
            return;
        }

        enabledReadyProviders.forEach((provider, index) => {
            const row = document.createElement("div");
            row.className = "provider-option";

            const label = document.createElement("label");
            const checkbox = document.createElement("input");
            checkbox.type = "checkbox";
            checkbox.value = provider.slug;
            checkbox.id = `provider-${index}`;
            checkbox.checked = app.selectedProviderSlugs.has(provider.slug);
            const name = document.createElement("span");
            name.textContent = provider.name;
            label.htmlFor = checkbox.id;
            label.append(checkbox, name);

            row.appendChild(label);
            elements.providerOptions.appendChild(row);
        });
    };

    const createLockSvg = () => {
        const svg = document.createElementNS("http://www.w3.org/2000/svg", "svg");
        svg.setAttribute("aria-hidden", "true");
        svg.setAttribute("viewBox", "0 0 24 24");
        svg.setAttribute("focusable", "false");
        const rect = document.createElementNS("http://www.w3.org/2000/svg", "rect");
        rect.setAttribute("x", "3");
        rect.setAttribute("y", "11");
        rect.setAttribute("width", "18");
        rect.setAttribute("height", "11");
        rect.setAttribute("rx", "2");
        rect.setAttribute("ry", "2");
        const path = document.createElementNS("http://www.w3.org/2000/svg", "path");
        path.setAttribute("d", "M7 11V7a5 5 0 0 1 10 0v4");
        svg.append(rect, path);
        return svg;
    };

    const sortProvidersForProgressList = (providers, statsBySlug) => {
        const ready = [];
        const notReady = [];
        for (const provider of providers) {
            if (provider.isDataReady) {
                ready.push(provider);
            } else {
                notReady.push(provider);
            }
        }
        ready.sort((a, b) => {
            const statsA = statsBySlug.get(a.slug) || { visited: 0, total: 0 };
            const statsB = statsBySlug.get(b.slug) || { visited: 0, total: 0 };
            const ratioA = statsA.total > 0 ? statsA.visited / statsA.total : 0;
            const ratioB = statsB.total > 0 ? statsB.visited / statsB.total : 0;
            if (ratioB !== ratioA) {
                return ratioB - ratioA;
            }
            return (a.name || "").localeCompare(b.name || "", "de");
        });
        notReady.sort((a, b) => (a.name || "").localeCompare(b.name || "", "de"));
        return [...ready, ...notReady];
    };

    const calculateProgressStats = () => {
        const pendingDeltasBySlug = new Map();
        for (const action of app.pendingActions.values()) {
            const cachedPoint = Object.values(app.pointCache)
                .flat()
                .find(point => point.id === action.pointId);
            const isPermanent = typeof action.countsTowardProgress === "boolean"
                ? action.countsTowardProgress
                : cachedPoint?.countsTowardProgress === true;
            if (!isPermanent) continue;
            const providerSlug = action.providerSlug;
            if (!providerSlug) continue;
            let delta = 0;
            if (!action.expected?.isVisited && action.desired?.isVisited) {
                delta = 1;
            } else if (action.expected?.isVisited && !action.desired?.isVisited) {
                delta = -1;
            }
            if (delta !== 0) {
                pendingDeltasBySlug.set(providerSlug, (pendingDeltasBySlug.get(providerSlug) || 0) + delta);
            }
        }

        const statsBySlug = new Map();
        for (const provider of app.providers) {
            if (!provider.isDataReady) {
                statsBySlug.set(provider.slug, { ready: false, isEnabled: Boolean(provider.isEnabled) });
            } else {
                const total = typeof provider.totalPoints === "number" ? provider.totalPoints : 0;
                const baseVisited = typeof provider.visitedPoints === "number" ? provider.visitedPoints : 0;
                const delta = pendingDeltasBySlug.get(provider.slug) || 0;
                const visited = Math.max(0, Math.min(total, baseVisited + delta));
                const percent = total > 0 ? Math.round((visited / total) * 100) : 0;
                statsBySlug.set(provider.slug, {
                    ready: true,
                    isEnabled: Boolean(provider.isEnabled),
                    total,
                    visited,
                    percent
                });
            }
        }

        return statsBySlug;
    };

    const updateProgressSummaryAria = () => {
        if (!app.hasCompleteProviderCatalog) {
            elements.progressButtonCount.textContent = "Online aktualisieren";
            elements.progressButtonFill.style.width = "0%";
            elements.progressButtonSrPercent.textContent = "Fortschritt offline nicht verfügbar";
            elements.progressButton.setAttribute(
                "aria-label",
                elements.progressPanel.hidden
                    ? "Gesamtfortschritt ist offline noch nicht verfügbar. Fortschrittsübersicht öffnen."
                    : "Gesamtfortschritt ist offline noch nicht verfügbar. Fortschrittsübersicht schließen.");
            return;
        }

        const statsBySlug = calculateProgressStats();
        const enabledReadyProviders = app.providers.filter(p => p.isEnabled && p.isDataReady);
        if (enabledReadyProviders.length === 0) {
            elements.progressButtonCount.textContent = "Keine Anbieter freigeschaltet";
            elements.progressButtonFill.style.width = "0%";
            elements.progressButtonSrPercent.textContent = "0 %";
            elements.progressButton.setAttribute(
                "aria-label",
                elements.progressPanel.hidden
                    ? "Gesamtfortschritt: Keine Anbieter freigeschaltet. Fortschrittsübersicht öffnen."
                    : "Gesamtfortschritt: Keine Anbieter freigeschaltet. Fortschrittsübersicht schließen.");
        } else {
            let overallTotal = 0;
            let overallVisited = 0;
            for (const provider of enabledReadyProviders) {
                const stat = statsBySlug.get(provider.slug);
                if (stat && stat.ready) {
                    overallTotal += stat.total;
                    overallVisited += stat.visited;
                }
            }
            const overallPercent = overallTotal > 0 ? Math.round((overallVisited / overallTotal) * 100) : 0;
            elements.progressButtonCount.textContent = `${overallVisited} / ${overallTotal}`;
            elements.progressButtonFill.style.width = `${overallPercent}%`;
            elements.progressButtonSrPercent.textContent = `${overallPercent} %`;
            elements.progressButton.setAttribute(
                "aria-label",
                elements.progressPanel.hidden
                    ? `Gesamtfortschritt: ${overallVisited} von ${overallTotal} Stempeln (${overallPercent} %). Fortschrittsübersicht öffnen.`
                    : `Gesamtfortschritt: ${overallVisited} von ${overallTotal} Stempeln (${overallPercent} %). Fortschrittsübersicht schließen.`);
        }
    };

    const renderProgressOverview = () => {
        const statsBySlug = calculateProgressStats();
        updateProgressSummaryAria();

        elements.progressList.replaceChildren();
        if (app.providers.length === 0) {
            const status = document.createElement("p");
            status.className = "provider-options-status";
            status.textContent = "Keine Stempelanbieter verfügbar.";
            elements.progressList.appendChild(status);
            return;
        }

        if (!app.hasCompleteProviderCatalog) {
            const notice = document.createElement("p");
            notice.className = "provider-options-status";
            notice.textContent = "Die vollständige Fortschrittsübersicht ist nach der nächsten Online-Aktualisierung verfügbar.";
            elements.progressList.appendChild(notice);
            return;
        }

        const sortedProviders = sortProvidersForProgressList(app.providers, statsBySlug);
        for (const provider of sortedProviders) {
            const stat = statsBySlug.get(provider.slug);
            const isLocked = !provider.isEnabled;
            const isNotReady = !provider.isDataReady;

            const item = document.createElement("div");
            item.className = "progress-item";
            if (isLocked) item.classList.add("progress-item--locked");
            if (isNotReady) item.classList.add("progress-item--not-ready");
            item.setAttribute("role", "listitem");

            const header = document.createElement("div");
            header.className = "progress-item__header";

            const title = document.createElement("div");
            title.className = "progress-item__title";

            const nameSpan = document.createElement("span");
            nameSpan.textContent = provider.name;
            title.appendChild(nameSpan);

            if (provider.abbreviation) {
                const abbrSpan = document.createElement("span");
                abbrSpan.className = "progress-item__abbr";
                abbrSpan.textContent = provider.abbreviation;
                title.appendChild(abbrSpan);
            }

            if (isLocked) {
                const lockSpan = document.createElement("span");
                lockSpan.className = "progress-item__lock";
                lockSpan.title = "Nicht freigeschaltet";
                lockSpan.appendChild(createLockSvg());
                const lockSr = document.createElement("span");
                lockSr.className = "visually-hidden";
                lockSr.textContent = "(Nicht freigeschaltet)";
                lockSpan.appendChild(lockSr);
                title.appendChild(lockSpan);
            }

            const infoButton = document.createElement("button");
            infoButton.type = "button";
            infoButton.className = "provider-info-button";
            infoButton.setAttribute("aria-label", `Informationen zu ${provider.name}`);
            infoButton.setAttribute("aria-haspopup", "dialog");
            const qMark = document.createElement("span");
            qMark.setAttribute("aria-hidden", "true");
            qMark.textContent = "?";
            infoButton.appendChild(qMark);
            infoButton.addEventListener("click", () => openProviderInfo(provider, infoButton));

            header.append(title, infoButton);

            const body = document.createElement("div");
            body.className = "progress-item__body";

            if (isNotReady) {
                const status = document.createElement("div");
                status.className = "progress-item__status";
                status.textContent = "In Vorbereitung";
                body.appendChild(status);
            } else if (stat && stat.ready) {
                const stats = document.createElement("div");
                stats.className = "progress-item__stats";

                const countSpan = document.createElement("span");
                countSpan.textContent = `${stat.visited} / ${stat.total}`;

                const percentSpan = document.createElement("span");
                percentSpan.textContent = `${stat.percent} %`;

                stats.append(countSpan, percentSpan);

                const bar = document.createElement("div");
                bar.className = "progress-bar";
                bar.setAttribute("aria-hidden", "true");

                const fill = document.createElement("div");
                fill.className = "progress-bar__fill";
                fill.style.width = `${stat.percent}%`;
                bar.appendChild(fill);

                body.append(stats, bar);
            }

            item.append(header, body);
            elements.progressList.appendChild(item);
        }
    };

    const setProviderCatalog = (response, resetSelection = true) => {
        app.providers = Array.isArray(response.stampingProviders)
            ? response.stampingProviders.filter(provider => typeof provider.slug === "string")
            : [];
        app.hasCompleteProviderCatalog = typeof response.totalPoints === "number"
            && typeof response.visitedPoints === "number"
            && app.providers.every(provider =>
                typeof provider.isEnabled === "boolean"
                && typeof provider.isDataReady === "boolean");
        const filterableSlugs = new Set(getFilterableProviders().map(provider => provider.slug));
        app.selectedProviderSlugs = resetSelection
            ? filterableSlugs
            : new Set([...app.selectedProviderSlugs].filter(slug => filterableSlugs.has(slug)));
        renderProviderOptions();
        renderProgressOverview();
    };

    const resetProviderCatalog = () => {
        app.providers = [];
        app.hasCompleteProviderCatalog = false;
        app.selectedProviderSlugs = new Set();
        renderProviderOptions();
        renderProgressOverview();
    };

    const createMarker = (stampingPoint, visitState) => {
        const feature = new ol.Feature(new ol.geom.Point(ol.proj.fromLonLat([
            stampingPoint.position.longitude,
            stampingPoint.position.latitude
        ])));
        feature.stampingPoint = stampingPoint;
        feature.visitState = visitState;
        return feature;
    };

    const addPoints = (stampingPoints, visitState) => {
        const features = stampingPoints.map(point => createMarker(point, visitState));
        app.markerSource.addFeatures(features);
        return features.length;
    };

    const cachePoints = (response, visitState) => {
        app.pointCache[visitState] = Array.isArray(response.stampingPoints)
            ? response.stampingPoints
            : [];
    };

    const normalizeSearchText = value => String(value ?? "")
        .normalize("NFD")
        .replace(/[\u0300-\u036f]/g, "")
        .replace(/ß/g, "ss")
        .toLocaleLowerCase("de-DE")
        .replace(/\s+/g, " ")
        .trim();

    const getPointNumberLabel = point => {
        if (point.number === null || point.number === undefined) {
            return point.series?.name ?? "Sonderstempel";
        }
        const prefix = point.provider?.abbreviation
            ? `${point.provider.abbreviation} ${point.number}`
            : `Stempelstelle ${point.number}`;
        return point.series?.slug && point.series.slug !== "standard"
            ? `${point.series.name} ${point.number}`
            : prefix;
    };

    const getSearchablePoints = () => Object.values(VisitState).flatMap(visitState =>
        isVisitStateVisible(visitState) ? app.pointCache[visitState]
            .filter(point => app.selectedProviderSlugs.has(point.provider?.slug))
            .map(point => ({ point, visitState })) : []);

    const findRenderedFeature = result => app.markerSource
        .getFeatures()
        .find(feature => pointMatches(feature.stampingPoint, result.point));

    const focusPointFeature = feature => {
        const coordinate = feature.getGeometry().getCoordinates();
        const view = app.map.getView();
        const zoom = Math.max(view.getZoom() ?? 0, 15);
        const showSelectedPoint = () => {
            const pixel = app.map.getPixelFromCoordinate(coordinate);
            showInfo(feature, pixel, true);
        };
        if (reducedMotion.matches) {
            view.setCenter(coordinate);
            view.setZoom(zoom);
            showSelectedPoint();
        } else {
            view.animate({ center: coordinate, zoom, duration: 250 }, completed => {
                if (completed) {
                    showSelectedPoint();
                }
            });
        }
    };

    const openSearchResult = result => {
        const feature = findRenderedFeature(result);
        if (!feature) {
            elements.searchResultsStatus.textContent = "Der Treffer ist momentan nicht auf der Karte verfügbar.";
            return;
        }

        closeSearchMenu();
        focusPointFeature(feature);
    };

    const openPendingPointLink = () => {
        const pointLink = app.pendingPointLink;
        if (!pointLink || !app.authenticated) {
            return false;
        }
        app.pendingPointLink = null;

        let result = null;
        for (const visitState of Object.values(VisitState)) {
            const point = app.pointCache[visitState].find(candidate =>
                candidate.id === pointLink.pointId
                && candidate.provider?.slug === pointLink.providerSlug);
            if (point) {
                result = { point, visitState };
                break;
            }
        }

        if (!result) {
            setMapStatus("Die verlinkte Stempelstelle ist nicht verfügbar oder nicht freigeschaltet.", "error");
            return true;
        }

        if (!app.selectedProviderSlugs.has(pointLink.providerSlug)) {
            app.selectedProviderSlugs.add(pointLink.providerSlug);
            renderProviderOptions();
            renderSelectedPoints();
            renderSearchResults();
        }

        const feature = findRenderedFeature(result);
        if (!feature) {
            setMapStatus("Die verlinkte Stempelstelle ist mit dem aktuellen Filter nicht sichtbar.", "error");
            return true;
        }

        focusPointFeature(feature);
        return true;
    };

    const renderSearchResults = () => {
        elements.searchResults.replaceChildren();
        const query = normalizeSearchText(elements.stampingPointSearchInput.value);
        if (!query) {
            elements.searchResultsStatus.textContent = "Suche innerhalb der angezeigten Stempelstellen.";
            return;
        }

        const searchablePoints = getSearchablePoints();
        if (searchablePoints.length === 0) {
            elements.searchResultsStatus.textContent = "Mit den aktuellen Filtern sind keine Stempelstellen verfügbar.";
            return;
        }

        const queryTokens = query.split(" ");
        const matches = searchablePoints
            .map(result => {
                const point = result.point;
                const numberLabel = getPointNumberLabel(point);
                const compactNumber = point.number === null || point.number === undefined
                    ? ""
                    : `${point.provider?.abbreviation ?? ""}${point.number}`;
                const haystack = normalizeSearchText([
                    point.name,
                    point.number,
                    point.series?.name,
                    point.series?.slug,
                    numberLabel,
                    compactNumber,
                    point.provider?.name,
                    point.provider?.abbreviation
                ].join(" "));
                const normalizedName = normalizeSearchText(point.name);
                const normalizedNumber = normalizeSearchText(numberLabel);
                const score = normalizedNumber === query || normalizeSearchText(compactNumber) === query
                    ? 0
                    : normalizedName.startsWith(query)
                        ? 1
                        : 2;
                return { ...result, numberLabel, haystack, score };
            })
            .filter(result => queryTokens.every(token => result.haystack.includes(token)))
            .sort((left, right) => left.score - right.score
                || left.point.name.localeCompare(right.point.name, "de")
                || left.point.number - right.point.number);

        if (matches.length === 0) {
            elements.searchResultsStatus.textContent = "Keine passenden Stempelstellen gefunden.";
            return;
        }

        const visibleMatches = matches.slice(0, SearchResultLimit);
        elements.searchResultsStatus.textContent = matches.length > SearchResultLimit
            ? `${SearchResultLimit} von ${matches.length} Treffern angezeigt. Suche genauer, um die Liste einzugrenzen.`
            : `${matches.length} Treffer.`;
        for (const result of visibleMatches) {
            const item = document.createElement("li");
            const button = document.createElement("button");
            button.type = "button";
            button.className = "search-result-button";
            button.setAttribute("aria-label", `${result.numberLabel}: ${result.point.name} auf der Karte anzeigen`);
            const number = document.createElement("span");
            number.className = "search-result-number";
            number.textContent = result.numberLabel;
            const name = document.createElement("span");
            name.className = "search-result-name";
            name.textContent = result.point.name;
            button.append(number, name);
            button.addEventListener("click", () => openSearchResult(result));
            item.appendChild(button);
            elements.searchResults.appendChild(item);
        }
    };

    const renderSelectedPoints = () => {
        hideInfo(true);
        clearMarkers();
        return Object.values(VisitState).reduce((count, visitState) => {
            if (!isVisitStateVisible(visitState)) {
                return count;
            }
            const selectedPoints = app.pointCache[visitState].filter(point =>
                app.selectedProviderSlugs.has(point.provider?.slug));
            return count + addPoints(selectedPoints, visitState);
        }, 0);
    };

    const setSelectedProviders = providerSlugs => {
        app.selectedProviderSlugs = new Set(providerSlugs);
        return renderSelectedPoints();
    };

    const formatPointCount = pointCount => pointCount === 1
        ? "1 Stempelstelle"
        : `${pointCount} Stempelstellen`;

    const announceFilteredPointCount = pointCount => {
        const message = `${formatPointCount(pointCount)} angezeigt.`;
        setMapStatus(message, "ready");
        if (pointCount > 0) {
            window.setTimeout(() => {
                if (elements.mapStatus.textContent === message) {
                    setMapStatus("");
                }
            }, 1800);
        }
    };

    const applyProviderSelection = () => {
        const selectedSlugs = Array.from(
            elements.providerOptions.querySelectorAll('input[type="checkbox"]:checked'),
            checkbox => checkbox.value);
        announceFilteredPointCount(setSelectedProviders(selectedSlugs));
        renderSearchResults();
    };

    const selectProviders = selected => {
        for (const checkbox of elements.providerOptions.querySelectorAll('input[type="checkbox"]')) {
            checkbox.checked = selected;
        }
        applyProviderSelection();
    };

    const cycleVisitFilter = () => {
        closeSearchMenu();
        closeProviderMenu();
        closeAccountMenu();
        closeProgressMenu();
        const currentIndex = VisitFilterOrder.indexOf(app.visitFilter);
        app.visitFilter = VisitFilterOrder[(currentIndex + 1) % VisitFilterOrder.length];
        updateVisitFilterButton();
        announceFilteredPointCount(renderSelectedPoints());
        renderSearchResults();
    };

    const getJson = async (url) => {
        const response = await fetch(url, {
            headers: { "Accept": "application/json" }
        });
        if (!response.ok) {
            throw response;
        }
        return await response.json();
    };

    const sendVisitStateRequest = async action => {
        const provider = encodeURIComponent(action.providerSlug);
        const response = await fetch(`api/points/id/${action.pointId}/state?provider=${provider}`, {
            method: "PUT",
            headers: { "Accept": "application/json", "Content-Type": "application/json" },
            body: JSON.stringify({
                expected: action.expected,
                desired: action.desired,
                utcOffsetMinutes: action.utcOffsetMinutes
            })
        });
        let body = null;
        if (response.headers.get("content-type")?.includes("json")) {
            try {
                body = await response.json();
            } catch {
                body = null;
            }
        }
        return { response, body };
    };

    const hideInfo = (force = false) => {
        if (app.infoLocked && !force) {
            return;
        }
        elements.infoCard.hidden = true;
        elements.pointShareControls.hidden = true;
        elements.pointShareStatus.textContent = "";
        delete elements.pointShareStatus.dataset.state;
        elements.visitForm.hidden = true;
        elements.visitActionStatus.textContent = "";
        app.activeFeature = null;
        app.infoLocked = false;
        app.infoPixel = null;
    };

    const formatVisit = (stampingPoint) => {
        if (!stampingPoint.visitedOn) {
            return null;
        }

        const dateParts = stampingPoint.visitedOn.split("-").map(Number);
        if (dateParts.length !== 3 || dateParts.some(Number.isNaN)) {
            return null;
        }
        const visitedDate = new Date(dateParts[0], dateParts[1] - 1, dateParts[2]);
        let text = visitedDate.toLocaleDateString("de-DE", {
            year: "numeric",
            month: "long",
            day: "2-digit"
        });
        let dateTime = stampingPoint.visitedOn;
        if (stampingPoint.visitedAt) {
            const time = stampingPoint.visitedAt.slice(0, 5);
            text += ` um ${time} Uhr`;
            dateTime += `T${stampingPoint.visitedAt}`;
        }
        return { text, dateTime };
    };

    const pointMatches = (left, right) => left.id === right.id;

    const createPointLink = stampingPoint => {
        const providerSlug = stampingPoint.provider?.slug;
        if (!providerSlug || !Number.isSafeInteger(stampingPoint.id) || stampingPoint.id <= 0) {
            return null;
        }
        const url = new URL("./", document.baseURI);
        url.searchParams.set("provider", providerSlug);
        url.searchParams.set("point", String(stampingPoint.id));
        return url.href;
    };

    const copyText = async text => {
        if (navigator.clipboard?.writeText) {
            await navigator.clipboard.writeText(text);
            return;
        }

        const field = document.createElement("textarea");
        field.value = text;
        field.setAttribute("readonly", "");
        field.style.position = "fixed";
        field.style.opacity = "0";
        document.body.appendChild(field);
        field.select();
        const copied = document.execCommand("copy");
        field.remove();
        if (!copied) {
            throw new Error("Copy command failed");
        }
    };

    const copyActivePointLink = async () => {
        const link = app.activeFeature && createPointLink(app.activeFeature.stampingPoint);
        if (!link) {
            return;
        }
        elements.copyPointLinkButton.disabled = true;
        try {
            await copyText(link);
            elements.pointShareStatus.textContent = "Link kopiert.";
            elements.pointShareStatus.dataset.state = "ready";
        } catch {
            elements.pointShareStatus.textContent = "Der Link konnte nicht kopiert werden.";
            elements.pointShareStatus.dataset.state = "error";
        } finally {
            elements.copyPointLinkButton.disabled = false;
        }
    };

    const visitStateFromPoint = point => ({
        isVisited: point.isVisited === true,
        visitedOn: point.isVisited === true ? point.visitedOn ?? null : null,
        visitedAt: point.isVisited === true ? point.visitedAt ?? null : null
    });

    const normalizedVisitState = state => ({
        isVisited: state?.isVisited === true,
        visitedOn: state?.isVisited === true ? state.visitedOn ?? null : null,
        visitedAt: state?.isVisited === true ? state.visitedAt ?? null : null
    });

    const applyStateToPoint = (point, state) => {
        const normalized = normalizedVisitState(state);
        return {
            ...point,
            isVisited: normalized.isVisited,
            visitedOn: normalized.visitedOn,
            visitedAt: normalized.visitedAt
        };
    };

    const replacePointInResponses = (snapshot, pointId, state, canonicalPoint = null) => {
        const allPoints = [
            ...(snapshot.unvisitedPoints?.stampingPoints ?? []),
            ...(snapshot.visitedPoints?.stampingPoints ?? [])
        ];
        const existing = allPoints.find(point => point.id === pointId);
        const point = canonicalPoint
            ? {
                ...(existing ?? {}),
                ...canonicalPoint,
                tours: existing?.tours ?? canonicalPoint.tours
            }
            : (existing ? applyStateToPoint(existing, state) : null);
        const withoutPoint = allPoints.filter(candidate => candidate.id !== pointId);
        if (point) withoutPoint.push(point);
        return {
            ...snapshot,
            unvisitedPoints: {
                ...(snapshot.unvisitedPoints ?? {}),
                stampingPoints: withoutPoint.filter(candidate => !candidate.isVisited),
                overallCount: withoutPoint.filter(candidate => !candidate.isVisited).length
            },
            visitedPoints: {
                ...(snapshot.visitedPoints ?? {}),
                stampingPoints: withoutPoint.filter(candidate => candidate.isVisited),
                overallCount: withoutPoint.filter(candidate => candidate.isVisited).length
            },
            savedAt: new Date().toISOString()
        };
    };

    const setPendingActions = actions => {
        app.pendingActions = new Map((actions ?? []).map(action => [action.pointId, action]));
    };

    const overlayPendingActions = (unvisited, visited, actions) => {
        let combined = {
            unvisitedPoints: { ...unvisited, stampingPoints: [...unvisited.stampingPoints] },
            visitedPoints: { ...visited, stampingPoints: [...visited.stampingPoints] }
        };
        for (const action of actions ?? []) {
            combined = replacePointInResponses(combined, action.pointId, action.desired);
        }
        return {
            unvisited: combined.unvisitedPoints,
            visited: combined.visitedPoints
        };
    };

    const hasPendingAction = pointId => app.pendingActions.has(pointId);

    const loadSnapshotData = snapshot => {
        setPendingActions(snapshot.pendingActions);
        setProviderCatalog(snapshot.providers);
        cachePoints(snapshot.unvisitedPoints, VisitState.open);
        cachePoints(snapshot.visitedPoints, VisitState.visited);
        const pointCount = renderSelectedPoints();
        renderSearchResults();
        renderProgressOverview();
        return pointCount;
    };

    const restoreLockedInfo = (pointId, pixel, locked) => {
        if (pointId === null || !locked) return;
        const renderedFeature = app.markerSource.getFeatures().find(feature =>
            feature.stampingPoint.id === pointId);
        if (renderedFeature) showInfo(renderedFeature, pixel, true);
    };

    const updatePointVisit = (feature, isVisited, visitedOn = null, visitedAt = null) => {
        const stampingPoint = feature.stampingPoint;
        const infoPixel = app.infoPixel;
        for (const visitState of Object.values(VisitState)) {
            app.pointCache[visitState] = app.pointCache[visitState].filter(point =>
                !pointMatches(point, stampingPoint));
        }

        stampingPoint.isVisited = isVisited;
        stampingPoint.visitedOn = visitedOn;
        stampingPoint.visitedAt = visitedAt;
        feature.visitState = isVisited ? VisitState.visited : VisitState.open;
        app.pointCache[feature.visitState].push(stampingPoint);
        const pointCount = renderSelectedPoints();
        renderSearchResults();
        renderProgressOverview();
        if (isVisitStateVisible(feature.visitState)) {
            const renderedFeature = findRenderedFeature({ point: stampingPoint, visitState: feature.visitState });
            if (renderedFeature) {
                showInfo(renderedFeature, infoPixel, true);
            }
        } else {
            announceFilteredPointCount(pointCount);
        }

        return Promise.resolve();
    };

    const refreshFromStoredSnapshot = async () => {
        const snapshot = await getStoredSnapshot();
        if (!isSnapshotValid(snapshot) ||
            !app.authenticated ||
            snapshot.email.toLocaleLowerCase() !== app.sessionEmail?.toLocaleLowerCase()) {
            return;
        }
        const activePointId = app.activeFeature?.stampingPoint.id ?? null;
        const activePixel = app.infoPixel;
        const activeLocked = app.infoLocked;
        setPendingActions(snapshot.pendingActions);
        setProviderCatalog(snapshot.providers, app.providers.length === 0);
        cachePoints(snapshot.unvisitedPoints, VisitState.open);
        cachePoints(snapshot.visitedPoints, VisitState.visited);
        renderSelectedPoints();
        renderSearchResults();
        renderProgressOverview();
        restoreLockedInfo(activePointId, activePixel, activeLocked);
    };

    const clearRetryTimer = () => {
        if (app.retryTimer !== null) {
            window.clearTimeout(app.retryTimer);
            app.retryTimer = null;
        }
    };

    const scheduleSynchronizationRetry = () => {
        if (app.retryTimer !== null || app.pendingActions.size === 0) return;
        const delay = Math.min(1000 * (2 ** app.retryAttempt), RETRY_MAX_MILLISECONDS);
        app.retryAttempt += 1;
        app.retryTimer = window.setTimeout(() => {
            app.retryTimer = null;
            synchronizePendingActions();
        }, delay);
    };

    const clearPersonalData = async (broadcast = true) => {
        clearRetryTimer();
        app.backOnlineNoticePending = false;
        setPendingActions([]);
        resetProviderCatalog();
        await clearStoredSnapshot();
        if (broadcast) broadcastSyncEvent("personal-data-cleared");
    };

    const acquireSyncLease = async () => {
        let acquired = false;
        const result = await updateStoredSnapshot(snapshot => {
            if (!isSnapshotValid(snapshot)) return snapshot;
            const lease = snapshot.syncLease;
            if (lease && lease.owner !== TAB_ID && Date.parse(lease.expiresAt) > Date.now()) {
                return snapshot;
            }
            acquired = true;
            return {
                ...snapshot,
                syncLease: {
                    owner: TAB_ID,
                    expiresAt: new Date(Date.now() + SYNC_LEASE_MILLISECONDS).toISOString()
                }
            };
        });
        return result.ok && acquired;
    };

    const renewSyncLease = () => updateStoredSnapshot(snapshot => {
        if (!snapshot || snapshot.syncLease?.owner !== TAB_ID) return snapshot;
        return {
            ...snapshot,
            syncLease: {
                owner: TAB_ID,
                expiresAt: new Date(Date.now() + SYNC_LEASE_MILLISECONDS).toISOString()
            }
        };
    });

    const releaseSyncLease = () => updateStoredSnapshot(snapshot => {
        if (!snapshot || snapshot.syncLease?.owner !== TAB_ID) return snapshot;
        const { syncLease, ...withoutLease } = snapshot;
        return withoutLease;
    });

    const updateProviderProgressInSnapshot = (snapshot, action, state, canonicalPoint) => {
        const providersResponse = snapshot.providers;
        if (!providersResponse || !Array.isArray(providersResponse.stampingProviders)) {
            return snapshot;
        }

        const countsTowardProgress = typeof canonicalPoint?.countsTowardProgress === "boolean"
            ? canonicalPoint.countsTowardProgress
            : action.countsTowardProgress === true;
        if (!countsTowardProgress || action.expected?.isVisited === state.isVisited) {
            return snapshot;
        }

        const delta = state.isVisited ? 1 : -1;
        let enabledReadyDelta = 0;
        const stampingProviders = providersResponse.stampingProviders.map(provider => {
            if (provider.slug !== action.providerSlug || typeof provider.visitedPoints !== "number") {
                return provider;
            }

            const total = typeof provider.totalPoints === "number" ? provider.totalPoints : 0;
            const visitedPoints = Math.max(0, Math.min(total, provider.visitedPoints + delta));
            if (provider.isEnabled && provider.isDataReady) {
                enabledReadyDelta = visitedPoints - provider.visitedPoints;
            }
            return { ...provider, visitedPoints };
        });

        return {
            ...snapshot,
            providers: {
                ...providersResponse,
                visitedPoints: typeof providersResponse.visitedPoints === "number"
                    ? Math.max(0, providersResponse.visitedPoints + enabledReadyDelta)
                    : providersResponse.visitedPoints,
                stampingProviders
            }
        };
    };

    const finishPendingAction = async (action, state, canonicalPoint = null) => {
        const result = await updateStoredSnapshot(snapshot => {
            if (!isSnapshotValid(snapshot)) return snapshot;
            const pendingActions = snapshot.pendingActions.filter(pending =>
                pending.pointId !== action.pointId || pending.createdAt !== action.createdAt);
            const updatedSnapshot = updateProviderProgressInSnapshot(
                snapshot,
                action,
                state,
                canonicalPoint);
            return {
                ...replacePointInResponses(updatedSnapshot, action.pointId, state, canonicalPoint),
                pendingActions
            };
        });
        if (!result.ok || !result.snapshot) return false;
        setPendingActions(result.snapshot.pendingActions);
        await refreshFromStoredSnapshot();
        renderProgressOverview();
        broadcastSyncEvent("snapshot-updated");
        return true;
    };

    const showBackOnlineNotice = () => {
        const message = "TourEd ist wieder online.";
        setMapStatus(message, "ready");
        window.setTimeout(() => {
            if (elements.mapStatus.textContent === message) setMapStatus("");
        }, 3000);
    };

    const synchronizePendingActions = (knownSession = null) => {
        clearRetryTimer();
        if (app.syncPromise) return app.syncPromise;

        app.syncPromise = (async () => {
            let snapshot = await getStoredSnapshot();
            if (!isSnapshotValid(snapshot)) {
                await clearPersonalData();
                return;
            }
            if (snapshot.pendingActions.length === 0) {
                setPendingActions([]);
                return;
            }
            setPendingActions(snapshot.pendingActions);

            let session = knownSession;
            if (!session) {
                try {
                    session = await getJson("auth/session");
                } catch (error) {
                    if (!(error instanceof Response)) setOfflineMode(true);
                    scheduleSynchronizationRetry();
                    return;
                }
            }

            if (!session?.authenticated) {
                await clearPersonalData();
                setSession({ authenticated: false });
                showAuthBarrier();
                return;
            }

            if (snapshot.email.toLocaleLowerCase() !== session.email.toLocaleLowerCase()) {
                await clearPersonalData();
                window.queueMicrotask(() => initialize());
                return;
            }

            setSession(session);
            setOfflineMode(false);
            await updateStoredSnapshot(current => current ? {
                ...current,
                expiresAt: session.expiresAt ?? current.expiresAt
            } : current);

            if (!await acquireSyncLease()) {
                scheduleSynchronizationRetry();
                return;
            }

            let shouldReload = false;
            let synchronizedAny = false;
            try {
                while (true) {
                    snapshot = await getStoredSnapshot();
                    if (!isSnapshotValid(snapshot) || snapshot.pendingActions.length === 0) {
                        app.retryAttempt = 0;
                        if (synchronizedAny) app.backOnlineNoticePending = true;
                        break;
                    }
                    const action = snapshot.pendingActions[0];
                    await renewSyncLease();

                    let result;
                    try {
                        result = await sendVisitStateRequest(action);
                    } catch {
                        setOfflineMode(true);
                        scheduleSynchronizationRetry();
                        break;
                    }

                    const { response, body } = result;
                    if (response.ok || response.status === 409) {
                        const canonicalPoint = body?.stampingPoint ?? null;
                        const canonicalState = body
                            ? normalizedVisitState(body)
                            : (response.ok ? action.desired : action.expected);
                        if (!await finishPendingAction(action, canonicalState, canonicalPoint)) {
                            scheduleSynchronizationRetry();
                            break;
                        }
                        synchronizedAny = true;
                        app.retryAttempt = 0;
                        continue;
                    }

                    if (response.status === 400) {
                        await finishPendingAction(action, action.expected);
                        setMapStatus("Eine vorgemerkte Stempeländerung war ungültig und wurde verworfen.", "error");
                        continue;
                    }

                    if (response.status === 401) {
                        await clearPersonalData();
                        setSession({ authenticated: false });
                        resetPointCache();
                        clearMarkers();
                        showAuthBarrier();
                        break;
                    }

                    if (response.status === 403) {
                        await finishPendingAction(action, action.expected);
                        shouldReload = true;
                        continue;
                    }

                    if (response.status === 404) {
                        await finishPendingAction(action, action.expected, null);
                        shouldReload = true;
                        continue;
                    }

                    if (response.status >= 500) {
                        scheduleSynchronizationRetry();
                        break;
                    }

                    scheduleSynchronizationRetry();
                    break;
                }
            } finally {
                await releaseSyncLease();
            }

            if (shouldReload && app.authenticated && !app.isOffline) {
                window.queueMicrotask(() => initialize());
            }
        })().finally(() => {
            app.syncPromise = null;
        });
        return app.syncPromise;
    };

    const queueVisitAction = async (feature, desired, utcOffsetMinutes) => {
        const point = feature?.stampingPoint;
        if (!point || hasPendingAction(point.id)) return false;
        const action = {
            pointId: point.id,
            providerSlug: point.provider.slug,
            countsTowardProgress: point.countsTowardProgress === true,
            expected: visitStateFromPoint(point),
            desired: normalizedVisitState(desired),
            utcOffsetMinutes,
            createdAt: new Date().toISOString()
        };

        const result = await updateStoredSnapshot(snapshot => {
            if (!isSnapshotValid(snapshot) ||
                snapshot.email.toLocaleLowerCase() !== app.sessionEmail?.toLocaleLowerCase() ||
                snapshot.pendingActions.some(pending => pending.pointId === point.id)) {
                return snapshot;
            }
            return {
                ...replacePointInResponses(snapshot, point.id, action.desired),
                pendingActions: [...snapshot.pendingActions, action]
            };
        });

        if (!result.ok || !result.snapshot ||
            !result.snapshot.pendingActions.some(pending => pending.createdAt === action.createdAt)) {
            if (isSnapshotValid(result.snapshot)) {
                setPendingActions(result.snapshot.pendingActions);
                await refreshFromStoredSnapshot();
            }
            setVisitActionStatus("Die Änderung konnte nicht sicher auf diesem Gerät gespeichert werden.", "error");
            return false;
        }

        setPendingActions(result.snapshot.pendingActions);
        await updatePointVisit(
            feature,
            action.desired.isVisited,
            action.desired.visitedOn,
            action.desired.visitedAt);
        renderProgressOverview();
        setVisitActionStatus("Änderung wurde zur Synchronisierung vorgemerkt.", "ready");
        broadcastSyncEvent("snapshot-updated");
        if (!app.isOffline) synchronizePendingActions();
        return true;
    };

    const setVisitActionStatus = (message, state) => {
        elements.visitActionStatus.textContent = message;
        elements.visitActionStatus.dataset.state = state ?? "ready";
    };

    const visitEditableControls = () => [
        elements.visitNowButton,
        elements.openVisitFormButton,
        elements.editVisitButton,
        elements.deleteVisitButton,
        elements.visitedOnInput,
        elements.visitedAtInput,
        elements.saveVisitButton,
        elements.cancelVisitButton,
        elements.confirmDeleteVisitButton,
        elements.cancelDeleteVisitButton,
        elements.closeDeleteVisitButton
    ];

    const setVisitControlsDisabled = disabled => {
        for (const control of visitEditableControls()) {
            control.disabled = disabled;
        }
    };

    const setVisitBusy = busy => {
        const pending = app.activeFeature && hasPendingAction(app.activeFeature.stampingPoint.id);
        setVisitControlsDisabled(busy || pending);
        if (!busy && !pending) {
            elements.visitedAtInput.disabled = !elements.visitedOnInput.value;
        }
    };

    const toLocalDateInput = date => [
        date.getFullYear(),
        String(date.getMonth() + 1).padStart(2, "0"),
        String(date.getDate()).padStart(2, "0")
    ].join("-");

    const toLocalTimeInput = date => [
        String(date.getHours()).padStart(2, "0"),
        String(date.getMinutes()).padStart(2, "0")
    ].join(":");

    const closeVisitForm = (restoreFocus = false) => {
        elements.visitForm.hidden = true;
        const visited = app.activeFeature?.visitState === VisitState.visited;
        elements.newVisitActions.hidden = visited;
        elements.existingVisitActions.hidden = !visited;
        if (restoreFocus) {
            (visited ? elements.editVisitButton : elements.openVisitFormButton).focus({ preventScroll: true });
        }
    };

    const openVisitForm = () => {
        const stampingPoint = app.activeFeature?.stampingPoint;
        if (!stampingPoint || hasPendingAction(stampingPoint.id)) {
            return;
        }
        elements.visitedOnInput.max = toLocalDateInput(new Date());
        elements.visitedOnInput.value = stampingPoint.isVisited && stampingPoint.visitedOn
            ? stampingPoint.visitedOn
            : "";
        elements.visitedAtInput.value = stampingPoint.isVisited && stampingPoint.visitedAt
            ? stampingPoint.visitedAt.slice(0, 5)
            : "";
        elements.visitedAtInput.disabled = !elements.visitedOnInput.value;
        elements.newVisitActions.hidden = true;
        elements.existingVisitActions.hidden = true;
        elements.visitForm.hidden = false;
        setVisitActionStatus("");
        elements.visitedOnInput.focus({ preventScroll: true });
    };

    const saveVisit = async (visitedOn, visitedAt, utcOffsetMinutes = -new Date().getTimezoneOffset()) => {
        const feature = app.activeFeature;
        if (!feature || hasPendingAction(feature.stampingPoint.id)) {
            return;
        }
        setVisitBusy(true);
        setVisitActionStatus("Änderung wird auf diesem Gerät gespeichert …");
        try {
            await queueVisitAction(feature, { isVisited: true, visitedOn, visitedAt }, utcOffsetMinutes);
        } catch {
            setVisitActionStatus("Die Änderung konnte nicht sicher auf diesem Gerät gespeichert werden.", "error");
        } finally {
            setVisitBusy(false);
        }
    };

    const updateVisitControls = (feature, locked) => {
        const visited = feature.visitState === VisitState.visited;
        elements.visitControls.hidden = !locked;
        elements.visitLoginLink.hidden = !locked || app.authenticated;
        if (elements.offlineNotice) {
            elements.offlineNotice.hidden = !locked || !app.isOffline;
        }
        const pending = hasPendingAction(feature.stampingPoint.id);
        if (elements.pendingVisitIndicator) {
            elements.pendingVisitIndicator.hidden = !locked || !pending;
        }
        elements.infoCard.classList.toggle("info-card--pending", locked && pending);
        setVisitControlsDisabled(pending);

        elements.newVisitActions.hidden = !locked || !app.authenticated || visited;
        elements.existingVisitActions.hidden = !locked || !app.authenticated || !visited;
        elements.visitForm.hidden = true;
        setVisitActionStatus("");
    };

    const populateTours = (tours) => {
        elements.pointTours.replaceChildren();
        const list = document.createElement("ul");
        list.className = "tour-list";
        const names = Array.isArray(tours) && tours.length > 0
            ? tours.map(tour => tour.name)
            : ["Einzelstempel"];
        for (const name of names) {
            const item = document.createElement("li");
            item.textContent = name;
            list.appendChild(item);
        }
        elements.pointTours.appendChild(list);
    };

    const positionInfo = (pixel) => {
        if (!finePointer.matches) {
            elements.infoCard.style.removeProperty("left");
            elements.infoCard.style.removeProperty("top");
            return;
        }

        const margin = 16;
        const cardWidth = Math.min(elements.infoCard.offsetWidth, window.innerWidth - (margin * 2));
        const cardHeight = Math.min(elements.infoCard.offsetHeight, window.innerHeight - (margin * 2));
        const left = Math.max(margin, Math.min(pixel[0] + 12, window.innerWidth - cardWidth - margin));
        const top = Math.max(margin, Math.min(pixel[1] + 12, window.innerHeight - cardHeight - margin));
        elements.infoCard.style.left = `${left}px`;
        elements.infoCard.style.top = `${top}px`;
    };

    const showInfo = (feature, pixel, locked) => {
        const stampingPoint = feature.stampingPoint;
        const visitState = feature.visitState;
        elements.pointNumber.textContent = getPointNumberLabel(stampingPoint);
        elements.pointName.textContent = stampingPoint.name;
        elements.pointProvider.textContent = stampingPoint.provider?.name
            ? `Anbieter: ${stampingPoint.provider.name}${stampingPoint.series?.slug !== "standard" ? ` · ${stampingPoint.series.name}` : ""}`
            : "Anbieter nicht angegeben";
        elements.pointStatus.textContent = visitState === VisitState.visited
            ? "✓ Gestempelt"
            : visitState === VisitState.open
                ? "Noch nicht gestempelt"
                : "Stempelstatus nicht verfügbar";
        elements.pointStatus.dataset.state = visitState;
        populateTours(stampingPoint.tours);

        const formattedVisit = formatVisit(stampingPoint);
        elements.pointVisited.hidden = !formattedVisit;
        elements.pointVisited.textContent = formattedVisit?.text ?? "";
        elements.pointVisited.dateTime = formattedVisit?.dateTime ?? "";
        elements.pointShareControls.hidden = !locked;
        elements.pointShareStatus.textContent = "";
        delete elements.pointShareStatus.dataset.state;
        elements.copyPointLinkButton.disabled = !createPointLink(stampingPoint);

        elements.infoCard.hidden = false;
        app.activeFeature = feature;
        app.infoLocked = locked;
        app.infoPixel = pixel;
        updateVisitControls(feature, locked);
        positionInfo(pixel);
        if (locked) {
            elements.infoCard.focus({ preventScroll: true });
        }
    };

    const clusterFeatureAt = (pixel) => app.map.forEachFeatureAtPixel(
        pixel,
        feature => feature.get("features") ? feature : undefined,
        {
            hitTolerance: finePointer.matches ? 3 : 10,
            layerFilter: layer => layer === app.markerLayer
        }
    );

    const zoomToCluster = clusterFeature => {
        const features = clusterFeature.get("features") ?? [];
        if (features.length < 2) {
            return;
        }

        const view = app.map.getView();
        const currentZoom = view.getZoom() ?? 0;
        const maxZoom = view.getMaxZoom();
        if (currentZoom >= maxZoom) {
            return;
        }

        const extent = ol.extent.boundingExtent(features.map(feature =>
            feature.getGeometry().getCoordinates()));
        const targetMaxZoom = Math.min(maxZoom, currentZoom + 3);
        view.fit(extent, {
            duration: reducedMotion.matches ? 0 : 450,
            maxZoom: targetMaxZoom,
            padding: [80, 80, 80, 80]
        });
    };

    app.map.on("click", event => {
        const clusterFeature = clusterFeatureAt(event.pixel);
        const features = clusterFeature?.get("features") ?? [];
        if (features.length > 1) {
            zoomToCluster(clusterFeature);
        } else if (features.length === 1) {
            showInfo(features[0], event.pixel, true);
        } else {
            hideInfo(true);
        }
    });

    app.map.on("pointermove", event => {
        if (!finePointer.matches || app.infoLocked) {
            return;
        }
        const clusterFeature = clusterFeatureAt(event.pixel);
        const features = clusterFeature?.get("features") ?? [];
        elements.map.style.cursor = features.length > 0 ? "pointer" : "";
        if (features.length === 1) {
            showInfo(features[0], event.pixel, false);
        } else {
            hideInfo();
        }
    });

    app.map.on("moveend", () => {
        if (elements.infoCard.hidden || !app.activeFeature) {
            return;
        }
        const pixel = app.map.getPixelFromCoordinate(app.activeFeature.getGeometry().getCoordinates());
        app.infoPixel = pixel;
        positionInfo(pixel);
    });

    elements.closeInfoButton.addEventListener("click", () => {
        hideInfo(true);
        elements.map.focus({ preventScroll: true });
    });
    elements.copyPointLinkButton.addEventListener("click", copyActivePointLink);

    elements.visitNowButton.addEventListener("click", () => {
        const now = new Date();
        saveVisit(
            toLocalDateInput(now),
            `${toLocalTimeInput(now)}:00`,
            -now.getTimezoneOffset());
    });

    elements.openVisitFormButton.addEventListener("click", openVisitForm);
    elements.editVisitButton.addEventListener("click", openVisitForm);
    elements.cancelVisitButton.addEventListener("click", () => closeVisitForm(true));
    elements.visitedOnInput.addEventListener("input", () => {
        elements.visitedAtInput.disabled = !elements.visitedOnInput.value;
        if (!elements.visitedOnInput.value) {
            elements.visitedAtInput.value = "";
        }
    });
    elements.visitForm.addEventListener("submit", event => {
        event.preventDefault();
        const visitedOn = elements.visitedOnInput.value || null;
        const visitedAt = elements.visitedAtInput.value
            ? `${elements.visitedAtInput.value}:00`
            : null;
        if (visitedAt && !visitedOn) {
            setVisitActionStatus("Eine Uhrzeit benötigt auch ein Datum.", "error");
            elements.visitedOnInput.focus({ preventScroll: true });
            return;
        }
        if (visitedOn && visitedAt && new Date(`${visitedOn}T${visitedAt}`) > new Date(Date.now() + 300000)) {
            setVisitActionStatus("Ein Stempeldatum kann nicht in der Zukunft liegen.", "error");
            return;
        }
        saveVisit(visitedOn, visitedAt);
    });

    const closeDeleteVisitDialog = () => {
        if (elements.deleteVisitDialog.open) {
            elements.deleteVisitDialog.close();
        }
    };

    elements.deleteVisitButton.addEventListener("click", () => {
        const stampingPoint = app.activeFeature?.stampingPoint;
        if (!stampingPoint || hasPendingAction(stampingPoint.id)) {
            return;
        }
        const label = getPointNumberLabel(stampingPoint);
        elements.deleteVisitMessage.textContent = `Soll dein Stempeleintrag bei ${label} – ${stampingPoint.name} wirklich entfernt werden?`;
        elements.deleteVisitDialog.showModal();
        elements.cancelDeleteVisitButton.focus({ preventScroll: true });
    });
    elements.closeDeleteVisitButton.addEventListener("click", closeDeleteVisitDialog);
    elements.cancelDeleteVisitButton.addEventListener("click", closeDeleteVisitDialog);
    elements.confirmDeleteVisitButton.addEventListener("click", async () => {
        const feature = app.activeFeature;
        if (!feature) {
            closeDeleteVisitDialog();
            return;
        }
        setVisitBusy(true);
        try {
            await queueVisitAction(
                feature,
                { isVisited: false, visitedOn: null, visitedAt: null },
                -new Date().getTimezoneOffset());
            closeDeleteVisitDialog();
        } catch {
            closeDeleteVisitDialog();
            setVisitActionStatus("Die Änderung konnte nicht sicher auf diesem Gerät gespeichert werden.", "error");
        } finally {
            setVisitBusy(false);
        }
    });
    elements.deleteVisitDialog.addEventListener("cancel", event => {
        event.preventDefault();
        closeDeleteVisitDialog();
    });
    elements.deleteVisitDialog.addEventListener("close", () => {
        if (app.activeFeature?.visitState === VisitState.visited) {
            elements.deleteVisitButton.focus({ preventScroll: true });
        } else {
            elements.infoCard.focus({ preventScroll: true });
        }
    });
    elements.deleteVisitDialog.addEventListener("click", event => {
        if (event.target === elements.deleteVisitDialog) {
            closeDeleteVisitDialog();
        }
    });

    document.addEventListener("keydown", event => {
        if (event.key !== "Escape") {
            return;
        }
        if (elements.providerInfoDialog.open || elements.deleteVisitDialog.open) {
            return;
        }
        if (!elements.progressPanel.hidden) {
            closeProgressMenu(true);
        } else if (!elements.searchPanel.hidden) {
            closeSearchMenu(true);
        } else if (!elements.providerPanel.hidden) {
            closeProviderMenu(true);
        } else if (!elements.accountPanel.hidden) {
            closeAccountMenu(true);
        } else if (!elements.infoCard.hidden) {
            hideInfo(true);
            elements.map.focus({ preventScroll: true });
        }
    });

    elements.locateButton.addEventListener("click", () => {
        closeSearchMenu();
        closeProviderMenu();
        closeAccountMenu();
        closeProgressMenu();

        const coordinates = geolocation.getPosition();
        if (coordinates) {
            if (!geolocation.getTracking()) {
                geolocation.setTracking(true);
            }
            const view = app.map.getView();
            if (!reducedMotion.matches) {
                view.animate({
                    center: coordinates,
                    zoom: Math.max(view.getZoom() ?? 0, 14),
                    duration: 500
                });
            } else {
                view.setCenter(coordinates);
                view.setZoom(Math.max(view.getZoom() ?? 0, 14));
            }
        } else {
            app.centerOnNextPosition = true;
            setMapStatus("Standort wird ermittelt …");
            if (!geolocation.getTracking()) {
                geolocation.setTracking(true);
            }
        }
    });

    elements.visitFilterButton.addEventListener("click", cycleVisitFilter);
    elements.accountMenuButton.addEventListener("click", toggleAccountMenu);
    elements.providerMenuButton.addEventListener("click", toggleProviderMenu);
    elements.searchMenuButton.addEventListener("click", toggleSearchMenu);
    elements.progressButton.addEventListener("click", toggleProgressMenu);
    elements.closeProgressPanelButton.addEventListener("click", () => closeProgressMenu(true));
    elements.stampingPointSearchInput.addEventListener("input", renderSearchResults);
    elements.providerOptions.addEventListener("change", event => {
        if (event.target.matches('input[type="checkbox"]')) {
            applyProviderSelection();
        }
    });
    elements.selectAllProvidersButton.addEventListener("click", () => selectProviders(true));
    elements.selectNoProvidersButton.addEventListener("click", () => selectProviders(false));
    elements.closeProviderInfoButton.addEventListener("click", closeProviderInfo);
    elements.providerInfoDialog.addEventListener("cancel", event => {
        event.preventDefault();
        closeProviderInfo();
    });
    elements.providerInfoDialog.addEventListener("close", () => {
        const trigger = elements.providerInfoTrigger;
        elements.providerInfoTrigger = null;
        if (trigger?.isConnected) {
            trigger.focus({ preventScroll: true });
        } else if (app.authenticated && elements.progressButton.isConnected) {
            elements.progressButton.focus({ preventScroll: true });
        }
    });
    elements.providerInfoDialog.addEventListener("click", event => {
        if (event.target === elements.providerInfoDialog) {
            closeProviderInfo();
        }
    });

    document.addEventListener("pointerdown", event => {
        if (elements.providerInfoDialog.open || elements.deleteVisitDialog.open || elements.userSession.contains(event.target) || elements.progressOverview.contains(event.target)) {
            return;
        }
        if (!elements.progressPanel.hidden) {
            closeProgressMenu();
        } else if (!elements.searchPanel.hidden) {
            closeSearchMenu();
        } else if (!elements.providerPanel.hidden) {
            closeProviderMenu();
        } else if (!elements.accountPanel.hidden) {
            closeAccountMenu();
        }
    });

    const refreshInfoPosition = () => {
        if (!elements.infoCard.hidden && app.infoPixel) {
            positionInfo(app.infoPixel);
        }
    };

    finePointer.addEventListener("change", refreshInfoPosition);
    window.addEventListener("resize", refreshInfoPosition);

    const initialize = async () => {
        const generation = ++app.loadGeneration;
        const activePointId = app.activeFeature?.stampingPoint.id ?? null;
        const activePixel = app.infoPixel;
        const activeLocked = app.infoLocked;
        const registrationParam = app.pendingRegistration;
        app.pendingRegistration = null;

        hideInfo(true);
        clearMarkers();
        resetPointCache();
        app.visitFilter = VisitFilter.all;
        updateVisitFilterButton();
        elements.stampingPointSearchInput.value = "";
        renderSearchResults();

        const registrationDecisionVisible = registrationParam === "pending" || registrationParam === "rejected";
        elements.authBarrierLoginButton.hidden = true;
        if (elements.authBarrierLoading) elements.authBarrierLoading.hidden = registrationDecisionVisible;
        elements.authBarrierDesc.hidden = registrationDecisionVisible;
        if (registrationParam === "pending" && elements.authBarrierNotice) {
            elements.authBarrierNotice.className = "auth-barrier__notice";
            elements.authBarrierNotice.textContent = "";
            const strong = document.createElement("strong");
            strong.textContent = "Registrierungsantrag eingegangen";
            elements.authBarrierNotice.appendChild(strong);
            elements.authBarrierNotice.appendChild(document.createTextNode(
                "Dein Antrag wurde erfasst und wartet auf administrative Freischaltung. Sobald dein Zugang freigeschaltet wurde, kannst du dich mit Google anmelden."
            ));
            elements.authBarrierNotice.hidden = false;
        } else if (registrationParam === "rejected" && elements.authBarrierNotice) {
            elements.authBarrierNotice.className = "auth-barrier__notice auth-barrier__notice--rejected";
            elements.authBarrierNotice.textContent = "";
            const strong = document.createElement("strong");
            strong.textContent = "Registrierungsantrag abgelehnt";
            elements.authBarrierNotice.appendChild(strong);
            elements.authBarrierNotice.appendChild(document.createTextNode(
                "Dein Registrierungsantrag wurde abgelehnt. Solange diese Entscheidung gespeichert ist, ist keine erneute Registrierung möglich."
            ));
            elements.authBarrierNotice.hidden = false;
        }

        setOfflineMode(false);

        let session = null;
        let isNetworkError = false;
        let isHttpError = false;

        try {
            session = await getJson("auth/session");
        } catch (error) {
            if (error instanceof Response) {
                isHttpError = true;
            } else {
                isNetworkError = true;
            }
        }

        if (generation !== app.loadGeneration) {
            return;
        }

        if (isNetworkError) {
            const snapshot = await getStoredSnapshot();
            if (generation !== app.loadGeneration) {
                return;
            }

            if (isSnapshotValid(snapshot)) {
                setPendingActions(snapshot.pendingActions);
                setSession({
                    authenticated: true,
                    email: snapshot.email,
                    expiresAt: snapshot.expiresAt
                });
                setOfflineMode(true);
                if (elements.authBarrierNotice) {
                    elements.authBarrierNotice.hidden = true;
                }
                hideAuthBarrier();
                const pointCount = loadSnapshotData(snapshot);
                setMapStatus(`${formatPointCount(pointCount)} offline geladen.`, "ready");
                window.setTimeout(() => {
                    if (generation === app.loadGeneration && elements.mapStatus.dataset.state === "ready") {
                        setMapStatus("");
                    }
                }, 1800);
                if (!openPendingPointLink()) {
                    restoreLockedInfo(activePointId, activePixel, activeLocked);
                }
                return;
            }

            await showOfflineUnavailable(
                "Es ist kein gültiger gespeicherter Datenstand vorhanden oder deine Sitzung ist abgelaufen. Bitte verbinde dich mit dem Internet und melde dich an.");
            return;
        }

        if (isHttpError) {
            if (elements.authBarrierLoading) elements.authBarrierLoading.hidden = true;
            setSession({ authenticated: false });
            showAuthBarrier();
            setMapStatus("Sitzungsprüfung fehlgeschlagen. Bitte versuche es erneut.", "error");
            return;
        }

        if (!session || !session.authenticated) {
            await clearPersonalData();
            setSession({ authenticated: false });
            elements.authBarrierLoginButton.hidden = false;
            if (elements.authBarrierLoading) elements.authBarrierLoading.hidden = true;
            showAuthBarrier();
            setMapStatus("");
            return;
        }

        setSession(session);

        const existingSnapshot = await getStoredSnapshot();
        if (existingSnapshot &&
            existingSnapshot.email &&
            existingSnapshot.email.toLocaleLowerCase() !== session.email.toLocaleLowerCase()) {
            await clearPersonalData();
        } else if (isSnapshotValid(existingSnapshot)) {
            setPendingActions(existingSnapshot.pendingActions);
            if (existingSnapshot.pendingActions.length > 0) {
                await synchronizePendingActions(session);
            }
        }

        if (generation !== app.loadGeneration) return;

        if (app.isOffline) {
            const offlineSnapshot = await getStoredSnapshot();
            if (isSnapshotValid(offlineSnapshot)) {
                hideAuthBarrier();
                const pointCount = loadSnapshotData(offlineSnapshot);
                setMapStatus(`${formatPointCount(pointCount)} offline geladen.`, "ready");
                if (!openPendingPointLink()) {
                    restoreLockedInfo(activePointId, activePixel, activeLocked);
                }
                return;
            }
        }

        if (elements.authBarrierNotice) {
            elements.authBarrierNotice.hidden = true;
        }
        hideAuthBarrier();
        setMapStatus("Karte wird geladen …", "loading");

        try {
            const providers = await getJson("api/providers");
            if (generation !== app.loadGeneration) {
                return;
            }
            setProviderCatalog(providers);

            const [serverUnvisited, serverVisited] = await Promise.all([
                getJson("api/points?provider=all&vis=false"),
                getJson("api/points?provider=all&vis=true")
            ]);
            if (generation !== app.loadGeneration) {
                return;
            }

            const currentSnapshot = await getStoredSnapshot();
            const pendingActions = isSnapshotValid(currentSnapshot)
                ? currentSnapshot.pendingActions
                : [];
            setPendingActions(pendingActions);
            const { unvisited, visited } = overlayPendingActions(
                serverUnvisited,
                serverVisited,
                pendingActions);
            cachePoints(unvisited, VisitState.open);
            cachePoints(visited, VisitState.visited);
            const pointCount = renderSelectedPoints();
            renderSearchResults();

            if (session.expiresAt) {
                const stored = await updateStoredSnapshot(snapshot => ({
                    ...(snapshot ?? {}),
                    schemaVersion: SNAPSHOT_SCHEMA_VERSION,
                    email: session.email,
                    expiresAt: session.expiresAt,
                    providers,
                    unvisitedPoints: unvisited,
                    visitedPoints: visited,
                    pendingActions,
                    savedAt: new Date().toISOString()
                }));
                if (!stored.ok) {
                    setMapStatus("Der Offline-Datenstand konnte nicht sicher gespeichert werden.", "error");
                }
            }

            if (app.backOnlineNoticePending) {
                app.backOnlineNoticePending = false;
                showBackOnlineNotice();
            } else {
                setMapStatus(`${formatPointCount(pointCount)} geladen.`, "ready");
                window.setTimeout(() => {
                    if (generation === app.loadGeneration && elements.mapStatus.dataset.state === "ready") {
                        setMapStatus("");
                    }
                }, 1800);
            }
            if (!openPendingPointLink()) {
                restoreLockedInfo(activePointId, activePixel, activeLocked);
            }
        } catch (error) {
            if (generation === app.loadGeneration) {
                if (error?.status === 401) {
                    await clearPersonalData();
                    setSession({ authenticated: false });
                    resetPointCache();
                    clearMarkers();
                    showAuthBarrier();
                    setMapStatus("");
                } else if (!(error instanceof Response)) {
                    const fallbackSnapshot = await getStoredSnapshot();
                    if (isSnapshotValid(fallbackSnapshot)) {
                        setOfflineMode(true);
                        const pointCount = loadSnapshotData(fallbackSnapshot);
                        restoreLockedInfo(activePointId, activePixel, activeLocked);
                        setMapStatus(`${formatPointCount(pointCount)} offline geladen.`, "ready");
                    } else {
                        setMapStatus("Anbieter und Stempelstellen konnten nicht geladen werden.", "error");
                    }
                } else {
                    setMapStatus("Anbieter und Stempelstellen konnten nicht geladen werden.", "error");
                }
            }
        }
    };

    elements.logoutButton.addEventListener("click", async () => {
        if (app.pendingActions.size > 0 &&
            !window.confirm("Nicht synchronisierte Stempeländerungen gehen beim Abmelden verloren. Trotzdem abmelden?")) {
            return;
        }
        elements.logoutButton.disabled = true;
        await clearPersonalData();
        ++app.loadGeneration;
        hideInfo(true);
        resetPointCache();
        clearMarkers();
        setSession({ authenticated: false });
        closeAccountMenu();
        showAuthBarrier();
        try {
            const response = await fetch("auth/logout", { method: "POST" });
            if (!response.ok && response.status !== 401) {
                setMapStatus("Abmelden ist fehlgeschlagen. Bitte erneut versuchen.", "error");
                return;
            }
            await initialize();
        } catch {
            await showOfflineUnavailable(
                "Deine lokalen Daten wurden gelöscht. Die serverseitige Abmeldung konnte ohne Verbindung nicht bestätigt werden.");
        } finally {
            elements.logoutButton.disabled = false;
        }
    });

    window.addEventListener("offline", () => {
        if (!app.authenticated || app.isOffline) {
            return;
        }

        if (!app.sessionExpiresAt || Date.parse(app.sessionExpiresAt) <= Date.now()) {
            showOfflineUnavailable(
                "Deine gespeicherte Sitzung ist abgelaufen. Bitte verbinde dich mit dem Internet und melde dich erneut an.");
            return;
        }

        setOfflineMode(true);
        setMapStatus("Offline: Gespeicherter Datenstand wird angezeigt.", "ready");
    });

    window.addEventListener("online", () => {
        clearRetryTimer();
        initialize();
    });

    syncChannel?.addEventListener("message", async event => {
        if (!event.data || event.data.sender === TAB_ID) return;
        if (event.data.type === "personal-data-cleared") {
            ++app.loadGeneration;
            clearRetryTimer();
            setPendingActions([]);
            resetProviderCatalog();
            hideInfo(true);
            resetPointCache();
            clearMarkers();
            setSession({ authenticated: false });
            showAuthBarrier();
            return;
        }
        if (event.data.type === "snapshot-updated") {
            if (!elements.visitForm.hidden || elements.deleteVisitDialog.open) return;
            await refreshFromStoredSnapshot();
        }
    });

    window.addEventListener("focus", () => {
        if (!elements.visitForm.hidden || elements.deleteVisitDialog.open) return;
        refreshFromStoredSnapshot();
    });
    if (!syncChannel) {
        window.setInterval(() => {
            if (app.authenticated && elements.visitForm.hidden && !elements.deleteVisitDialog.open) {
                refreshFromStoredSnapshot();
            }
        }, 2000);
    }

    initialize();
})();
