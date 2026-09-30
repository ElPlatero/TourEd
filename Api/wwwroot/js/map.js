// OpenLayers map, marker and cluster styles, user location and geolocation.
import { VisitState } from "./constants.js";
import { elements } from "./dom.js";
import { app, finePointer, reducedMotion } from "./state.js";

if (typeof ol === "undefined") {
    elements.mapStatus.dataset.state = "error";
    elements.mapStatus.textContent = "Die Kartenbibliothek konnte nicht geladen werden.";
    // Stops evaluating the application module, like the former early return.
    throw new Error("OpenLayers is not available.");
}

const createMarkerStyle = iconSource => new ol.style.Style({
    image: new ol.style.Icon({
        anchor: [0.5, 1],
        src: iconSource,
        scale: 0.32
    })
});

const markerStyles = {
    [VisitState.unknown]: createMarkerStyle("img/pin_icon_neutral.svg"),
    [VisitState.open]: createMarkerStyle("img/pin_icon_neutral.svg"),
    [VisitState.visited]: createMarkerStyle("img/pin_icon_visited.svg")
};
const clusterStyleCache = new Map();
const markerSource = new ol.source.Vector();
const clusterSource = new ol.source.Cluster({
    distance: 44,
    source: markerSource
});

const getClusterSize = count => count < 10 ? 40 : count < 50 ? 46 : 52;

const getClusterVisitState = features => {
    const visitedCount = features.filter(feature => feature.visitState === VisitState.visited).length;
    if (visitedCount === features.length) {
        return VisitState.visited;
    }
    return visitedCount === 0 ? VisitState.open : "mixed";
};

const createClusterIconSource = (displayCount, visitState, size) => {
    const center = size / 2;
    const outerRadius = center - 1;
    const innerRadius = center - 8;
    const fill = visitState === "mixed"
        ? "url(#mixed)"
        : visitState === VisitState.visited ? "#123e65" : "#279cdf";
    const mixedDefinition = visitState === "mixed"
        ? [
            '<defs><linearGradient id="mixed" x1="0" y1="1" x2="1" y2="0">',
            '<stop offset="0%" stop-color="#123e65"/>',
            '<stop offset="50%" stop-color="#123e65"/>',
            '<stop offset="50%" stop-color="#279cdf"/>',
            '<stop offset="100%" stop-color="#279cdf"/>',
            "</linearGradient></defs>"
        ].join("")
        : "";
    const check = visitState === VisitState.visited
        ? `<circle cx="${size - 9}" cy="${size - 9}" r="7" fill="#123e65" stroke="#fff" stroke-width="1.5"/><path d="M${size - 13} ${size - 9}l3 3 5-6" fill="none" stroke="#fff" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"/>`
        : "";
    const fontSize = displayCount.length > 2 ? 12 : 14;
    const svg = [
        `<svg xmlns="http://www.w3.org/2000/svg" width="${size}" height="${size}" viewBox="0 0 ${size} ${size}">`,
        mixedDefinition,
        `<circle cx="${center}" cy="${center}" r="${outerRadius}" fill="${fill}" stroke="#123e65" stroke-width="1.5"/>`,
        `<circle cx="${center}" cy="${center}" r="${innerRadius}" fill="#fff"/>`,
        `<text x="${center}" y="${center + 0.5}" fill="#123e65" font-family="system-ui,sans-serif" font-size="${fontSize}" font-weight="700" text-anchor="middle" dominant-baseline="middle">${displayCount}</text>`,
        check,
        "</svg>"
    ].join("");
    return `data:image/svg+xml;charset=utf-8,${encodeURIComponent(svg)}`;
};

const getClusterStyle = features => {
    const displayCount = features.length > 99 ? "99+" : String(features.length);
    const visitState = getClusterVisitState(features);
    const size = getClusterSize(features.length);
    const cacheKey = `${visitState}-${displayCount}-${size}`;
    if (!clusterStyleCache.has(cacheKey)) {
        clusterStyleCache.set(cacheKey, new ol.style.Style({
            image: new ol.style.Icon({
                src: createClusterIconSource(displayCount, visitState, size)
            })
        }));
    }
    return clusterStyleCache.get(cacheKey);
};

const markerLayer = new ol.layer.Vector({
    source: clusterSource,
    style: feature => {
        const features = feature.get("features") ?? [];
        return features.length === 1
            ? markerStyles[features[0].visitState]
            : getClusterStyle(features);
    }
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

app.markerSource = markerSource;
app.markerLayer = markerLayer;
app.userLocationLayer = userLocationLayer;

const osmSource = new ol.source.OSM({
    url: "https://tile.openstreetmap.org/{z}/{x}/{y}.png",
    attributions: [ol.source.OSM.ATTRIBUTION],
    maxZoom: 18
});

osmSource.on("tileloaderror", () => {
    if (app.isOffline || !navigator.onLine) {
        if (elements.tileErrorBanner) {
            elements.tileErrorBanner.hidden = false;
        }
    }
});

osmSource.on("tileloadend", () => {
    if (elements.tileErrorBanner && !app.isOffline && navigator.onLine) {
        elements.tileErrorBanner.hidden = true;
    }
});

app.map = new ol.Map({
    controls: ol.control.defaults({ attribution: false, zoom: false }).extend([new ol.control.Attribution({
        collapsible: false
    })]),
    interactions: ol.interaction.defaults({
        altShiftDragRotate: false,
        pinchRotate: false
    }),
    layers: [
        new ol.layer.Tile({
            source: osmSource
        }),
        userLocationLayer,
        app.markerLayer
    ],
    target: elements.map,
    view: new ol.View({
        center: ol.proj.fromLonLat([11.816394330314203, 50.972084944877366]),
        enableRotation: false,
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

const clearMarkers = () => {
    app.markerSource.clear();
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

const addPoints = (stampingPoints, visitState) => {
    const features = stampingPoints.map(point => createMarker(point, visitState));
    app.markerSource.addFeatures(features);
    return features.length;
};

const clusterFeatureAt = (pixel) => app.map.forEachFeatureAtPixel(
    pixel,
    feature => feature.get("features") ? feature : undefined,
    {
        hitTolerance: finePointer.matches ? 3 : 10,
        layerFilter: layer => layer === app.markerLayer
    }
);

const zoomToCluster = clusterFeature => {
    const features = clusterFeature.get("features") ?? [];
    if (features.length < 2) {
        return;
    }

    const view = app.map.getView();
    const currentZoom = view.getZoom() ?? 0;
    const maxZoom = view.getMaxZoom();
    if (currentZoom >= maxZoom) {
        return;
    }

    const extent = ol.extent.boundingExtent(features.map(feature =>
        feature.getGeometry().getCoordinates()));
    const targetMaxZoom = Math.min(maxZoom, currentZoom + 3);
    view.fit(extent, {
        duration: reducedMotion.matches ? 0 : 450,
        maxZoom: targetMaxZoom,
        padding: [80, 80, 80, 80]
    });
};

export {
    addPoints,
    clearMarkers,
    clusterFeatureAt,
    geolocation,
    setMapStatus,
    zoomToCluster
};
