// Offline snapshot persistence in IndexedDB. All access is serialized through
// queueSnapshotOperation so reads, migrations and writes never interleave.
const DB_NAME = "toured-db";
const DB_VERSION = 1;
const STORE_NAME = "snapshots";
const SNAPSHOT_KEY = "current";
const SNAPSHOT_REVISION_KEY = "revision";
const SNAPSHOT_SCHEMA_VERSION = 3;

let snapshotOperations = Promise.resolve();

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

const readStoredSnapshot = async (key = SNAPSHOT_KEY) => {
    const db = await openDatabase();
    if (!db) return null;
    return new Promise(resolve => {
        try {
            const tx = db.transaction(STORE_NAME, "readonly");
            const store = tx.objectStore(STORE_NAME);
            const request = store.get(key);
            request.onsuccess = () => resolve(request.result || null);
            request.onerror = () => resolve(null);
        } catch {
            resolve(null);
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
            const revisionRequest = store.get(SNAPSHOT_REVISION_KEY);
            const request = store.get(SNAPSHOT_KEY);
            let nextSnapshot = null;
            request.onsuccess = () => {
                const snapshot = request.result || null;
                nextSnapshot = mutation(snapshot, revisionRequest.result?.value ?? null);
                if (nextSnapshot && nextSnapshot !== snapshot) {
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
            // Keep only an anonymous invalidation token after personal data is purged.
            store.put({ key: SNAPSHOT_REVISION_KEY, value: crypto.randomUUID() });
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

// Migrations must also use the latest snapshot inside a single transaction.
const getStoredSnapshot = () => queueSnapshotOperation(async () => {
    const result = await mutateStoredSnapshot(normalizeSnapshot);
    return result.ok ? result.snapshot : null;
});

const getSnapshotRevision = () => queueSnapshotOperation(async () =>
    (await readStoredSnapshot(SNAPSHOT_REVISION_KEY))?.value ?? null);

const updateStoredSnapshot = mutation => queueSnapshotOperation(
    () => mutateStoredSnapshot((snapshot, revision) => mutation(normalizeSnapshot(snapshot), revision)));

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
    (action.actionId === undefined || (typeof action.actionId === "string" && action.actionId.length > 0)) &&
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

export {
    SNAPSHOT_SCHEMA_VERSION,
    clearStoredSnapshot,
    getSnapshotRevision,
    getStoredSnapshot,
    isSnapshotValid,
    updateStoredSnapshot
};
