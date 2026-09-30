// Orchestrates queued visit actions: enqueueing, sending under the sync lease, confirmations,
// retries and reloading the page state from the offline snapshot.
import { getJson, sendVisitStateRequest } from "./api.js";
import { VisitState } from "./constants.js";
import { initialize } from "./initialize.js";
import { clearMarkers, setMapStatus } from "./map.js";
import { restoreLockedInfo, setVisitActionStatus, updatePointVisit } from "./point-details.js";
import { cachePoints, renderSearchResults, renderSelectedPoints, resetPointCache } from "./points.js";
import { renderProgressOverview, resetProviderCatalog, setProviderCatalog } from "./providers.js";
import { setOfflineMode, setSession, showAuthBarrier } from "./session.js";
import {
    clearStoredSnapshot,
    getStoredSnapshot,
    isSnapshotValid,
    updateStoredSnapshot
} from "./snapshot-store.js";
import { app } from "./state.js";
import {
    RETRY_MAX_MILLISECONDS,
    acquireSyncLease,
    broadcastSyncEvent,
    clearRetryTimer,
    createOperationId,
    hasPendingAction,
    hasSyncLease,
    isSyncSessionCurrent,
    normalizedVisitState,
    releaseSyncLease,
    renewSyncLease,
    replacePointInResponses,
    setPendingActions,
    updateProviderProgressInSnapshot,
    visitStateFromPoint
} from "./sync.js";

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

const finishPendingAction = async (sync, action, state, canonicalPoint = null) => {
    let finished = false;
    const result = await updateStoredSnapshot(snapshot => {
        if (!hasSyncLease(snapshot, sync) || !action.actionId) return snapshot;
        const pendingAction = snapshot.pendingActions.find(pending =>
            pending.actionId === action.actionId && pending.pointId === action.pointId &&
            pending.providerSlug === action.providerSlug);
        if (!pendingAction) return snapshot;
        finished = true;
        const pendingActions = snapshot.pendingActions.filter(pending => pending !== pendingAction);
        const updatedSnapshot = updateProviderProgressInSnapshot(
            snapshot,
            pendingAction,
            state,
            canonicalPoint);
        return {
            ...replacePointInResponses(updatedSnapshot, action.pointId, state, canonicalPoint),
            pendingActions
        };
    });
    if (!result.ok || !finished || !isSyncSessionCurrent(sync)) return false;
    setPendingActions(result.snapshot.pendingActions);
    await refreshFromStoredSnapshot();
    renderProgressOverview();
    broadcastSyncEvent("snapshot-updated");
    return true;
};

const synchronizePendingActions = (knownSession = null) => {
    clearRetryTimer();
    if (app.syncPromise) return app.syncPromise;

    const generation = app.loadGeneration;
    app.syncPromise = (async () => {
        let snapshot = await getStoredSnapshot();
        if (generation !== app.loadGeneration) return;
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

        if (generation !== app.loadGeneration) return;
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
        const sync = { token: createOperationId(), email: session.email.toLocaleLowerCase(), generation };
        await updateStoredSnapshot(current => isSyncSessionCurrent(sync) &&
            current?.email?.toLocaleLowerCase() === sync.email ? {
                ...current,
                expiresAt: session.expiresAt ?? current.expiresAt
            } : current);

        if (!await acquireSyncLease(sync)) {
            scheduleSynchronizationRetry();
            return;
        }

        let shouldReload = false;
        let synchronizedAny = false;
        try {
            while (true) {
                snapshot = await getStoredSnapshot();
                if (!isSyncSessionCurrent(sync)) break;
                if (!isSnapshotValid(snapshot) || snapshot.pendingActions.length === 0) {
                    app.retryAttempt = 0;
                    if (synchronizedAny) app.backOnlineNoticePending = true;
                    break;
                }
                const action = snapshot.pendingActions[0];
                if (!await renewSyncLease(sync)) {
                    scheduleSynchronizationRetry();
                    break;
                }

                let result;
                try {
                    result = await sendVisitStateRequest(action);
                } catch {
                    if (await renewSyncLease(sync)) setOfflineMode(true);
                    scheduleSynchronizationRetry();
                    break;
                }

                // A slow response may outlive this lease or initialization. Never
                // let it apply a result (including an error) to a newer owner/account.
                if (!await renewSyncLease(sync)) {
                    scheduleSynchronizationRetry();
                    break;
                }
                const { response, body } = result;
                if (response.ok || response.status === 409) {
                    const canonicalPoint = body?.stampingPoint ?? null;
                    const canonicalState = body
                        ? normalizedVisitState(body)
                        : (response.ok ? action.desired : action.expected);
                    if (!await finishPendingAction(sync, action, canonicalState, canonicalPoint)) {
                        scheduleSynchronizationRetry();
                        break;
                    }
                    synchronizedAny = true;
                    app.retryAttempt = 0;
                    continue;
                }

                if (response.status === 400) {
                    if (!await finishPendingAction(sync, action, action.expected)) {
                        scheduleSynchronizationRetry();
                        break;
                    }
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
                    if (!await finishPendingAction(sync, action, action.expected)) {
                        scheduleSynchronizationRetry();
                        break;
                    }
                    shouldReload = true;
                    continue;
                }

                if (response.status === 404) {
                    if (!await finishPendingAction(sync, action, action.expected, null)) {
                        scheduleSynchronizationRetry();
                        break;
                    }
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
            await releaseSyncLease(sync);
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
        actionId: createOperationId(),
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
        !result.snapshot.pendingActions.some(pending => pending.actionId === action.actionId)) {
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

export {
    clearPersonalData,
    loadSnapshotData,
    queueVisitAction,
    refreshFromStoredSnapshot,
    scheduleSynchronizationRetry,
    synchronizePendingActions
};
