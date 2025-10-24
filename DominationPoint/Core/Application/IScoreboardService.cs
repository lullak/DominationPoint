using DominationPoint.Models;

namespace DominationPoint.Core.Application
{
    public interface IScoreboardService
    {
        Task<ScoreboardViewModel> CalculateScoreboardAsync(int gameId);
        Task<ScoreboardViewModel> GetSavedScoreboardAsync(int gameId);
    }
}
