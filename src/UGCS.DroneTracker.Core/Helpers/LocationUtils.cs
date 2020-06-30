using System;

namespace UGCS.DroneTracker.Core.Helpers
{
    public static class LocationUtils
    {
        public const double HALF_PI = Math.PI / 2d;
        public const double PI2 = Math.PI * 2d;

        public const double RADIANS_TO_DEGREES = 180 / Math.PI;
        public const double DEGREES_TO_RADIANS = Math.PI / 180;

        public const double RADIANS_TO_SECONDS = 360 * 60 * 60 / PI2;


        public static Tuple<int, int, float> RadiansToFullDegrees(double radians)
        {
            var totalSeconds = radians * RADIANS_TO_SECONDS;
            var seconds = (float)Math.Round(totalSeconds % 60, 2);
            var minutes = ((int)totalSeconds / 60) % 60;
            var degrees = (int)totalSeconds / (60 * 60);
            return new Tuple<int, int, float>(degrees, minutes, seconds);
        }

        public static string DegreesToString(Tuple<int, int, float> degrees)
        {
            return $"{degrees.Item1}\u00B0{degrees.Item2}'{degrees.Item3}\"";
        }

        public static double GetAzimuthBetweenCoordinate(double lat1, double long1, double lat2, double long2)
        {
            //https://www.movable-type.co.uk/scripts/latlong.html
            var dLon = long2 - long1;

            var y = Math.Sin(dLon) * Math.Cos(lat2);
            var x = Math.Cos(lat1) * Math.Sin(lat2) -
                    Math.Sin(lat1) * Math.Cos(lat2) * Math.Cos(dLon);

            var azimuthRad = Math.Atan2(y, x);

            var azimuthDeg = azimuthRad * LocationUtils.RADIANS_TO_DEGREES;

            azimuthDeg = (azimuthDeg + 360) % 360;

            return azimuthDeg;
        }

        public static double GetDistance(double lat1, double long1, double lat2, double long2)
        {
            // https://www.movable-type.co.uk/scripts/latlong.html
            //const R = 6371e3; // meters
            //const φ1 = lat1 * Math.PI / 180; // φ, λ in radians
            //const φ2 = lat2 * Math.PI / 180;
            //const Δφ = (lat2 - lat1) * Math.PI / 180;
            //const Δλ = (lon2 - lon1) * Math.PI / 180;

            //const a = Math.sin(Δφ / 2) * Math.sin(Δφ / 2) +
            //    Math.cos(φ1) * Math.cos(φ2) *
            //    Math.sin(Δλ / 2) * Math.sin(Δλ / 2);
            //const c = 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));

            //const d = R * c; // in meters

            double R = 6371000d;
            var lat1Rad = lat1;
            var lat2Rad = lat2;
            var deltaLatRad = (lat2 - lat1);
            var deltaLonRad = (long2 - long1);

            var alpha = Math.Sin(deltaLatRad / 2d) * Math.Sin(deltaLatRad / 2d) +
                        Math.Cos(lat1Rad) * Math.Cos(lat2Rad) * Math.Sin(deltaLonRad / 2d) * Math.Sin(deltaLonRad / 2d);
            var c = 2 * Math.Atan2(Math.Sqrt(alpha), Math.Sqrt(1 - alpha));

            var distance = R * c;

            return distance;
        }
    }
}