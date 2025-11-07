using MongoDB.Driver;
using SmartRoute.API.Data;
using SmartRoute.API.Models;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmartRoute.API.Services
{
    public class OSMDataImporter
    {
        private readonly MongoDbContext _context;
        private readonly HttpClient _httpClient;

        public OSMDataImporter(MongoDbContext context)
        {
            _context = context;
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromMinutes(5);
        }

        public async Task ImportRajkotRoadsAsync()
        {
            try
            {
                // Check if roads already exist
                var existingRoads = await _context.Roads.CountDocumentsAsync(_ => true);
                if (existingRoads > 0)
                {
                    Console.WriteLine($"Roads already exist in database ({existingRoads} roads).");
                    Console.WriteLine("Skipping import - data already exists.");
                    return;
                }

                Console.WriteLine("Starting OSM data import for Rajkot...");
                Console.WriteLine("This may take a few minutes...");

                // Rajkot bounding box: (south, west, north, east)
                var overpassQuery = @"
[out:json][timeout:300];
(
  way[""highway""~""motorway|trunk|primary|secondary|tertiary|unclassified|residential|service|living_street""]
    (22.25,70.75,22.35,70.85);
);
out body;
>;
out skel qt;
";

                var overpassUrl = "https://overpass-api.de/api/interpreter";

                Console.WriteLine("Querying Overpass API...");
                var content = new StringContent(overpassQuery);
                var response = await _httpClient.PostAsync(overpassUrl, content);

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"ERROR: Failed to query Overpass API. Status: {response.StatusCode}");
                    Console.WriteLine("Falling back to sample data import...");
                    await ImportSampleDataAsync();
                    return;
                }

                var jsonResponse = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Received {jsonResponse.Length} bytes of data");

                // Parse OSM data
                var osmData = JsonSerializer.Deserialize<OSMResponse>(jsonResponse);

                if (osmData?.Elements == null || osmData.Elements.Count == 0)
                {
                    Console.WriteLine("No OSM data received. Importing sample data instead...");
                    await ImportSampleDataAsync();
                    return;
                }

                Console.WriteLine($"Processing {osmData.Elements.Count} OSM elements...");

                // Build node dictionary
                var nodes = osmData.Elements
                    .Where(e => e.Type == "node")
                    .ToDictionary(e => e.Id, e => e);

                // Process ways (roads)
                var roads = new List<RoadFeature>();
                var ways = osmData.Elements.Where(e => e.Type == "way").ToList();

                Console.WriteLine($"Found {ways.Count} roads to import...");

                foreach (var way in ways)
                {
                    if (way.Nodes == null || way.Nodes.Count < 2)
                        continue;

                    var coordinates = new List<double[]>();
                    bool hasAllNodes = true;

                    foreach (var nodeId in way.Nodes)
                    {
                        if (nodes.TryGetValue(nodeId, out var node))
                        {
                            coordinates.Add(new[] { node.Lon, node.Lat });
                        }
                        else
                        {
                            hasAllNodes = false;
                            break;
                        }
                    }

                    if (hasAllNodes && coordinates.Count >= 2)
                    {
                        var roadName = way.Tags?.GetValueOrDefault("name") ??
                                      way.Tags?.GetValueOrDefault("highway") ??
                                      "Unnamed Road";

                        roads.Add(new RoadFeature
                        {
                            Type = "Feature",
                            Geometry = new Geometry
                            {
                                Type = "LineString",
                                Coordinates = new[] { coordinates.ToArray() }
                            },
                            Properties = new SmartRoute.API.Models.Properties
                            {
                                Name = roadName,
                                Highway = way.Tags?.GetValueOrDefault("highway"),
                                MaxSpeed = way.Tags?.GetValueOrDefault("maxspeed"),
                                Lanes = way.Tags?.GetValueOrDefault("lanes")
                            }
                        });
                    }
                }

                if (roads.Count == 0)
                {
                    Console.WriteLine("No valid roads found in OSM data. Importing sample data...");
                    await ImportSampleDataAsync();
                    return;
                }

                Console.WriteLine($"Importing {roads.Count} roads into MongoDB...");

                // Import in batches to avoid memory issues
                var batchSize = 1000;
                for (int i = 0; i < roads.Count; i += batchSize)
                {
                    var batch = roads.Skip(i).Take(batchSize).ToList();
                    await _context.Roads.InsertManyAsync(batch);
                    Console.WriteLine($"Imported {Math.Min(i + batchSize, roads.Count)}/{roads.Count} roads...");
                }

                Console.WriteLine($"✓ Successfully imported {roads.Count} roads from OpenStreetMap!");
                Console.WriteLine("Road network is ready for routing.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR during OSM import: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                Console.WriteLine("Falling back to sample data...");
                await ImportSampleDataAsync();
            }
        }

        private async Task ImportSampleDataAsync()
        {
            Console.WriteLine("Importing sample road data as fallback...");

            var sampleRoads = new List<RoadFeature>
            {
                // Main road 1: Kalawad Road
                new RoadFeature
                {
                    Type = "Feature",
                    Geometry = new Geometry
                    {
                        Type = "LineString",
                        Coordinates = new[]
                        {
                            new[]
                            {
                                new[] { 70.8022, 22.3039 },
                                new[] { 70.8050, 22.3050 },
                                new[] { 70.8080, 22.3065 },
                                new[] { 70.8110, 22.3080 },
                                new[] { 70.8140, 22.3095 },
                                new[] { 70.8170, 22.3110 }
                            }
                        }
                    },
                    Properties = new SmartRoute.API.Models.Properties
                    {
                        Name = "Kalawad Road",
                        Highway = "primary",
                        MaxSpeed = "60",
                        Lanes = "4"
                    }
                },
                // Main road 2: 150 Feet Ring Road
                new RoadFeature
                {
                    Type = "Feature",
                    Geometry = new Geometry
                    {
                        Type = "LineString",
                        Coordinates = new[]
                        {
                            new[]
                            {
                                new[] { 70.8022, 22.3039 },
                                new[] { 70.8000, 22.3060 },
                                new[] { 70.7980, 22.3080 },
                                new[] { 70.7960, 22.3100 },
                                new[] { 70.7940, 22.3120 },
                                new[] { 70.7920, 22.3140 }
                            }
                        }
                    },
                    Properties = new SmartRoute.API.Models.Properties
                    {
                        Name = "150 Feet Ring Road",
                        Highway = "primary",
                        MaxSpeed = "60",
                        Lanes = "6"
                    }
                },
                // Connector 1
                new RoadFeature
                {
                    Type = "Feature",
                    Geometry = new Geometry
                    {
                        Type = "LineString",
                        Coordinates = new[]
                        {
                            new[]
                            {
                                new[] { 70.8110, 22.3080 },
                                new[] { 70.8090, 22.3100 },
                                new[] { 70.8070, 22.3120 },
                                new[] { 70.8050, 22.3140 }
                            }
                        }
                    },
                    Properties = new SmartRoute.API.Models.Properties
                    {
                        Name = "Connector Road 1",
                        Highway = "secondary",
                        MaxSpeed = "40",
                        Lanes = "2"
                    }
                },
                // Connector 2
                new RoadFeature
                {
                    Type = "Feature",
                    Geometry = new Geometry
                    {
                        Type = "LineString",
                        Coordinates = new[]
                        {
                            new[]
                            {
                                new[] { 70.8050, 22.3050 },
                                new[] { 70.8040, 22.3070 },
                                new[] { 70.8030, 22.3090 },
                                new[] { 70.8020, 22.3110 },
                                new[] { 70.8010, 22.3130 }
                            }
                        }
                    },
                    Properties = new SmartRoute.API.Models.Properties
                    {
                        Name = "Connector Road 2",
                        Highway = "secondary",
                        MaxSpeed = "40",
                        Lanes = "2"
                    }
                },
                // University Road
                new RoadFeature
                {
                    Type = "Feature",
                    Geometry = new Geometry
                    {
                        Type = "LineString",
                        Coordinates = new[]
                        {
                            new[]
                            {
                                new[] { 70.8140, 22.3095 },
                                new[] { 70.8160, 22.3105 },
                                new[] { 70.8180, 22.3115 },
                                new[] { 70.8200, 22.3125 }
                            }
                        }
                    },
                    Properties = new SmartRoute.API.Models.Properties
                    {
                        Name = "University Road",
                        Highway = "tertiary",
                        MaxSpeed = "50",
                        Lanes = "2"
                    }
                },
                // Yagnik Road
                new RoadFeature
                {
                    Type = "Feature",
                    Geometry = new Geometry
                    {
                        Type = "LineString",
                        Coordinates = new[]
                        {
                            new[]
                            {
                                new[] { 70.7940, 22.3120 },
                                new[] { 70.7970, 22.3130 },
                                new[] { 70.8000, 22.3140 },
                                new[] { 70.8030, 22.3150 }
                            }
                        }
                    },
                    Properties = new SmartRoute.API.Models.Properties
                    {
                        Name = "Yagnik Road",
                        Highway = "secondary",
                        MaxSpeed = "50",
                        Lanes = "2"
                    }
                },
                // Cross connector
                new RoadFeature
                {
                    Type = "Feature",
                    Geometry = new Geometry
                    {
                        Type = "LineString",
                        Coordinates = new[]
                        {
                            new[]
                            {
                                new[] { 70.7960, 22.3100 },
                                new[] { 70.8000, 22.3100 },
                                new[] { 70.8040, 22.3100 },
                                new[] { 70.8080, 22.3100 },
                                new[] { 70.8120, 22.3100 }
                            }
                        }
                    },
                    Properties = new SmartRoute.API.Models.Properties
                    {
                        Name = "Cross Road",
                        Highway = "tertiary",
                        MaxSpeed = "40",
                        Lanes = "2"
                    }
                },
                // Vertical connector
                new RoadFeature
                {
                    Type = "Feature",
                    Geometry = new Geometry
                    {
                        Type = "LineString",
                        Coordinates = new[]
                        {
                            new[]
                            {
                                new[] { 70.8080, 22.3065 },
                                new[] { 70.8080, 22.3085 },
                                new[] { 70.8080, 22.3105 },
                                new[] { 70.8080, 22.3125 }
                            }
                        }
                    },
                    Properties = new SmartRoute.API.Models.Properties
                    {
                        Name = "North-South Road",
                        Highway = "tertiary",
                        MaxSpeed = "40",
                        Lanes = "2"
                    }
                }
            };

            await _context.Roads.InsertManyAsync(sampleRoads);
            Console.WriteLine($"✓ Imported {sampleRoads.Count} sample roads");
            Console.WriteLine("Sample road network is ready. For production, real OSM data is recommended.");
        }
    }

    // OSM Response models
    public class OSMResponse
    {
        [JsonPropertyName("elements")]
        public List<OSMElement> Elements { get; set; }
    }

    public class OSMElement
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("lat")]
        public double Lat { get; set; }

        [JsonPropertyName("lon")]
        public double Lon { get; set; }

        [JsonPropertyName("nodes")]
        public List<long> Nodes { get; set; }

        [JsonPropertyName("tags")]
        public Dictionary<string, string> Tags { get; set; }
    }
}