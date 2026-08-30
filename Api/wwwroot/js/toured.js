(() => {
    "use strict";

    const elements = {
        accountMenuButton: document.getElementById("accountMenuButton"),
        accountPanel: document.getElementById("accountPanel"),
        closeInfoButton: document.getElementById("closeInfoButton"),
        infoCard: document.getElementById("infoCard"),
        loginLink: document.getElementById("loginLink"),
        logoutButton: document.getElementById("logoutButton"),
        map: document.getElementById("map"),
        mapLegend: document.getElementById("mapLegend"),
        mapStatus: document.getElementById("mapStatus"),
        pointName: document.getElementById("pointName"),
        pointNumber: document.getElementById("pointNumber"),
        pointStatus: document.getElementById("pointStatus"),
        pointTours: document.getElementById("pointTours"),
        pointVisited: document.getElementById("pointVisited"),
        sessionStatus: document.getElementById("sessionStatus"),
        userSession: document.getElementById("userSession")
    };

    if (typeof ol === "undefined") {
        elements.mapStatus.dataset.state = "error";
        elements.mapStatus.textContent = "Die Kartenbibliothek konnte nicht geladen werden.";
        return;
    }

    const finePointer = window.matchMedia("(hover: hover) and (pointer: fine)");
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

    const app = {
        infoLocked: false,
        infoPixel: null,
        neutralMarkers: createMarkerLayer("img/pin_icon_neutral.svg?v=3"),
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
                        '&copy; TourEd 2023 · <a class="privacy-link" href="datenschutz/">Datenschutz</a>'
                    ],
                    maxZoom: 18
                })
            }),
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

    const setSession = (session) => {
        const authenticated = session?.authenticated === true;
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

    const toggleAccountMenu = () => {
        const opening = elements.accountPanel.hidden;
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

    const clearMarkers = () => {
        app.neutralMarkers.getSource().clear();
        app.visitedMarkers.getSource().clear();
        app.unvisitedMarkers.getSource().clear();
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

    const addPoints = (response, visitState) => {
        const layer = visitState === VisitState.visited
            ? app.visitedMarkers
            : visitState === VisitState.open
                ? app.unvisitedMarkers
                : app.neutralMarkers;
        const features = response.stampingPoints.map(point => createMarker(point, visitState));
        layer.getSource().addFeatures(features);
        return features.length;
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

    const loadAnonymousPoints = async () => {
        clearMarkers();
        const points = await getJson("api/points");
        return addPoints(points, VisitState.unknown);
    };

    const loadAuthenticatedPoints = async () => {
        clearMarkers();
        try {
            const [unvisited, visited] = await Promise.all([
                getJson("api/points?vis=false"),
                getJson("api/points?vis=true")
            ]);
            return addPoints(unvisited, VisitState.open) + addPoints(visited, VisitState.visited);
        } catch (response) {
            if (response.status !== 401) {
                throw response;
            }
            setSession({ authenticated: false });
            return await loadAnonymousPoints();
        }
    };

    const hideInfo = (force = false) => {
        if (app.infoLocked && !force) {
            return;
        }
        elements.infoCard.hidden = true;
        app.infoLocked = false;
        app.infoPixel = null;
    };

    const formatVisit = (value) => {
        const visitedDate = new Date(value);
        if (Number.isNaN(visitedDate.getTime())) {
            return null;
        }
        return visitedDate.toLocaleString("de-DE", {
            year: "numeric",
            month: "long",
            day: "2-digit",
            hour: "2-digit",
            minute: "2-digit"
        }) + " Uhr";
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
        elements.pointNumber.textContent = `Stempelstelle ${stampingPoint.number}`;
        elements.pointName.textContent = stampingPoint.name;
        elements.pointStatus.textContent = visitState === VisitState.visited
            ? "✓ Besucht"
            : visitState === VisitState.open
                ? "Noch nicht besucht"
                : "Besuchsstatus nicht verfügbar";
        elements.pointStatus.dataset.state = visitState;
        populateTours(stampingPoint.tours);

        const formattedVisit = stampingPoint.visited ? formatVisit(stampingPoint.visited) : null;
        elements.pointVisited.hidden = !formattedVisit;
        elements.pointVisited.textContent = formattedVisit ?? "";
        elements.pointVisited.dateTime = stampingPoint.visited ?? "";

        elements.infoCard.hidden = false;
        app.infoLocked = locked;
        app.infoPixel = pixel;
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

    document.addEventListener("keydown", event => {
        if (event.key !== "Escape") {
            return;
        }
        if (!elements.accountPanel.hidden) {
            closeAccountMenu(true);
        } else if (!elements.infoCard.hidden) {
            hideInfo(true);
            elements.map.focus({ preventScroll: true });
        }
    });

    elements.accountMenuButton.addEventListener("click", toggleAccountMenu);

    document.addEventListener("pointerdown", event => {
        if (!elements.accountPanel.hidden && !elements.userSession.contains(event.target)) {
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
        if (window.location.search) {
            window.history.replaceState(null, "", `${window.location.pathname}${window.location.hash}`);
        }

        setMapStatus("Karte wird geladen …", "loading");
        let session = { authenticated: false };
        try {
            session = await getJson("auth/session");
        } catch {
            // Anonymous map use remains available when the session check fails.
        }
        setSession(session);

        try {
            const pointCount = session.authenticated === true
                ? await loadAuthenticatedPoints()
                : await loadAnonymousPoints();
            setMapStatus(`${pointCount} Stempelstellen geladen.`, "ready");
            window.setTimeout(() => {
                if (elements.mapStatus.dataset.state === "ready") {
                    setMapStatus("");
                }
            }, 1800);
        } catch {
            setMapStatus("Die Stempelstellen konnten nicht geladen werden.", "error");
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
            await initialize();
            closeAccountMenu();
        } catch {
            setMapStatus("Abmelden ist fehlgeschlagen. Bitte erneut versuchen.", "error");
        } finally {
            elements.logoutButton.disabled = false;
        }
    });

    initialize();
})();
