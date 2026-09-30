// Shared mutable page state and media queries.
import { VisitFilter, VisitState } from "./constants.js";
import { initialNavigation } from "./navigation.js";

const finePointer = window.matchMedia("(hover: hover) and (pointer: fine)");
const reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)");

const app = {
    // Set by map.js when the map is created: map, markerLayer, markerSource, userLocationLayer.
    map: null,
    markerLayer: null,
    markerSource: null,
    userLocationLayer: null,
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
    pointCache: {
        [VisitState.unknown]: [],
        [VisitState.open]: [],
        [VisitState.visited]: []
    },
    providers: [],
    selectedProviderSlugs: new Set(),
    backOnlineNoticePending: false,
    retryAttempt: 0,
    retryTimer: null,
    syncPromise: null,
    visitFilter: VisitFilter.all
};

export {
    app,
    finePointer,
    reducedMotion
};
