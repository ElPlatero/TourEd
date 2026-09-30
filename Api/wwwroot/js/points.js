// Cached stamping points: visit filter, search, point links and marker rendering.
import { SearchResultLimit, VisitFilter, VisitFilterOrder, VisitState } from "./constants.js";
import { elements } from "./dom.js";
import { addPoints, clearMarkers, setMapStatus } from "./map.js";
import { closeAccountMenu, closeProgressMenu, closeProviderMenu, closeSearchMenu } from "./menus.js";
import { hideInfo, pointMatches, showInfo } from "./point-details.js";
import { renderProviderOptions } from "./providers.js";
import { app, reducedMotion } from "./state.js";

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

export {
    announceFilteredPointCount,
    cachePoints,
    cycleVisitFilter,
    findRenderedFeature,
    formatPointCount,
    getPointNumberLabel,
    isVisitStateVisible,
    openPendingPointLink,
    renderSearchResults,
    renderSelectedPoints,
    resetPointCache,
    updateVisitFilterButton
};
