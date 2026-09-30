using Api.Dto;
using Microsoft.AspNetCore.Routing.Constraints;

namespace Api.Controllers.Points;

public sealed record GetStampingPointsResponse(int OverallCount, IEnumerable<StampingPointDto> StampingPoints);
