using SmartRoute.API.Models;

namespace SmartRoute.API.Services
{
    public interface ITrafficService
    {
        Task<List<TrafficData>> GetCurrentTrafficAsync();
        Task UpdateTrafficAsync(TrafficUpdate update);
        Task<bool> ReportTrafficAsync(TrafficUpdate trafficReport);
    }
}