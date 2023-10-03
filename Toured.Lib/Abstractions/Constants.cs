using System.Xml.Linq;

namespace TourEd.Lib.Abstractions;

public sealed class Constants
{
    public static class XmlNames
    {
        public const string Waypoint = "wpt";
        public const string Latitude = "lat";
        public const string Longitude = "lon";
        public const string ElementId = "ele";
        public const string Name = "name";
        public const string Description = "desc";
    }

    public static class ClaimsNames
    {
        public const string UserId = "userid";
        public const string UserEmail = "email";
    }
}
