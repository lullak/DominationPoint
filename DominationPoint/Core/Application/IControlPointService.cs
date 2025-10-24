using DominationPoint.Core.Domain;

namespace DominationPoint.Core.Application
{
    public interface IControlPointService
    {

        Task<List<ControlPoint>> GetControlPointsForGameAsync(int gameId);
        Task DeleteControlPointAsync(int cpId);
        Task UpdateControlPointStateAsync(int cpId, string? ownerId);
        Task ToggleControlPointMarkerAsync(int gameId, int x, int y, bool isCp);
    }
}
