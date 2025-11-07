namespace SmartRoute.API.Models
{
    public class RouteRequest
    {
        public double StartLat { get; set; }
        public double StartLng { get; set; }
        public double EndLat { get; set; }
        public double EndLng { get; set; }
        public bool AvoidTraffic { get; set; } = true;
    }

    public class RouteResponse
    {
        public List<RoutePoint> Path { get; set; } = new List<RoutePoint>();
        public double TotalDistance { get; set; }
        public double EstimatedTime { get; set; } // in minutes
        public List<string> Instructions { get; set; } = new List<string>();
        public bool HasTrafficDetours { get; set; }
    }

    public class RoutePoint
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }
}