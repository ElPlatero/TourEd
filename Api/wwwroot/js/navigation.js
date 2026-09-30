// Reads the app root and the initial point link or registration outcome from the URL.
const isGoogleCallbackPath = pathname => pathname.endsWith("/signin-google");
const getAppRootUrl = () => new URL("./", document.baseURI || window.location.href);

const readInitialNavigation = () => {
    const params = new URLSearchParams(window.location.search);
    const registration = params.get("registration");
    const providerSlug = params.get("provider")?.trim().toLocaleLowerCase("de-DE") ?? "";
    const pointId = Number(params.get("point"));
    const pointLink = /^[a-z0-9-]+$/.test(providerSlug)
        && Number.isSafeInteger(pointId)
        && pointId > 0
        ? { providerSlug, pointId }
        : null;

    const canonicalParams = new URLSearchParams();
    if (pointLink) {
        canonicalParams.set("provider", pointLink.providerSlug);
        canonicalParams.set("point", String(pointLink.pointId));
    }
    const canonicalSearch = canonicalParams.toString();
    const canonicalPath = isGoogleCallbackPath(window.location.pathname)
        ? getAppRootUrl().pathname
        : window.location.pathname;
    const canonicalUrl = `${canonicalPath}${canonicalSearch ? `?${canonicalSearch}` : ""}${window.location.hash}`;
    if (`${window.location.pathname}${window.location.search}${window.location.hash}` !== canonicalUrl) {
        window.history.replaceState(null, "", canonicalUrl);
    }

    return { registration, pointLink };
};

const initialNavigation = readInitialNavigation();

export {
    getAppRootUrl,
    initialNavigation,
    isGoogleCallbackPath
};
