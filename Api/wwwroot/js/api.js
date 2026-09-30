// HTTP calls to the TourEd backend; callers interpret status codes.
const getJson = async (url) => {
    const response = await fetch(url, {
        headers: { "Accept": "application/json" }
    });
    if (!response.ok) {
        throw response;
    }
    return await response.json();
};

const sendVisitStateRequest = async action => {
    const provider = encodeURIComponent(action.providerSlug);
    const response = await fetch(`api/points/id/${action.pointId}/state?provider=${provider}`, {
        method: "PUT",
        headers: { "Accept": "application/json", "Content-Type": "application/json" },
        body: JSON.stringify({
            expected: action.expected,
            desired: action.desired,
            utcOffsetMinutes: action.utcOffsetMinutes
        })
    });
    let body = null;
    if (response.headers.get("content-type")?.includes("json")) {
        try {
            body = await response.json();
        } catch {
            body = null;
        }
    }
    return { response, body };
};

export {
    getJson,
    sendVisitStateRequest
};
