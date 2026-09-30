export const SearchResultLimit = 30;
export const VisitState = Object.freeze({
    unknown: "unknown",
    open: "open",
    visited: "visited"
});
export const VisitFilter = Object.freeze({
    all: "all",
    open: "open",
    visited: "visited"
});
export const VisitFilterOrder = [VisitFilter.all, VisitFilter.open, VisitFilter.visited];
