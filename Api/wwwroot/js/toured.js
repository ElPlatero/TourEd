(() => {
    "use strict";

    const elements = {
        closeInfoButton: document.getElementById("closeInfoButton"),
        infoCard: document.getElementById("infoCard"),
        loginLink: document.getElementById("loginLink"),
        logoutButton: document.getElementById("logoutButton"),
        map: document.getElementById("map"),
        mapStatus: document.getElementById("mapStatus"),
        pointName: document.getElementById("pointName"),
        pointNumber: document.getElementById("pointNumber"),
        pointStatus: document.getElementById("pointStatus"),
        pointTours: document.getElementById("pointTours"),
        pointVisited: document.getElementById("pointVisited"),
        sessionStatus: document.getElementById("sessionStatus")
    };

    if (typeof ol === "undefined") {
        elements.mapStatus.dataset.state = "error";
        elements.mapStatus.textContent = "Die Kartenbibliothek konnte nicht geladen werden.";
        return;
    }

    const finePointer = window.matchMedia("(hover: hover) and (pointer: fine)");

    const createMarkerLayer = (iconSource, visited) => new ol.layer.Vector({
        source: new ol.source.Vector(),
        style: new ol.style.Style({
            image: new ol.style.Icon({
                anchor: [0.5, 1],
                src: iconSource,
                scale: 0.4
            }),
            text: visited ? new ol.style.Text({
                text: "✓",
                offsetY: -20,
                font: "bold 14px sans-serif",
                fill: new ol.style.Fill({ color: "#ffffff" }),
                stroke: new ol.style.Stroke({ color: "#245a3b", width: 2 })
            }) : undefined
        })
    });

    const app = {
        infoLocked: false,
        infoPixel: null,
        visitedMarkers: createMarkerLayer("img/pin_icon_green.png", true),
        unvisitedMarkers: createMarkerLayer("img/pin_icon_red.png", false)
    };

    app.map = new ol.Map({
        controls: ol.control.defaults({ attribution: false }).extend([new ol.control.Attribution({
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
    };

    const clearMarkers = () => {
        app.visitedMarkers.getSource().clear();
        app.unvisitedMarkers.getSource().clear();
    };

    const createMarker = (stampingPoint, visited) => {
        const feature = new ol.Feature(new ol.geom.Point(ol.proj.fromLonLat([
            stampingPoint.position.longitude,
            stampingPoint.position.latitude
        ])));
        feature.stampingPoint = stampingPoint;
        feature.visited = visited;
        return feature;
    };

    const addPoints = (response, visited) => {
        const layer = visited ? app.visitedMarkers : app.unvisitedMarkers;
        const features = response.stampingPoints.map(point => createMarker(point, visited));
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
        return addPoints(points, false);
    };

    const loadAuthenticatedPoints = async () => {
        clearMarkers();
        try {
            const [unvisited, visited] = await Promise.all([
                getJson("api/points?vis=false"),
                getJson("api/points?vis=true")
            ]);
            return addPoints(unvisited, false) + addPoints(visited, true);
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
        const visited = feature.visited === true;
        elements.pointNumber.textContent = `Stempelstelle ${stampingPoint.number}`;
        elements.pointName.textContent = stampingPoint.name;
        elements.pointStatus.textContent = visited ? "✓ Besucht" : "Noch nicht besucht";
        elements.pointStatus.dataset.visited = visited.toString();
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
        if (event.key === "Escape" && !elements.infoCard.hidden) {
            hideInfo(true);
            elements.map.focus({ preventScroll: true });
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
        } catch {
            setMapStatus("Abmelden ist fehlgeschlagen. Bitte erneut versuchen.", "error");
        } finally {
            elements.logoutButton.disabled = false;
        }
    });

    initialize();
})();
