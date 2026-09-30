const SearchResultLimit = 30;
const VisitState = Object.freeze({
    unknown: "unknown",
    open: "open",
    visited: "visited"
});
const VisitFilter = Object.freeze({
    all: "all",
    open: "open",
    visited: "visited"
});
const VisitFilterOrder = [VisitFilter.all, VisitFilter.open, VisitFilter.visited];

export {
    SearchResultLimit,
    VisitFilter,
    VisitFilterOrder,
    VisitState
};
