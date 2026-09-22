using System;

namespace RWPM.Common.Helper
{
    public static class GeoLocationHelper
    {
        private const double EarthRadiusMeters = 6371000.0; // Bán kính trái đất (mét)

        /// <summary>
        /// Tính khoảng cách giữa 2 tọa độ GPS (Kinh độ, Vĩ độ) theo công thức Haversine (tính bằng mét).
        /// </summary>
        public static double CalculateDistanceMeters(double lat1, double lon1, double lat2, double lon2)
        {
            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);

            var radLat1 = ToRadians(lat1);
            var radLat2 = ToRadians(lat2);

            var a = Math.Sin(dLat / 2.0) * Math.Sin(dLat / 2.0) +
                    Math.Sin(dLon / 2.0) * Math.Sin(dLon / 2.0) * Math.Cos(radLat1) * Math.Cos(radLat2);

            var c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));

            return EarthRadiusMeters * c;
        }

        private static double ToRadians(double angleDegrees)
        {
            return (Math.PI / 180.0) * angleDegrees;
        }
    }
}
