// The mutually exclusive progress, account, search and provider flyouts.
import { elements } from "./dom.js";
import { renderSearchResults } from "./points.js";
import { updateProgressSummaryAria } from "./providers.js";

const closeProgressMenu = (restoreFocus = false) => {
    elements.progressPanel.hidden = true;
    elements.progressButton.setAttribute("aria-expanded", "false");
    updateProgressSummaryAria();
    if (restoreFocus) {
        elements.progressButton.focus({ preventScroll: true });
    }
};

const toggleProgressMenu = () => {
    const opening = elements.progressPanel.hidden;
    if (opening) {
        closeSearchMenu();
        closeProviderMenu();
        closeAccountMenu();
    }
    elements.progressPanel.hidden = !opening;
    elements.progressButton.setAttribute("aria-expanded", opening.toString());
    updateProgressSummaryAria();
    if (opening) {
        const firstButton = elements.progressPanel.querySelector("button:not([hidden])");
        firstButton?.focus({ preventScroll: true });
    }
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
        closeProgressMenu();
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
        closeProgressMenu();
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
        closeProgressMenu();
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

export {
    closeAccountMenu,
    closeProgressMenu,
    closeProviderMenu,
    closeSearchMenu,
    toggleAccountMenu,
    toggleProgressMenu,
    toggleProviderMenu,
    toggleSearchMenu
};
