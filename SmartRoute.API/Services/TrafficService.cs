using MongoDB.Driver;
using SmartRoute.API.Data;
using SmartRoute.API.Models;

namespace SmartRoute.API.Services
{
    public class TrafficService : ITrafficService
    {
        private readonly MongoDbContext _context;

        public TrafficService(MongoDbContext context)
        {
            _context = context;
        }

        public async Task<List<TrafficData>> GetCurrentTrafficAsync()
        {
            var cutoffTime = DateTime.UtcNow.AddMinutes(-30); // Only consider traffic data from last 30 minutes

            return await _context.Traffic
                .Find(t => t.ReportedAt > cutoffTime)
                .ToListAsync();
        }

        public async Task UpdateTrafficAsync(TrafficUpdate update)
        {
            var multiplier = GetMultiplierFromTrafficLevel(update.TrafficLevel);

            var trafficData = new TrafficData
            {
                RoadName = update.RoadName,
                TrafficLevel = update.TrafficLevel,
                Multiplier = multiplier,
                ReportedAt = DateTime.UtcNow,
                Coordinates = new[] { update.Longitude, update.Latitude }
            };

            await _context.Traffic.InsertOneAsync(trafficData);
        }

        public async Task<bool> ReportTrafficAsync(TrafficUpdate trafficReport)
        {
            try
            {
                await UpdateTrafficAsync(trafficReport);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private double GetMultiplierFromTrafficLevel(TrafficLevel level)
        {
            return level switch
            {
                TrafficLevel.Light => 1.2,
                TrafficLevel.Moderate => 1.8,
                TrafficLevel.Heavy => 2.5,
                TrafficLevel.Blocked => 10.0, // Effectively avoids the road
                _ => 1.0
            };
        }
    }
}