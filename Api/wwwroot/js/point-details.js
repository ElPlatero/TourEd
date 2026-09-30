// Point details dialog: position, tours, point link, visit form and delete confirmation.
import { VisitState } from "./constants.js";
import { elements } from "./dom.js";
import {
    announceFilteredPointCount,
    findRenderedFeature,
    getPointNumberLabel,
    isVisitStateVisible,
    renderSearchResults,
    renderSelectedPoints
} from "./points.js";
import { renderProgressOverview } from "./providers.js";
import { app, finePointer } from "./state.js";
import { hasPendingAction } from "./sync.js";
import { queueVisitAction } from "./visit-sync.js";

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

const closeDeleteVisitDialog = () => {
    if (elements.deleteVisitDialog.open) {
        elements.deleteVisitDialog.close();
    }
};

const refreshInfoPosition = () => {
    if (!elements.infoCard.hidden && app.infoPixel) {
        positionInfo(app.infoPixel);
    }
};

export {
    closeDeleteVisitDialog,
    closeVisitForm,
    copyActivePointLink,
    hideInfo,
    openVisitForm,
    pointMatches,
    positionInfo,
    refreshInfoPosition,
    restoreLockedInfo,
    saveVisit,
    setVisitActionStatus,
    setVisitBusy,
    showInfo,
    toLocalDateInput,
    toLocalTimeInput,
    updatePointVisit
};
