// Add this file to SmartRoute.API/Hubs/LocationHub.cs

using Microsoft.AspNetCore.SignalR;
using SmartRoute.API.Services;
using SmartRoute.API.Models;

namespace SmartRoute.API.Hubs
{
    public class LocationHub : Hub
    {
        private readonly ILocationTrackingService _locationService;
        private readonly ITrafficService _trafficService;
        private readonly IShortestPathService _routeService;

        public LocationHub(
            ILocationTrackingService locationService,
            ITrafficService trafficService,
            IShortestPathService routeService)
        {
            _locationService = locationService;
            _trafficService = trafficService;
            _routeService = routeService;
        }

        public override async Task OnConnectedAsync()
        {
            var userId = Context.GetHttpContext()?.Request.Query["userId"].ToString();
            if (!string.IsNullOrEmpty(userId))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
            }
            await base.OnConnectedAsync();
        }

        public async Task SendLocationUpdate(LocationUpdateDto location)
        {
            try
            {
                // Save location
                await _locationService.UpdateUserLocationAsync(location);

                // Broadcast to tracking group
                await Clients.Group($"tracking_{location.UserId}")
                    .SendAsync("ReceiveLocationUpdate", location);

                // Check if user is on an active route
                var activeRoute = await _locationService.GetActiveRouteAsync(location.UserId);
                if (activeRoute != null)
                {
                    // Check for traffic on current route
                    var hasTrafficAhead = await _trafficService.CheckTrafficOnRouteAsync(
                        activeRoute.RouteId,
                        location.Latitude,
                        location.Longitude
                    );

                    if (hasTrafficAhead)
                    {
                        // Recalculate route avoiding traffic
                        var newRoute = await _routeService.CalculateAlternativeRouteAsync(
                            location.Latitude,
                            location.Longitude,
                            activeRoute.DestinationLat,
                            activeRoute.DestinationLon,
                            activeRoute.RouteId
                        );

                        if (newRoute != null && newRoute.TotalDistance < activeRoute.RemainingDistance)
                        {
                            // Send route update notification
                            await Clients.User(location.UserId).SendAsync("RouteRecalculated", new
                            {
                                Reason = "Traffic detected ahead",
                                NewRoute = newRoute,
                                TrafficInfo = hasTrafficAhead
                            });

                            // Update active route
                            await _locationService.UpdateActiveRouteAsync(location.UserId, newRoute);
                        }
                    }

                    // Calculate route progress
                    var progress = await CalculateRouteProgress(location, activeRoute);
                    await Clients.User(location.UserId).SendAsync("RouteProgress", progress);
                }
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("Error", new { message = ex.Message });
            }
        }

        public async Task StartTracking(string userId, string routeId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"tracking_{userId}");
            await Groups.AddToGroupAsync(Context.ConnectionId, $"route_{routeId}");

            await _locationService.MarkRouteAsActiveAsync(userId, routeId);

            await Clients.Caller.SendAsync("TrackingStarted", new { userId, routeId });
        }

        public async Task StopTracking(string userId)
        {
            await _locationService.DeactivateRouteAsync(userId);
            await Clients.Caller.SendAsync("TrackingStopped");
        }

        public async Task ReportTraffic(TrafficReportDto trafficReport)
        {
            // Save traffic report
            await _trafficService.CreateTrafficReportAsync(trafficReport);

            // Find all users affected by this traffic
            var affectedUsers = await _locationService.GetUsersNearLocationAsync(
                trafficReport.Latitude,
                trafficReport.Longitude,
                5000 // 5km radius
            );

            // Notify affected users
            foreach (var user in affectedUsers)
            {
                await Clients.User(user.UserId).SendAsync("TrafficAlert", trafficReport);

                // Trigger route recalculation for affected users
                var location = await _locationService.GetLatestLocationAsync(user.UserId);
                if (location != null)
                {
                    await SendLocationUpdate(location);
                }
            }
        }

        private async Task<RouteProgressDto> CalculateRouteProgress(
            LocationUpdateDto location,
            ActiveRouteDto route)
        {
            var distanceCovered = CalculateDistance(
                route.StartLat, route.StartLon,
                location.Latitude, location.Longitude
            );

            var distanceRemaining = route.TotalDistance - distanceCovered;
            var percentComplete = (distanceCovered / route.TotalDistance) * 100;

            // Check if reached waypoints
            var currentWaypoint = await CheckWaypointReached(location, route);

            return new RouteProgressDto
            {
                RouteId = route.RouteId,
                UserId = location.UserId,
                DistanceCovered = distanceCovered,
                DistanceRemaining = distanceRemaining,
                PercentComplete = percentComplete,
                CurrentWaypointIndex = currentWaypoint,
                EstimatedTimeRemaining = CalculateETA(distanceRemaining, location.Speed ?? 0)
            };
        }

        private async Task<int> CheckWaypointReached(LocationUpdateDto location, ActiveRouteDto route)
        {
            var waypoints = await _routeService.GetRouteWaypointsAsync(route.RouteId);

            for (int i = 0; i < waypoints.Count; i++)
            {
                var distance = CalculateDistance(
                    location.Latitude, location.Longitude,
                    waypoints[i].Latitude, waypoints[i].Longitude
                );

                if (distance <= 50) // 50 meters threshold
                {
                    await Clients.User(location.UserId).SendAsync("WaypointReached", new
                    {
                        WaypointIndex = i,
                        WaypointName = waypoints[i].Name,
                        NextWaypoint = i < waypoints.Count - 1 ? waypoints[i + 1] : null
                    });
                    return i;
                }
            }

            return route.CurrentWaypointIndex;
        }

        private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            var R = 6371000; // Earth radius in meters
            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        private double ToRadians(double degrees) => degrees * Math.PI / 180;

        private TimeSpan CalculateETA(double distanceMeters, double speedMps)
        {
            if (speedMps <= 0) speedMps = 13.89; // Default 50 km/h
            var seconds = distanceMeters / speedMps;
            return TimeSpan.FromSeconds(seconds);
        }
    }
}