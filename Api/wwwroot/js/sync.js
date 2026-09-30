// Offline visit synchronization state: operation ids, cross-tab notifications, pending-action overlays
// and the IndexedDB lease protocol that lets exactly one tab send queued actions at a time.
import { isSnapshotValid, updateStoredSnapshot } from "./snapshot-store.js";
import { app } from "./state.js";

const SYNC_LEASE_MILLISECONDS = 30000;

const RETRY_MAX_MILLISECONDS = 60000;

const createOperationId = () => crypto.randomUUID?.() ?? `${Date.now()}-${Math.random()}`;

const TAB_ID = createOperationId();

const syncChannel = "BroadcastChannel" in window
    ? new BroadcastChannel("toured-offline-sync")
    : null;

const broadcastSyncEvent = type => syncChannel?.postMessage({ type, sender: TAB_ID });

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

const clearRetryTimer = () => {
    if (app.retryTimer !== null) {
        window.clearTimeout(app.retryTimer);
        app.retryTimer = null;
    }
};

const isSyncSessionCurrent = sync => app.authenticated &&
    app.loadGeneration === sync.generation &&
    app.sessionEmail?.toLocaleLowerCase() === sync.email;

const hasSyncLease = (snapshot, sync) => isSyncSessionCurrent(sync) &&
    isSnapshotValid(snapshot) && snapshot.email.toLocaleLowerCase() === sync.email &&
    snapshot.syncLease?.owner === TAB_ID && snapshot.syncLease.token === sync.token &&
    Date.parse(snapshot.syncLease.expiresAt) > Date.now();

const acquireSyncLease = async sync => {
    let acquired = false;
    const result = await updateStoredSnapshot(snapshot => {
        if (!isSyncSessionCurrent(sync) || !isSnapshotValid(snapshot) ||
            snapshot.email.toLocaleLowerCase() !== sync.email) return snapshot;
        const lease = snapshot.syncLease;
        if (lease && lease.owner !== TAB_ID && Date.parse(lease.expiresAt) > Date.now()) {
            return snapshot;
        }
        acquired = true;
        return {
            ...snapshot,
            // Older schema-2/3 queues lack action IDs. Assign them once, atomically,
            // before any sender reads the queue; timestamps are not identities.
            pendingActions: snapshot.pendingActions.map(action => action.actionId
                ? action : { ...action, actionId: createOperationId() }),
            syncLease: {
                owner: TAB_ID,
                token: sync.token,
                expiresAt: new Date(Date.now() + SYNC_LEASE_MILLISECONDS).toISOString()
            }
        };
    });
    return result.ok && acquired;
};

const renewSyncLease = async sync => {
    let renewed = false;
    const result = await updateStoredSnapshot(snapshot => {
        if (!hasSyncLease(snapshot, sync)) return snapshot;
        renewed = true;
        return {
            ...snapshot,
            syncLease: {
                ...snapshot.syncLease,
                expiresAt: new Date(Date.now() + SYNC_LEASE_MILLISECONDS).toISOString()
            }
        };
    });
    return result.ok && renewed;
};

const releaseSyncLease = sync => updateStoredSnapshot(snapshot => {
    if (!snapshot || snapshot.email?.toLocaleLowerCase() !== sync.email ||
        snapshot.syncLease?.owner !== TAB_ID || snapshot.syncLease.token !== sync.token) return snapshot;
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

export {
    RETRY_MAX_MILLISECONDS,
    TAB_ID,
    acquireSyncLease,
    broadcastSyncEvent,
    clearRetryTimer,
    createOperationId,
    hasPendingAction,
    hasSyncLease,
    isSyncSessionCurrent,
    normalizedVisitState,
    overlayPendingActions,
    releaseSyncLease,
    renewSyncLease,
    replacePointInResponses,
    setPendingActions,
    syncChannel,
    updateProviderProgressInSnapshot,
    visitStateFromPoint
};
