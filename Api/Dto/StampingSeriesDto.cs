using Api.Entities;

namespace Api.Dto;

public sealed record StampingSeriesDto(string Slug, string Name, bool IsTemporary)
{
    public static StampingSeriesDto Create(StampingSeries series) => new(series.Slug, series.Name, series.IsTemporary);
}
