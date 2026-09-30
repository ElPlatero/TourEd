using Api.Entities;

namespace Api.Managers;

public sealed record PointVisit(StampingPoint StampingPoint, UserVisit? Visit);
