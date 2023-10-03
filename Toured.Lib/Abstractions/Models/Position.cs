namespace TourEd.Lib.Abstractions.Models;

public record Position(decimal Longitude, decimal Latitude)
{
    public static decimal GetDistance(Position x, Position y)
    {
        static (double, double) GetLonLat(Position position) => (Convert.ToDouble(position.Latitude) * Math.PI / 180.0, Convert.ToDouble(position.Longitude) * Math.PI / 180.0);

        var (latX, lonX) = GetLonLat(x);
        var (latY, lonY) = GetLonLat(y);

        var tmp = Math.Pow(Math.Sin(Convert.ToDouble((latY - latX) / 2)), 2) +
                  Math.Cos(latX) * Math.Cos(latY) * Math.Pow(Math.Sin((lonY - lonX) / 2), 2);
        return Convert.ToDecimal(6376500.0 * (2 * Math.Atan2(Math.Sqrt(tmp), Math.Sqrt(1.0 - tmp))));
    }
}
