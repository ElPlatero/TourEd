// Session state, the login barrier, session expiry and offline notices.
import { elements } from "./dom.js";
import { initialize } from "./initialize.js";
import { clearMarkers, setMapStatus } from "./map.js";
import { hideInfo } from "./point-details.js";
import { resetPointCache } from "./points.js";
import { app } from "./state.js";
import { clearPersonalData } from "./visit-sync.js";

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

const showBackOnlineNotice = () => {
    const message = "TourEd ist wieder online.";
    setMapStatus(message, "ready");
    window.setTimeout(() => {
        if (elements.mapStatus.textContent === message) setMapStatus("");
    }, 3000);
};

export {
    hideAuthBarrier,
    setOfflineMode,
    setSession,
    showAuthBarrier,
    showBackOnlineNotice,
    showOfflineUnavailable
};
