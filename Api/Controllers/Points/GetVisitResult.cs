using Api.Dto;

namespace Api.Controllers.Points;

public record GetVisitResult : VisitDto
{
    public GetVisitResult(VisitDto dto) : base(dto.Visited, dto.StampingPoint) { }
}
