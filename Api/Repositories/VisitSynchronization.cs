using Api.Entities;

namespace Api.Repositories;

/// <summary>Outcome of a conditional visit-state change: the current visit and whether a differing state won.</summary>
public sealed record VisitSynchronization(UserVisit? Visit, bool IsConflict);
