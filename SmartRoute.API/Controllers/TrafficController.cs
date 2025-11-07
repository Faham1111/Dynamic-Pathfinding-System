using Microsoft.AspNetCore.Mvc;
using SmartRoute.API.Models;
using SmartRoute.API.Services;

namespace SmartRoute.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TrafficController : ControllerBase
    {
        private readonly ITrafficService _trafficService;

        public TrafficController(ITrafficService trafficService)
        {
            _trafficService = trafficService;
        }

        [HttpGet("current")]
        public async Task<ActionResult<List<TrafficData>>> GetCurrentTraffic()
        {
            var traffic = await _trafficService.GetCurrentTrafficAsync();
            return Ok(traffic);
        }

        [HttpPost("report")]
        public async Task<ActionResult> ReportTraffic([FromBody] TrafficUpdate trafficUpdate)
        {
            var success = await _trafficService.ReportTrafficAsync(trafficUpdate);

            if (success)
                return Ok(new { message = "Traffic reported successfully" });
            else
                return BadRequest(new { message = "Failed to report traffic" });
        }

        [HttpPost("update")]
        public async Task<ActionResult> UpdateTraffic([FromBody] TrafficUpdate update)
        {
            await _trafficService.UpdateTrafficAsync(update);
            return Ok(new { message = "Traffic updated successfully" });
        }
    }
}