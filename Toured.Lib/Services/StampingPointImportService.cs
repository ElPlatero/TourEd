using System.Globalization;
using TourEd.Lib.Abstractions.Interfaces.Services;
using TourEd.Lib.Abstractions.Models;

namespace TourEd.Lib.Services;

public static class AdapterExtensions
{
    public static StampingPoint CreateStampingPoint(this RawStampPoint rawStampPoint) => new(default, string.IsNullOrWhiteSpace(rawStampPoint.Name) ? rawStampPoint.Title.Trim('"', ' ') : rawStampPoint.Name.Trim('"', ' '), rawStampPoint.Longitude, rawStampPoint.Latitude, rawStampPoint.StampPointNumber, rawStampPoint.StampPointExtendedNumber, StampingProvider.TouringenId, rawStampPoint.Id.ToString(CultureInfo.InvariantCulture))
    {
        SeriesId = StampingSeries.TouringenStandardId
    };
}

public class HikingToursImportService : IImportService<HikingTour>
{
    public IEnumerable<HikingTour> Import(RawArea[]? inputData)
    {
        if (inputData == null)
        {
            yield break;
        }

        foreach (var hikingTour in inputData.SelectMany(p => p.Touren))
        {
            var newTour = new HikingTour(
                hikingTour.Id,
                hikingTour.Title,
                hikingTour.StartPointDescription,
                hikingTour.EndPointDescription,
                string.IsNullOrWhiteSpace(hikingTour.KomootLink) ? null : new Uri(hikingTour.KomootLink),
                hikingTour.IsKidsTour,
                hikingTour.IsCircularPath,
                hikingTour.IsLongDistanceTrail);
            newTour.StampingPoints = hikingTour.StampPoints.Select(p => new SortedStampingPoint(p.Positionsnummer) { StampingPointId = p.Id, Tour = newTour }).ToList();
            yield return newTour;
        }
    }
}
