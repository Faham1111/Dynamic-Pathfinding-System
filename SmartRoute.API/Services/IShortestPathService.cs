using SmartRoute.API.Models;

namespace SmartRoute.API.Services
{
    public interface IShortestPathService
    {
        Task<Graph> LoadGraphAsync();
        Task<RouteResponse> FindShortestPathAsync(RouteRequest request);
        Task RefreshTrafficDataAsync();
    }
}