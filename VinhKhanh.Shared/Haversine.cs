namespace VinhKhanh.Shared;

public static class Haversine
{
    private const double EarthRadiusMeters = 6_371_000;

    /// <summary>Returns distance in meters between two GPS coordinates.</summary>
    public static double Distance(double lat1, double lon1, double lat2, double lon2)
    {
        var dLat = ToRad(lat2 - lat1);
        var dLon = ToRad(lon2 - lon1);

        var a = Math.Pow(Math.Sin(dLat / 2), 2)
              + Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2))
              * Math.Pow(Math.Sin(dLon / 2), 2);

        return 2 * EarthRadiusMeters * Math.Asin(Math.Sqrt(a));
    }

    private static double ToRad(double deg) => deg * Math.PI / 180;
}
