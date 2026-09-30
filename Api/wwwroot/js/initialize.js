// Page initialization: session check, provider catalog and points (online or from the offline
// snapshot) and the start of queued-action synchronization.
import { getJson } from "./api.js";
import { VisitFilter } from "./constants.js";
import { elements } from "./dom.js";
import { clearMarkers, setMapStatus } from "./map.js";
import { hideInfo, restoreLockedInfo } from "./point-details.js";
import {
    formatPointCount,
    openPendingPointLink,
    renderSearchResults,
    resetPointCache,
    updateVisitFilterButton
} from "./points.js";
import { setProviderCatalog } from "./providers.js";
import {
    hideAuthBarrier,
    setOfflineMode,
    setSession,
    showAuthBarrier,
    showBackOnlineNotice,
    showOfflineUnavailable
} from "./session.js";
import {
    SNAPSHOT_SCHEMA_VERSION,
    getSnapshotRevision,
    getStoredSnapshot,
    isSnapshotValid,
    updateStoredSnapshot
} from "./snapshot-store.js";
import { app } from "./state.js";
import { overlayPendingActions, setPendingActions } from "./sync.js";
import {
    clearPersonalData,
    loadSnapshotData,
    scheduleSynchronizationRetry,
    synchronizePendingActions
} from "./visit-sync.js";

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

    let snapshotRevision = await getSnapshotRevision();
    if (generation !== app.loadGeneration) return;

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
    if (generation !== app.loadGeneration) return;
    if (existingSnapshot &&
        existingSnapshot.email &&
        existingSnapshot.email.toLocaleLowerCase() !== session.email.toLocaleLowerCase()) {
        await clearPersonalData();
        snapshotRevision = await getSnapshotRevision();
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

        let initializedSnapshot = {
            schemaVersion: SNAPSHOT_SCHEMA_VERSION,
            email: session.email,
            expiresAt: session.expiresAt,
            providers,
            unvisitedPoints: serverUnvisited,
            visitedPoints: serverVisited,
            pendingActions: [],
            savedAt: new Date().toISOString()
        };
        let storageFailed = false;
        if (session.expiresAt) {
            const stored = await updateStoredSnapshot((snapshot, revision) => {
                if (generation !== app.loadGeneration || !app.authenticated ||
                    app.sessionEmail?.toLocaleLowerCase() !== session.email.toLocaleLowerCase() ||
                    !(Date.parse(session.expiresAt) > Date.now()) ||
                    revision !== snapshotRevision ||
                    (snapshot && snapshot.email?.toLocaleLowerCase() !== session.email.toLocaleLowerCase())) {
                    return null;
                }
                // Never merge actions read before this transaction: another tab may
                // have queued or completed them while the server responses were loading.
                const pendingActions = isSnapshotValid(snapshot) ? snapshot.pendingActions : [];
                const { unvisited, visited } = overlayPendingActions(
                    serverUnvisited, serverVisited, pendingActions);
                return {
                    ...snapshot,
                    ...initializedSnapshot,
                    unvisitedPoints: unvisited,
                    visitedPoints: visited,
                    pendingActions
                };
            });
            if (generation !== app.loadGeneration) return;
            if (stored.ok && !stored.snapshot) {
                setSession({ authenticated: false });
                resetPointCache();
                clearMarkers();
                showAuthBarrier();
                return;
            }
            storageFailed = !stored.ok;
            if (stored.snapshot) initializedSnapshot = stored.snapshot;
        }
        if (generation !== app.loadGeneration) return;
        const pointCount = loadSnapshotData(initializedSnapshot);

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
        if (storageFailed) {
            setMapStatus("Der Offline-Datenstand konnte nicht sicher gespeichert werden.", "error");
        }
        if (!openPendingPointLink()) {
            restoreLockedInfo(activePointId, activePixel, activeLocked);
        }
        if (app.pendingActions.size > 0) scheduleSynchronizationRetry();
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

export {
    initialize
};
