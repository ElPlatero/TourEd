using Api.Dto;
using Api.Entities;

namespace Api.Controllers.Points;

public record GetVisitResult : VisitDto
{
    public GetVisitResult(VisitDto dto) : base(dto.IsVisited, dto.VisitedOn, dto.VisitedAt, dto.StampingPoint) { }
}
