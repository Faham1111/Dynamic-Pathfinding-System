namespace SmartRoute.API.Models
{
    public class Node
    {
        public string Id { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public List<Edge> Edges { get; set; } = new List<Edge>();
    }

    public class Edge
    {
        public string To { get; set; }
        public double Weight { get; set; }
        public double Distance { get; set; }
        public string RoadName { get; set; }
        public double TrafficMultiplier { get; set; } = 1.0; // 1.0 = normal, 2.0 = heavy traffic
    }

    public class Graph
    {
        public Dictionary<string, Node> Nodes { get; set; } = new Dictionary<string, Node>();

        public void AddEdge(string from, string to, double distance, string roadName = "")
        {
            if (!Nodes.ContainsKey(from)) return;
            if (!Nodes.ContainsKey(to)) return;

            var edge = new Edge
            {
                To = to,
                Distance = distance,
                Weight = distance, // Initial weight = distance
                RoadName = roadName
            };

            Nodes[from].Edges.Add(edge);
        }

        public void UpdateTraffic(string roadName, double trafficMultiplier)
        {
            foreach (var node in Nodes.Values)
            {
                foreach (var edge in node.Edges.Where(e => e.RoadName == roadName))
                {
                    edge.TrafficMultiplier = trafficMultiplier;
                    edge.Weight = edge.Distance * trafficMultiplier;
                }
            }
        }
    }
}