using SmartRoute.API.Data;
using SmartRoute.API.Models;
using MongoDB.Driver;
using System.Collections.Generic;

namespace SmartRoute.API.Services
{
    public class ShortestPathService : IShortestPathService
    {
        private readonly MongoDbContext _context;
        private readonly ITrafficService _trafficService;
        private Graph _graph;
        private readonly object _graphLock = new object();
        private Task _graphBuildTask;

        public ShortestPathService(MongoDbContext context, ITrafficService trafficService)
        {
            _context = context;
            _trafficService = trafficService;
        }

        public async Task<Graph> LoadGraphAsync()
        {
            if (_graph == null)
            {
                lock (_graphLock)
                {
                    if (_graph == null)
                    {
                        _graph = new Graph();
                        _graphBuildTask = BuildGraphFromGeoJson();
                    }
                }
            }

            if (_graphBuildTask != null)
            {
                await _graphBuildTask;
            }

            return _graph;
        }

        private async Task BuildGraphFromGeoJson()
        {
            try
            {
                var roads = await _context.Roads.Find(_ => true).ToListAsync();

                Console.WriteLine($"Building graph from {roads.Count} roads...");

                if (roads.Count == 0)
                {
                    Console.WriteLine("WARNING: No roads found in database! Graph will be empty.");
                    _graph.Nodes = new Dictionary<string, Node>();
                    return;
                }

                var nodeMap = new Dictionary<string, Node>();

                foreach (var road in roads)
                {
                    if (road.Geometry.Type == "LineString" || road.Geometry.Type == "MultiLineString")
                    {
                        var coordinates = road.Geometry.Type == "LineString"
                            ? new[] { road.Geometry.Coordinates[0] }
                            : road.Geometry.Coordinates;

                        foreach (var lineString in coordinates)
                        {
                            for (int i = 0; i < lineString.Length - 1; i++)
                            {
                                var fromCoord = lineString[i];
                                var toCoord = lineString[i + 1];

                                var fromId = $"{fromCoord[1]:F6},{fromCoord[0]:F6}";
                                var toId = $"{toCoord[1]:F6},{toCoord[0]:F6}";

                                if (!nodeMap.ContainsKey(fromId))
                                {
                                    nodeMap[fromId] = new Node
                                    {
                                        Id = fromId,
                                        Latitude = fromCoord[1],
                                        Longitude = fromCoord[0]
                                    };
                                }

                                if (!nodeMap.ContainsKey(toId))
                                {
                                    nodeMap[toId] = new Node
                                    {
                                        Id = toId,
                                        Latitude = toCoord[1],
                                        Longitude = toCoord[0]
                                    };
                                }

                                var distance = CalculateDistance(fromCoord[1], fromCoord[0], toCoord[1], toCoord[0]);

                                nodeMap[fromId].Edges.Add(new Edge
                                {
                                    To = toId,
                                    Distance = distance,
                                    Weight = distance,
                                    RoadName = road.Properties?.Name ?? "Unknown Road"
                                });

                                nodeMap[toId].Edges.Add(new Edge
                                {
                                    To = fromId,
                                    Distance = distance,
                                    Weight = distance,
                                    RoadName = road.Properties?.Name ?? "Unknown Road"
                                });
                            }
                        }
                    }
                }

                _graph.Nodes = nodeMap;
                Console.WriteLine($"✓ Graph built successfully with {nodeMap.Count} nodes");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR building graph: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                _graph.Nodes = new Dictionary<string, Node>();
            }
        }

        public async Task<RouteResponse> FindShortestPathAsync(RouteRequest request)
        {
            try
            {
                await LoadGraphAsync();
                await RefreshTrafficDataAsync();

                Console.WriteLine($"Finding route from ({request.StartLat}, {request.StartLng}) to ({request.EndLat}, {request.EndLng})");
                Console.WriteLine($"Graph has {_graph.Nodes.Count} nodes");

                // If graph is empty, return a simple straight line route for testing
                if (_graph.Nodes.Count == 0)
                {
                    Console.WriteLine("WARNING: Graph is empty! Returning simple straight-line route for testing.");
                    return CreateSimpleRoute(request.StartLat, request.StartLng, request.EndLat, request.EndLng);
                }

                var startNode = FindNearestNode(request.StartLat, request.StartLng);
                var endNode = FindNearestNode(request.EndLat, request.EndLng);

                Console.WriteLine($"Start node: {startNode?.Id}, End node: {endNode?.Id}");

                if (startNode == null || endNode == null)
                {
                    Console.WriteLine("Could not find start or end node! Returning simple route.");
                    return CreateSimpleRoute(request.StartLat, request.StartLng, request.EndLat, request.EndLng);
                }

                var path = DijkstraShortestPath(startNode.Id, endNode.Id);

                Console.WriteLine($"Path found with {path.Count} nodes");

                if (path.Count == 0)
                {
                    Console.WriteLine("No path found! Returning simple route.");
                    return CreateSimpleRoute(request.StartLat, request.StartLng, request.EndLat, request.EndLng);
                }

                var routeResponse = new RouteResponse();
                var totalDistance = 0.0;
                var estimatedTime = 0.0;

                foreach (var nodeId in path)
                {
                    if (_graph.Nodes.TryGetValue(nodeId, out var node))
                    {
                        routeResponse.Path.Add(new RoutePoint
                        {
                            Latitude = node.Latitude,
                            Longitude = node.Longitude
                        });
                    }
                }

                for (int i = 0; i < path.Count - 1; i++)
                {
                    var currentNode = _graph.Nodes[path[i]];
                    var edge = currentNode.Edges.FirstOrDefault(e => e.To == path[i + 1]);
                    if (edge != null)
                    {
                        totalDistance += edge.Distance;
                        estimatedTime += (edge.Distance / 40.0) * 60 * edge.TrafficMultiplier;

                        if (edge.TrafficMultiplier > 1.2)
                        {
                            routeResponse.HasTrafficDetours = true;
                        }
                    }
                }

                routeResponse.TotalDistance = totalDistance;
                routeResponse.EstimatedTime = estimatedTime;
                routeResponse.Instructions = GenerateInstructions(path);

                Console.WriteLine($"✓ Route calculated: {totalDistance:F2}km, {estimatedTime:F0}min, {routeResponse.Path.Count} points");

                return routeResponse;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR in FindShortestPathAsync: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");

                // Return simple route as fallback
                return CreateSimpleRoute(request.StartLat, request.StartLng, request.EndLat, request.EndLng);
            }
        }

