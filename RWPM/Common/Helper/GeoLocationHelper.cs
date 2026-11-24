namespace RWPM.Common.Helper
{
    // Hỗ trợ tính khoảng cách giữa 2 tọa độ GPS theo công thức Haversine
    public static class GeoLocationHelper
    {
        // Bán kính trái đất xấp xỉ 6,371 km = 6,371,000 mét
        private const double EarthRadiusMeters = 6371000.0;

        // Tính khoảng cách giữa 2 tọa độ (mét)
        public static double CalculateDistanceMeters(double lat1, double lon1, double lat2, double lon2)
        {
            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);

            var radLat1 = ToRadians(lat1);
            var radLat2 = ToRadians(lat2);

            // Công thức haversine tính khoảng cách cung tròn trên mặt cầu
            var a = Math.Sin(dLat / 2.0) * Math.Sin(dLat / 2.0) +
                    Math.Sin(dLon / 2.0) * Math.Sin(dLon / 2.0) * Math.Cos(radLat1) * Math.Cos(radLat2);

            var c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));

            return EarthRadiusMeters * c;
        }

        // Đổi từ độ sang radian
        private static double ToRadians(double angleDegrees)
        {
            return angleDegrees * (Math.PI / 180.0);
        }
    }
}