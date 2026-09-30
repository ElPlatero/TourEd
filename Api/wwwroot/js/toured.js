import { VisitState } from "./constants.js";
import { elements } from "./dom.js";
import { initialize } from "./initialize.js";
import { clearMarkers, clusterFeatureAt, geolocation, setMapStatus, zoomToCluster } from "./map.js";
import {
    closeAccountMenu,
    closeProgressMenu,
    closeProviderMenu,
    closeSearchMenu,
    toggleAccountMenu,
    toggleProgressMenu,
    toggleProviderMenu,
    toggleSearchMenu
} from "./menus.js";
import { getAppRootUrl, initialNavigation, isGoogleCallbackPath } from "./navigation.js";
import {
    closeDeleteVisitDialog,
    closeVisitForm,
    copyActivePointLink,
    hideInfo,
    openVisitForm,
    positionInfo,
    refreshInfoPosition,
    saveVisit,
    setVisitActionStatus,
    setVisitBusy,
    showInfo,
    toLocalDateInput,
    toLocalTimeInput
} from "./point-details.js";
import { cycleVisitFilter, getPointNumberLabel, renderSearchResults, resetPointCache } from "./points.js";
import {
    applyProviderSelection,
    closeProviderInfo,
    resetProviderCatalog,
    selectProviders
} from "./providers.js";
import { setOfflineMode, setSession, showAuthBarrier, showOfflineUnavailable } from "./session.js";
import { app, finePointer, reducedMotion } from "./state.js";
import { TAB_ID, clearRetryTimer, hasPendingAction, setPendingActions, syncChannel } from "./sync.js";
import { clearPersonalData, queueVisitAction, refreshFromStoredSnapshot } from "./visit-sync.js";

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
            if (isGoogleCallbackPath(window.location.pathname)) {
                window.location.replace(getAppRootUrl().href);
            } else {
                window.location.reload();
            }
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

if (initialNavigation.pointLink) {
    const returnUrl = `${window.location.pathname}${window.location.search}`;
    const loginHref = `auth/login?returnUrl=${encodeURIComponent(returnUrl)}`;
    elements.loginLink.href = loginHref;
    elements.visitLoginLink.href = loginHref;
    elements.authBarrierLoginButton.href = loginHref;
}

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

finePointer.addEventListener("change", refreshInfoPosition);
window.addEventListener("resize", refreshInfoPosition);

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
