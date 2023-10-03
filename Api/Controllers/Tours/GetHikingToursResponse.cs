using Api.Dto;

namespace Api.Controllers.Tours;

public record GetHikingToursResponse(int OverallCount, int StampingPointCount, IEnumerable<TourDto> HikingTours);
