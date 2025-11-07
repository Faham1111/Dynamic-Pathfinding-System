using Microsoft.AspNetCore.Mvc;
using SmartRoute.API.Models;
using SmartRoute.API.Services;

namespace SmartRoute.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RoutesController : ControllerBase
    {
        private readonly IShortestPathService _routeService;

        public RoutesController(IShortestPathService routeService)
        {
            _routeService = routeService;
        }

        [HttpGet]
        public async Task<ActionResult<RouteResponse>> GetRoute(
            double startLat, double startLng, double endLat, double endLng, bool avoidTraffic = true)
        {
            var request = new RouteRequest
            {
                StartLat = startLat,
                StartLng = startLng,
                EndLat = endLat,
                EndLng = endLng,
                AvoidTraffic = avoidTraffic
            };

            var result = await _routeService.FindShortestPathAsync(request);
            return Ok(result);
        }

        [HttpPost("calculate")]
        public async Task<ActionResult<RouteResponse>> CalculateRoute([FromBody] RouteRequest request)
        {
            var result = await _routeService.FindShortestPathAsync(request);
            return Ok(result);
        }
    }
}