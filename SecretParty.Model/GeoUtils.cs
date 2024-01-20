using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecretParty.Model
{
    public class GeoUtils
    {
        private const double EarthRadiusKm = 6371; // Radius of the Earth in kilometers

        private static Random random = new Random();

        public static double DegreesToRadians(double degrees)
        {
            return degrees * Math.PI / 180.0;
        }

        public static double RadiansToDegrees(double radians)
        {
            return radians * 180.0 / Math.PI;
        }

        public static double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            // Haversine formula to calculate the distance between two points
            double dLat = DegreesToRadians(lat2 - lat1);
            double dLon = DegreesToRadians(lon2 - lon1);
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2)) *
                       Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            double distance = EarthRadiusKm * c;
            return distance;
        }

        public static (double, double) GenerateRandomPoint(double centerLat, double centerLon, double maxDistanceKm)
        {
            double radiusKm = maxDistanceKm * Math.Sqrt(random.NextDouble());
            double randomAngle = random.NextDouble() * 2 * Math.PI;

            double randomLat = centerLat + (radiusKm / EarthRadiusKm) * Math.Sin(randomAngle);
            double randomLon = centerLon + (radiusKm / EarthRadiusKm) * Math.Cos(randomAngle);

            return (randomLat, randomLon);
        }
    }
}
