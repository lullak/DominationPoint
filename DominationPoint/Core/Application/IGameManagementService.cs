using DominationPoint.Core.Domain;

namespace DominationPoint.Core.Application
{
    public interface IGameManagementService
    {
        Task<List<Game>> GetAllGamesAsync();
        Task<Game?> GetGameByIdAsync(int id);
        Task CreateGameAsync(string name, DateTime startTime, DateTime endTime);
        Task StartGameAsync(int id);
        Task EndGameAsync(int id);
        Task ResetGameAsync(int id);
        Task AddParticipantAsync(int gameId, string userId);
        Task RemoveParticipantAsync(int gameId, string userId);
        Task<List<ApplicationUser>> GetParticipantsAsync(int gameId);
        Task<List<ApplicationUser>> GetNonParticipantsAsync(int gameId);
        Task<List<GameParticipant>> GetParticipantsWithIncludesAsync(int gameId);
    }
}
