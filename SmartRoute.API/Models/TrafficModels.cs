using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SmartRoute.API.Models
{
    public class TrafficData
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        [BsonElement("roadName")]
        public string RoadName { get; set; }

        [BsonElement("trafficLevel")]
        public TrafficLevel TrafficLevel { get; set; }

        [BsonElement("multiplier")]
        public double Multiplier { get; set; }

        [BsonElement("reportedAt")]
        public DateTime ReportedAt { get; set; }

        [BsonElement("coordinates")]
        public double[] Coordinates { get; set; } // [lng, lat]
    }

    public enum TrafficLevel
    {
        Light = 1,
        Moderate = 2,
        Heavy = 3,
        Blocked = 4
    }

    public class TrafficUpdate
    {
        public string RoadName { get; set; }
        public TrafficLevel TrafficLevel { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string ReportedBy { get; set; } = "System";
    }
}