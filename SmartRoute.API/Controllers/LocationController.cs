using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using SmartRoute.API.Hubs;
using SmartRoute.API.Models;
using SmartRoute.API.Services;

namespace SmartRoute.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LocationController : ControllerBase
    {
        private readonly ILocationTrackingService _locationService;
        private readonly ITrafficService _trafficService;
        private readonly IHubContext<LocationHub> _hubContext;
        private readonly ILogger<LocationController> _logger;

        public LocationController(
            ILocationTrackingService locationService,
            ITrafficService trafficService,
            IHubContext<LocationHub> hubContext,
            ILogger<LocationController> logger)
        {
            _locationService = locationService;
            _trafficService = trafficService;
            _hubContext = hubContext;
            _logger = logger;
        }

        [HttpPost("update")]
        public async Task<IActionResult> UpdateLocation([FromBody] LocationUpdateDto location)
        {
            try
            {
                await _locationService.UpdateUserLocationAsync(location);
                return Ok(new { message = "Location updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating location");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("{userId}/latest")]
        public async Task<IActionResult> GetLatestLocation(string userId)
        {
            try
            {
                var location = await _locationService.GetLatestLocationAsync(userId);
                if (location == null)
                    return NotFound(new { message = "No location found for user" });

                return Ok(location);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting location");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("{userId}/history")]
        public async Task<IActionResult> GetLocationHistory(
            string userId,
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null)
        {
            try
            {
                var fromDate = from ?? DateTime.UtcNow.AddHours(-24);
                var toDate = to ?? DateTime.UtcNow;

                var history = await _locationService.GetLocationHistoryAsync(userId, fromDate, toDate);
                return Ok(history);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting location history");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("{userId}/route/{routeId}/start")]
        public async Task<IActionResult> StartRoute(string userId, string routeId)
        {
            try
            {
                await _locationService.MarkRouteAsActiveAsync(userId, routeId);

                // Notify via SignalR
                await _hubContext.Clients.Group($"tracking_{userId}")
                    .SendAsync("RouteStarted", new { userId, routeId });

                return Ok(new { message = "Route started successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting route");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("{userId}/route/stop")]
        public async Task<IActionResult> StopRoute(string userId)
        {
            try
            {
                await _locationService.DeactivateRouteAsync(userId);

                await _hubContext.Clients.Group($"tracking_{userId}")
                    .SendAsync("RouteStopped", new { userId });

                return Ok(new { message = "Route stopped successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping route");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("{userId}/active-route")]
        public async Task<IActionResult> GetActiveRoute(string userId)
        {
            try
            {
                var route = await _locationService.GetActiveRouteAsync(userId);
                if (route == null)
                    return NotFound(new { message = "No active route found" });

                return Ok(route);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active route");
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }

    [ApiController]
    [Route("api/[controller]")]
    public class TrafficController : ControllerBase
    {
        private readonly ITrafficService _trafficService;
        private readonly IHubContext<LocationHub> _hubContext;
        private readonly ILogger<TrafficController> _logger;

        public TrafficController(
            ITrafficService trafficService,
            IHubContext<LocationHub> hubContext,
            ILogger<TrafficController> logger)
        {
            _trafficService = trafficService;
            _hubContext = hubContext;
            _logger = logger;
        }

        [HttpPost("report")]
        public async Task<IActionResult> ReportTraffic([FromBody] TrafficReportDto report)
        {
            try
            {
                var trafficReport = await _trafficService.CreateTrafficReportAsync(report);

                // Broadcast to all users in the area
                await _hubContext.Clients.All.SendAsync("TrafficReported", trafficReport);

                return Ok(trafficReport);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reporting traffic");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("active")]
        public async Task<IActionResult> GetActiveTraffic(
            [FromQuery] double lat,
            [FromQuery] double lon,
            [FromQuery] int radius = 5000)
        {
            try
            {
                var reports = await _trafficService.GetActiveTrafficReportsAsync(lat, lon, radius);
                return Ok(reports);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active traffic");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("{reportId}/vote")]
        public async Task<IActionResult> VoteOnReport(
            string reportId,
            [FromBody] VoteDto vote)
        {
            try
            {
                await _trafficService.VoteOnTrafficReportAsync(reportId, vote.UserId, vote.IsConfirming);
                return Ok(new { message = "Vote recorded" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error voting on traffic report");
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }

    public class VoteDto
    {
        public string UserId { get; set; }
        public bool IsConfirming { get; set; }
    }
}