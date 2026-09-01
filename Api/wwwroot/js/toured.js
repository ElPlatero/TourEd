(() => {
    "use strict";

    const elements = {
        accountMenuButton: document.getElementById("accountMenuButton"),
        accountPanel: document.getElementById("accountPanel"),
        appShell: document.getElementById("appShell"),
        authBarrier: document.getElementById("authBarrier"),
        authBarrierLoginButton: document.getElementById("authBarrierLoginButton"),
        authBarrierNotice: document.getElementById("authBarrierNotice"),
        cancelDeleteVisitButton: document.getElementById("cancelDeleteVisitButton"),
        cancelVisitButton: document.getElementById("cancelVisitButton"),
        closeDeleteVisitButton: document.getElementById("closeDeleteVisitButton"),
        closeInfoButton: document.getElementById("closeInfoButton"),
        closeProviderInfoButton: document.getElementById("closeProviderInfoButton"),
        confirmDeleteVisitButton: document.getElementById("confirmDeleteVisitButton"),
        deleteVisitButton: document.getElementById("deleteVisitButton"),
        deleteVisitDialog: document.getElementById("deleteVisitDialog"),
        deleteVisitMessage: document.getElementById("deleteVisitMessage"),
        editVisitButton: document.getElementById("editVisitButton"),
        existingVisitActions: document.getElementById("existingVisitActions"),
        infoCard: document.getElementById("infoCard"),
        locateButton: document.getElementById("locateButton"),
        loginLink: document.getElementById("loginLink"),
        logoutButton: document.getElementById("logoutButton"),
        map: document.getElementById("map"),
        mapLegend: document.getElementById("mapLegend"),
        mapStatus: document.getElementById("mapStatus"),
        newVisitActions: document.getElementById("newVisitActions"),
        openVisitFormButton: document.getElementById("openVisitFormButton"),
        pointName: document.getElementById("pointName"),
        pointNumber: document.getElementById("pointNumber"),
        pointProvider: document.getElementById("pointProvider"),
        pointStatus: document.getElementById("pointStatus"),
        pointTours: document.getElementById("pointTours"),
        pointVisited: document.getElementById("pointVisited"),
        providerInfoDescription: document.getElementById("providerInfoDescription"),
        providerInfoDialog: document.getElementById("providerInfoDialog"),
        providerInfoDisclaimer: document.getElementById("providerInfoDisclaimer"),
        providerInfoName: document.getElementById("providerInfoName"),
        providerInfoWebsite: document.getElementById("providerInfoWebsite"),
        providerDataAttribution: document.getElementById("providerDataAttribution"),
        providerDataDownload: document.getElementById("providerDataDownload"),
        providerDataLicenseLink: document.getElementById("providerDataLicenseLink"),
        providerDataSource: document.getElementById("providerDataSource"),
        providerDataSourceLink: document.getElementById("providerDataSourceLink"),
        providerMenuButton: document.getElementById("providerMenuButton"),
        providerOptions: document.getElementById("providerOptions"),
        providerPanel: document.getElementById("providerPanel"),
        searchMenuButton: document.getElementById("searchMenuButton"),
        searchPanel: document.getElementById("searchPanel"),
        searchResults: document.getElementById("searchResults"),
        searchResultsStatus: document.getElementById("searchResultsStatus"),
        selectAllProvidersButton: document.getElementById("selectAllProvidersButton"),
        selectNoProvidersButton: document.getElementById("selectNoProvidersButton"),
        sessionStatus: document.getElementById("sessionStatus"),
        stampingPointSearchInput: document.getElementById("stampingPointSearchInput"),
        userSession: document.getElementById("userSession"),
        visitActionStatus: document.getElementById("visitActionStatus"),
        visitControls: document.getElementById("visitControls"),
        visitedAtInput: document.getElementById("visitedAtInput"),
        visitedOnInput: document.getElementById("visitedOnInput"),
        visitForm: document.getElementById("visitForm"),
        visitLoginLink: document.getElementById("visitLoginLink"),
        visitNowButton: document.getElementById("visitNowButton")
    };

    if (typeof ol === "undefined") {
        elements.mapStatus.dataset.state = "error";
        elements.mapStatus.textContent = "Die Kartenbibliothek konnte nicht geladen werden.";
        return;
    }

    const finePointer = window.matchMedia("(hover: hover) and (pointer: fine)");
    const reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)");
    const SearchResultLimit = 30;
    const VisitState = Object.freeze({
        unknown: "unknown",
        open: "open",
        visited: "visited"
    });

    const createMarkerLayer = iconSource => new ol.layer.Vector({
        source: new ol.source.Vector(),
        style: new ol.style.Style({
            image: new ol.style.Icon({
                anchor: [0.5, 1],
                src: iconSource,
                scale: 0.32
            })
        })
    });

    const locationSource = new ol.source.Vector();
    const accuracyFeature = new ol.Feature();
    const positionFeature = new ol.Feature();

    positionFeature.setStyle(
        new ol.style.Style({
            image: new ol.style.Circle({
                radius: 7,
                fill: new ol.style.Fill({
                    color: "rgba(39, 156, 223, 0.85)"
                }),
                stroke: new ol.style.Stroke({
                    color: "#123e65",
                    width: 2.5
                })
            })
        })
    );

    accuracyFeature.setStyle(
        new ol.style.Style({
            fill: new ol.style.Fill({
                color: "rgba(39, 156, 223, 0.12)"
            }),
            stroke: new ol.style.Stroke({
                color: "rgba(18, 62, 101, 0.3)",
                width: 1
            })
        })
    );

    locationSource.addFeatures([accuracyFeature, positionFeature]);

    const userLocationLayer = new ol.layer.Vector({
        source: locationSource
    });

    const app = {
        activeFeature: null,
        authenticated: false,
        centerOnNextPosition: false,
        infoLocked: false,
        infoPixel: null,
        loadGeneration: 0,
        providerInfoTrigger: null,
        neutralMarkers: createMarkerLayer("img/pin_icon_neutral.svg?v=3"),
        pointCache: {
            [VisitState.unknown]: [],
            [VisitState.open]: [],
            [VisitState.visited]: []
        },
        providers: [],
        selectedProviderSlugs: new Set(),
        userLocationLayer,
        visitedMarkers: createMarkerLayer("img/pin_icon_visited.svg?v=3"),
        unvisitedMarkers: createMarkerLayer("img/pin_icon_neutral.svg?v=3")
    };

    app.map = new ol.Map({
        controls: ol.control.defaults({ attribution: false, zoom: false }).extend([new ol.control.Attribution({
            collapsible: false
        })]),
        layers: [
            new ol.layer.Tile({
                source: new ol.source.OSM({
                    url: "https://tile.openstreetmap.org/{z}/{x}/{y}.png",
                    attributions: [
                        ol.source.OSM.ATTRIBUTION,
                        '<a class="footer-link" href="https://github.com/ElPlatero/TourEd" target="_blank" rel="noopener noreferrer" aria-label="TourEd-Quellcode auf GitHub (AGPL-3.0)" title="TourEd-Quellcode auf GitHub (AGPL-3.0)">&copy; TourEd</a> · <a class="footer-link" href="datenschutz/">Datenschutz</a>'
                    ],
                    maxZoom: 18
                })
            }),
            userLocationLayer,
            app.neutralMarkers,
            app.unvisitedMarkers,
            app.visitedMarkers
        ],
        target: elements.map,
        view: new ol.View({
            center: ol.proj.fromLonLat([11.816394330314203, 50.972084944877366]),
            maxZoom: 18,
            zoom: 12
        })
    });

    const setMapStatus = (message, state) => {
        elements.mapStatus.textContent = message;
        elements.mapStatus.dataset.state = state ?? "loading";
        elements.mapStatus.hidden = !message;
    };

    const geolocation = new ol.Geolocation({
        tracking: false,
        trackingOptions: {
            enableHighAccuracy: true
        },
        projection: app.map.getView().getProjection()
    });

    geolocation.on("change:position", () => {
        const coordinates = geolocation.getPosition();
        if (coordinates) {
            positionFeature.setGeometry(new ol.geom.Point(coordinates));
            if (app.centerOnNextPosition) {
                app.centerOnNextPosition = false;
                setMapStatus("");
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
            }
        }
    });

    geolocation.on("change:accuracyGeometry", () => {
        const geometry = geolocation.getAccuracyGeometry();
        accuracyFeature.setGeometry(geometry ?? undefined);
    });

    geolocation.on("error", () => {
        geolocation.setTracking(false);
        positionFeature.setGeometry(undefined);
        accuracyFeature.setGeometry(undefined);
        if (app.centerOnNextPosition) {
            app.centerOnNextPosition = false;
            setMapStatus("Standort konnte nicht ermittelt werden.", "error");
            setTimeout(() => {
                if (elements.mapStatus.textContent === "Standort konnte nicht ermittelt werden.") {
                    setMapStatus("");
                }
            }, 4000);
        }
    });

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

    const setSession = (session) => {
        const authenticated = session?.authenticated === true;
        app.authenticated = authenticated;
        elements.sessionStatus.textContent = authenticated ? session.email : "Nicht angemeldet";
        elements.loginLink.hidden = authenticated;
        elements.logoutButton.hidden = !authenticated;
        elements.mapLegend.hidden = !authenticated;
    };

    const closeAccountMenu = (restoreFocus = false) => {
        elements.accountPanel.hidden = true;
        elements.accountMenuButton.setAttribute("aria-expanded", "false");
        elements.accountMenuButton.setAttribute("aria-label", "Kontomenü öffnen");
        if (restoreFocus) {
            elements.accountMenuButton.focus({ preventScroll: true });
        }
    };

    const closeSearchMenu = (restoreFocus = false) => {
        elements.searchPanel.hidden = true;
        elements.searchMenuButton.setAttribute("aria-expanded", "false");
        elements.searchMenuButton.setAttribute("aria-label", "Stempelstellensuche öffnen");
        if (restoreFocus) {
            elements.searchMenuButton.focus({ preventScroll: true });
        }
    };

    const closeProviderMenu = (restoreFocus = false) => {
        elements.providerPanel.hidden = true;
        elements.providerMenuButton.setAttribute("aria-expanded", "false");
        elements.providerMenuButton.setAttribute("aria-label", "Anbieterfilter öffnen");
        if (restoreFocus) {
            elements.providerMenuButton.focus({ preventScroll: true });
        }
    };

    const toggleAccountMenu = () => {
        const opening = elements.accountPanel.hidden;
        if (opening) {
            closeSearchMenu();
            closeProviderMenu();
        }
        elements.accountPanel.hidden = !opening;
        elements.accountMenuButton.setAttribute("aria-expanded", opening.toString());
        elements.accountMenuButton.setAttribute(
            "aria-label",
            opening ? "Kontomenü schließen" : "Kontomenü öffnen");
        if (opening) {
            const action = elements.accountPanel.querySelector("a:not([hidden]), button:not([hidden])");
            action?.focus({ preventScroll: true });
        }
    };

    const toggleProviderMenu = () => {
        const opening = elements.providerPanel.hidden;
        if (opening) {
            closeSearchMenu();
            closeAccountMenu();
        }
        elements.providerPanel.hidden = !opening;
        elements.providerMenuButton.setAttribute("aria-expanded", opening.toString());
        elements.providerMenuButton.setAttribute(
            "aria-label",
            opening ? "Anbieterfilter schließen" : "Anbieterfilter öffnen");
        if (opening) {
            elements.providerOptions.querySelector("input")?.focus({ preventScroll: true });
        }
    };

    const toggleSearchMenu = () => {
        const opening = elements.searchPanel.hidden;
        if (opening) {
            closeProviderMenu();
            closeAccountMenu();
        }
        elements.searchPanel.hidden = !opening;
        elements.searchMenuButton.setAttribute("aria-expanded", opening.toString());
        elements.searchMenuButton.setAttribute(
            "aria-label",
            opening ? "Stempelstellensuche schließen" : "Stempelstellensuche öffnen");
        if (opening) {
            renderSearchResults();
            elements.stampingPointSearchInput.focus({ preventScroll: true });
        }
    };

    const clearMarkers = () => {
        app.neutralMarkers.getSource().clear();
        app.visitedMarkers.getSource().clear();
        app.unvisitedMarkers.getSource().clear();
    };

    const resetPointCache = () => {
        app.pointCache[VisitState.unknown] = [];
        app.pointCache[VisitState.open] = [];
        app.pointCache[VisitState.visited] = [];
    };

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
            && licenseUrl
            && provider.hasPublicDataDownload);
        elements.providerDataSource.hidden = !hasDataSource;
        if (hasDataSource) {
            elements.providerDataAttribution.textContent = provider.dataSourceAttribution;
            elements.providerDataSourceLink.href = sourceUrl;
            elements.providerDataLicenseLink.href = licenseUrl;
            elements.providerDataLicenseLink.textContent = provider.dataLicenseName;
            elements.providerDataDownload.href = `api/providers/${encodeURIComponent(provider.slug)}/points.geojson`;
            elements.providerDataDownload.download = `${provider.slug}-stempelstellen.geojson`;
        } else {
            elements.providerDataAttribution.textContent = "";
            elements.providerDataSourceLink.removeAttribute("href");
            elements.providerDataLicenseLink.removeAttribute("href");
            elements.providerDataLicenseLink.textContent = "";
            elements.providerDataDownload.removeAttribute("href");
            elements.providerDataDownload.removeAttribute("download");
        }
        elements.providerInfoTrigger = trigger;
        elements.providerInfoDialog.showModal();
        elements.closeProviderInfoButton.focus({ preventScroll: true });
    };

    const renderProviderOptions = () => {
        elements.providerOptions.replaceChildren();
        if (app.providers.length === 0) {
            const status = document.createElement("p");
            status.className = "provider-options-status";
            status.textContent = "Keine Stempelanbieter verfügbar.";
            elements.providerOptions.appendChild(status);
            return;
        }

        app.providers.forEach((provider, index) => {
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

            const infoButton = document.createElement("button");
            infoButton.type = "button";
            infoButton.className = "provider-info-button";
            infoButton.setAttribute("aria-label", `Informationen zu ${provider.name}`);
            infoButton.setAttribute("aria-haspopup", "dialog");
            const questionMark = document.createElement("span");
            questionMark.setAttribute("aria-hidden", "true");
            questionMark.textContent = "?";
            infoButton.appendChild(questionMark);
            infoButton.addEventListener("click", () => openProviderInfo(provider, infoButton));

            row.append(label, infoButton);
            elements.providerOptions.appendChild(row);
        });
    };

    const setProviderCatalog = response => {
        app.providers = Array.isArray(response.stampingProviders)
            ? response.stampingProviders.filter(provider => typeof provider.slug === "string")
            : [];
        app.selectedProviderSlugs = new Set(app.providers.map(provider => provider.slug));
        renderProviderOptions();
    };

    const createMarker = (stampingPoint, visitState) => {
        const feature = new ol.Feature(new ol.geom.Point(ol.proj.fromLonLat([
            stampingPoint.position.longitude,
            stampingPoint.position.latitude
        ])));
        feature.stampingPoint = stampingPoint;
        feature.visitState = visitState;
        return feature;
    };

    const getMarkerLayer = visitState => visitState === VisitState.visited
            ? app.visitedMarkers
            : visitState === VisitState.open
                ? app.unvisitedMarkers
                : app.neutralMarkers;

    const addPoints = (stampingPoints, visitState) => {
        const layer = getMarkerLayer(visitState);
        const features = stampingPoints.map(point => createMarker(point, visitState));
        layer.getSource().addFeatures(features);
        return features.length;
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
        app.pointCache[visitState]
            .filter(point => app.selectedProviderSlugs.has(point.provider?.slug))
            .map(point => ({ point, visitState })));

    const findRenderedFeature = result => getMarkerLayer(result.visitState)
        .getSource()
        .getFeatures()
        .find(feature => pointMatches(feature.stampingPoint, result.point));

    const openSearchResult = result => {
        const feature = findRenderedFeature(result);
        if (!feature) {
            elements.searchResultsStatus.textContent = "Der Treffer ist momentan nicht auf der Karte verfügbar.";
            return;
        }

        const coordinate = feature.getGeometry().getCoordinates();
        const view = app.map.getView();
        const zoom = Math.max(view.getZoom() ?? 0, 15);
        closeSearchMenu();
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

    const renderSearchResults = () => {
        elements.searchResults.replaceChildren();
        const query = normalizeSearchText(elements.stampingPointSearchInput.value);
        if (!query) {
            elements.searchResultsStatus.textContent = "Suche innerhalb der ausgewählten Anbieter.";
            return;
        }

        const searchablePoints = getSearchablePoints();
        if (searchablePoints.length === 0) {
            elements.searchResultsStatus.textContent = "In den ausgewählten Anbietern sind keine Stempelstellen verfügbar.";
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
            const selectedPoints = app.pointCache[visitState].filter(point =>
                app.selectedProviderSlugs.has(point.provider?.slug));
            return count + addPoints(selectedPoints, visitState);
        }, 0);
    };

    const setSelectedProviders = providerSlugs => {
        app.selectedProviderSlugs = new Set(providerSlugs);
        return renderSelectedPoints();
    };

    const announceFilteredPointCount = pointCount => {
        setMapStatus(`${pointCount} Stempelstellen angezeigt.`, "ready");
        if (pointCount > 0) {
            window.setTimeout(() => {
                if (elements.mapStatus.textContent === `${pointCount} Stempelstellen angezeigt.`) {
                    setMapStatus("");
                }
            }, 1800);
        }
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

    const getJson = async (url) => {
        const response = await fetch(url, {
            headers: { "Accept": "application/json" }
        });
        if (!response.ok) {
            throw response;
        }
        return await response.json();
    };

    const sendVisitRequest = async (method, stampingPoint, body) => {
        const provider = encodeURIComponent(stampingPoint.provider.slug);
        const response = await fetch(`api/points/id/${stampingPoint.id}?provider=${provider}`, {
            method,
            headers: body === undefined
                ? { "Accept": "application/json" }
                : { "Accept": "application/json", "Content-Type": "application/json" },
            body: body === undefined ? undefined : JSON.stringify(body)
        });
        if (!response.ok) {
            throw response;
        }
    };

    const loadAuthenticatedPoints = async generation => {
        try {
            const [unvisited, visited] = await Promise.all([
                getJson("api/points?provider=all&vis=false"),
                getJson("api/points?provider=all&vis=true")
            ]);
            if (generation !== app.loadGeneration) {
                return null;
            }
            cachePoints(unvisited, VisitState.open);
            cachePoints(visited, VisitState.visited);
            return renderSelectedPoints();
        } catch (response) {
            if (generation !== app.loadGeneration) {
                return null;
            }
            if (response?.status === 401) {
                setSession({ authenticated: false });
                resetPointCache();
                clearMarkers();
                showAuthBarrier();
                return null;
            }
            throw response;
        }
    };

    const hideInfo = (force = false) => {
        if (app.infoLocked && !force) {
            return;
        }
        elements.infoCard.hidden = true;
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

    const updatePointVisit = (feature, isVisited, visitedOn = null, visitedAt = null) => {
        const stampingPoint = feature.stampingPoint;
        for (const visitState of Object.values(VisitState)) {
            app.pointCache[visitState] = app.pointCache[visitState].filter(point =>
                !pointMatches(point, stampingPoint));
        }

        const previousLayer = getMarkerLayer(feature.visitState);
        previousLayer.getSource().removeFeature(feature);
        stampingPoint.isVisited = isVisited;
        stampingPoint.visitedOn = visitedOn;
        stampingPoint.visitedAt = visitedAt;
        feature.visitState = isVisited ? VisitState.visited : VisitState.open;
        app.pointCache[feature.visitState].push(stampingPoint);
        getMarkerLayer(feature.visitState).getSource().addFeature(feature);
        renderSearchResults();
        showInfo(feature, app.infoPixel, true);
    };

    const setVisitActionStatus = (message, state) => {
        elements.visitActionStatus.textContent = message;
        elements.visitActionStatus.dataset.state = state ?? "ready";
    };

    const setVisitBusy = busy => {
        for (const control of [
            elements.visitNowButton,
            elements.openVisitFormButton,
            elements.editVisitButton,
            elements.deleteVisitButton,
            elements.visitedOnInput,
            elements.visitedAtInput,
            elements.cancelVisitButton,
            elements.confirmDeleteVisitButton,
            elements.cancelDeleteVisitButton,
            elements.closeDeleteVisitButton
        ]) {
            control.disabled = busy;
        }
        if (!busy) {
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
        if (!stampingPoint) {
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

    const describeVisitError = response => {
        if (response?.status === 401) {
            return "Deine Sitzung ist abgelaufen. Bitte melde dich erneut an.";
        }
        if (response?.status === 404) {
            return "Die Stempelstelle oder der Eintrag wurde nicht gefunden.";
        }
        if (response?.status === 409) {
            return "Für diese Stempelstelle ist bereits ein Eintrag vorhanden.";
        }
        if (response?.status === 400) {
            return "Datum oder Uhrzeit sind ungültig. Bitte prüfe deine Eingabe.";
        }
        return "Der Eintrag konnte nicht gespeichert werden. Bitte versuche es erneut.";
    };

    const saveVisit = async (method, visitedOn, visitedAt) => {
        const feature = app.activeFeature;
        if (!feature) {
            return;
        }
        setVisitBusy(true);
        setVisitActionStatus("Eintrag wird gespeichert …");
        try {
            await sendVisitRequest(method, feature.stampingPoint, {
                visitedOn,
                visitedAt,
                utcOffsetMinutes: -new Date().getTimezoneOffset()
            });
            updatePointVisit(feature, true, visitedOn, visitedAt);
            setVisitActionStatus("Eintrag wurde gespeichert.", "ready");
        } catch (response) {
            if (response?.status === 401) {
                setMapStatus("Deine Sitzung ist abgelaufen. Bitte melde dich erneut an.", "error");
                await initialize();
                return;
            }
            setVisitActionStatus(describeVisitError(response), "error");
        } finally {
            setVisitBusy(false);
        }
    };

    const updateVisitControls = (feature, locked) => {
        const visited = feature.visitState === VisitState.visited;
        elements.visitControls.hidden = !locked;
        elements.visitLoginLink.hidden = !locked || app.authenticated;
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

    const featureAt = (pixel) => app.map.forEachFeatureAtPixel(
        pixel,
        feature => feature.stampingPoint ? feature : undefined,
        { hitTolerance: finePointer.matches ? 3 : 10 }
    );

    app.map.on("click", event => {
        const feature = featureAt(event.pixel);
        if (feature) {
            showInfo(feature, event.pixel, true);
        } else {
            hideInfo(true);
        }
    });

    app.map.on("pointermove", event => {
        if (!finePointer.matches || app.infoLocked) {
            return;
        }
        const feature = featureAt(event.pixel);
        elements.map.style.cursor = feature ? "pointer" : "";
        if (feature) {
            showInfo(feature, event.pixel, false);
        } else {
            hideInfo();
        }
    });

    elements.closeInfoButton.addEventListener("click", () => {
        hideInfo(true);
        elements.map.focus({ preventScroll: true });
    });

    elements.visitNowButton.addEventListener("click", () => {
        const now = new Date();
        saveVisit("PUT", toLocalDateInput(now), `${toLocalTimeInput(now)}:00`);
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
        saveVisit(app.activeFeature?.visitState === VisitState.visited ? "PATCH" : "PUT", visitedOn, visitedAt);
    });

    const closeDeleteVisitDialog = () => {
        if (elements.deleteVisitDialog.open) {
            elements.deleteVisitDialog.close();
        }
    };

    elements.deleteVisitButton.addEventListener("click", () => {
        const stampingPoint = app.activeFeature?.stampingPoint;
        if (!stampingPoint) {
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
            await sendVisitRequest("DELETE", feature.stampingPoint);
            updatePointVisit(feature, false);
            closeDeleteVisitDialog();
            setVisitActionStatus("Stempeleintrag wurde entfernt.", "ready");
        } catch (response) {
            closeDeleteVisitDialog();
            if (response?.status === 401) {
                setMapStatus("Deine Sitzung ist abgelaufen. Bitte melde dich erneut an.", "error");
                await initialize();
                return;
            }
            setVisitActionStatus(describeVisitError(response), "error");
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
        if (!elements.searchPanel.hidden) {
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

    elements.accountMenuButton.addEventListener("click", toggleAccountMenu);
    elements.providerMenuButton.addEventListener("click", toggleProviderMenu);
    elements.searchMenuButton.addEventListener("click", toggleSearchMenu);
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
        elements.providerInfoTrigger?.focus({ preventScroll: true });
        elements.providerInfoTrigger = null;
    });
    elements.providerInfoDialog.addEventListener("click", event => {
        if (event.target === elements.providerInfoDialog) {
            closeProviderInfo();
        }
    });

    document.addEventListener("pointerdown", event => {
        if (elements.providerInfoDialog.open || elements.deleteVisitDialog.open || elements.userSession.contains(event.target)) {
            return;
        }
        if (!elements.searchPanel.hidden) {
            closeSearchMenu();
        } else if (!elements.providerPanel.hidden) {
            closeProviderMenu();
        } else if (!elements.accountPanel.hidden) {
            closeAccountMenu();
        }
    });

    const refreshInfoPosition = () => {
        if (!elements.infoCard.hidden && app.infoPixel) {
            positionInfo(app.infoPixel);
        }
    };

    finePointer.addEventListener("change", refreshInfoPosition);
    window.addEventListener("resize", refreshInfoPosition);

    const initialize = async () => {
        const generation = ++app.loadGeneration;
        const urlParams = new URLSearchParams(window.location.search);
        const registrationParam = urlParams.get("registration");
        if (window.location.search) {
            window.history.replaceState(null, "", `${window.location.pathname}${window.location.hash}`);
        }

        hideInfo(true);
        clearMarkers();
        resetPointCache();
        elements.stampingPointSearchInput.value = "";
        renderSearchResults();

        const registrationDecisionVisible = registrationParam === "pending" || registrationParam === "rejected";
        elements.authBarrierLoginButton.hidden = registrationDecisionVisible;
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

        let session = { authenticated: false };
        try {
            session = await getJson("auth/session");
        } catch {
            session = { authenticated: false };
        }
        if (generation !== app.loadGeneration) {
            return;
        }
        setSession(session);

        if (!session.authenticated) {
            showAuthBarrier();
            setMapStatus("");
            return;
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
            const pointCount = await loadAuthenticatedPoints(generation);
            if (pointCount === null || generation !== app.loadGeneration) {
                return;
            }
            renderSearchResults();
            setMapStatus(`${pointCount} Stempelstellen geladen.`, "ready");
            window.setTimeout(() => {
                if (generation === app.loadGeneration && elements.mapStatus.dataset.state === "ready") {
                    setMapStatus("");
                }
            }, 1800);
        } catch (error) {
            if (generation === app.loadGeneration) {
                if (error?.status === 401) {
                    setSession({ authenticated: false });
                    resetPointCache();
                    clearMarkers();
                    showAuthBarrier();
                    setMapStatus("");
                } else {
                    setMapStatus("Anbieter und Stempelstellen konnten nicht geladen werden.", "error");
                }
            }
        }
    };

    elements.logoutButton.addEventListener("click", async () => {
        elements.logoutButton.disabled = true;
        try {
            const response = await fetch("auth/logout", { method: "POST" });
            if (!response.ok && response.status !== 401) {
                setMapStatus("Abmelden ist fehlgeschlagen. Bitte erneut versuchen.", "error");
                return;
            }
            closeAccountMenu();
            await initialize();
        } catch {
            setMapStatus("Abmelden ist fehlgeschlagen. Bitte erneut versuchen.", "error");
        } finally {
            elements.logoutButton.disabled = false;
        }
    });

    initialize();
})();