        private RouteResponse CreateSimpleRoute(double startLat, double startLng, double endLat, double endLng)
        {
            var response = new RouteResponse();

            // Create 10 intermediate points for a smooth line
            for (int i = 0; i <= 10; i++)
            {
                double ratio = i / 10.0;
                response.Path.Add(new RoutePoint
                {
                    Latitude = startLat + (endLat - startLat) * ratio,
                    Longitude = startLng + (endLng - startLng) * ratio
                });
            }

            var distance = CalculateDistance(startLat, startLng, endLat, endLng);
            response.TotalDistance = distance;
            response.EstimatedTime = (distance / 40.0) * 60; // 40 km/h average
            response.Instructions = new List<string>
            {
                "Start your journey",
                "Head towards destination",
                "You have arrived at your destination"
            };
            response.HasTrafficDetours = false;

            Console.WriteLine($"Created simple fallback route: {distance:F2}km");

            return response;
        }

        private List<string> DijkstraShortestPath(string startNode, string endNode)
        {
            var distances = new Dictionary<string, double>();
            var previous = new Dictionary<string, string>();
            var queue = new PriorityQueue<string, double>();
            var visited = new HashSet<string>();

            foreach (var nodeId in _graph.Nodes.Keys)
            {
                distances[nodeId] = double.MaxValue;
                previous[nodeId] = null;
            }

            distances[startNode] = 0;
            queue.Enqueue(startNode, 0);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();

                if (visited.Contains(current)) continue;
                visited.Add(current);

                if (current == endNode) break;

                var currentNode = _graph.Nodes[current];
                foreach (var edge in currentNode.Edges)
                {
                    if (visited.Contains(edge.To)) continue;

                    double newDist = distances[current] + edge.Weight;
                    if (newDist < distances[edge.To])
                    {
                        distances[edge.To] = newDist;
                        previous[edge.To] = current;
                        queue.Enqueue(edge.To, newDist);
                    }
                }
            }

            var path = new List<string>();
            string u = endNode;
            while (u != null)
            {
                path.Insert(0, u);
                u = previous[u];
            }

            return path.Count > 1 ? path : new List<string>();
        }

        private Node FindNearestNode(double lat, double lng)
        {
            Node nearest = null;
            double minDistance = double.MaxValue;

            foreach (var node in _graph.Nodes.Values)
            {
                var distance = CalculateDistance(lat, lng, node.Latitude, node.Longitude);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearest = node;
                }
            }

            return nearest;
        }

        private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371;
            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        private double ToRadians(double degrees) => degrees * Math.PI / 180;

        public async Task RefreshTrafficDataAsync()
        {
            try
            {
                var trafficData = await _trafficService.GetCurrentTrafficAsync();

                foreach (var traffic in trafficData)
                {
                    _graph.UpdateTraffic(traffic.RoadName, traffic.Multiplier);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR refreshing traffic: {ex.Message}");
            }
        }

        private List<string> GenerateInstructions(List<string> path)
        {
            var instructions = new List<string>();

            if (path.Count < 2) return instructions;

            instructions.Add("Start your journey");

            for (int i = 1; i < path.Count - 1; i++)
            {
                var currentNode = _graph.Nodes[path[i]];
                var nextEdge = currentNode.Edges.FirstOrDefault(e => e.To == path[i + 1]);
                if (nextEdge != null && !string.IsNullOrEmpty(nextEdge.RoadName))
                {
                    instructions.Add($"Continue on {nextEdge.RoadName}");
                }
            }

            instructions.Add("You have arrived at your destination");
            return instructions;
        }
    }
}