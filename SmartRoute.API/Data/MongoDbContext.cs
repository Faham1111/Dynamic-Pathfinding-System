using MongoDB.Driver;
using SmartRoute.API.Models;

namespace SmartRoute.API.Data
{
    public class MongoDbContext
    {
        private readonly IMongoDatabase _database;

        public MongoDbContext(IConfiguration configuration)
        {
            var client = new MongoClient(configuration.GetConnectionString("MongoDB"));
            _database = client.GetDatabase("smart_route_db");
        }

        public IMongoCollection<RoadFeature> Roads => _database.GetCollection<RoadFeature>("roads");
        public IMongoCollection<TrafficData> Traffic => _database.GetCollection<TrafficData>("traffic");
    }
}