using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SmartRoute.API.Models
{
    public class RoadFeature
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        [BsonElement("type")]
        public string Type { get; set; } = "Feature";

        [BsonElement("geometry")]
        public Geometry Geometry { get; set; }

        [BsonElement("properties")]
        public Properties Properties { get; set; }
    }

    public class Geometry
    {
        [BsonElement("type")]
        public string Type { get; set; }

        [BsonElement("coordinates")]
        public double[][][] Coordinates { get; set; } // For MultiLineString
    }

    public class Properties
    {
        [BsonElement("name")]
        public string Name { get; set; }

        [BsonElement("highway")]
        public string Highway { get; set; }

        [BsonElement("maxspeed")]
        public string MaxSpeed { get; set; }

        [BsonElement("lanes")]
        public string Lanes { get; set; }
    }
}
