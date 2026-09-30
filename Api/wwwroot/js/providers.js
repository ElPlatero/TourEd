// Provider catalog: filter list, provider information dialog, progress overview and selection.
import { elements } from "./dom.js";
import { announceFilteredPointCount, renderSearchResults, renderSelectedPoints } from "./points.js";
import { app } from "./state.js";

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

const setSelectedProviders = providerSlugs => {
    app.selectedProviderSlugs = new Set(providerSlugs);
    return renderSelectedPoints();
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

export {
    applyProviderSelection,
    closeProviderInfo,
    renderProgressOverview,
    renderProviderOptions,
    resetProviderCatalog,
    selectProviders,
    setProviderCatalog,
    updateProgressSummaryAria
};
