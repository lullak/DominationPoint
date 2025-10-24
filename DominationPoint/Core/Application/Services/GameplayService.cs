using DominationPoint.Core.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace DominationPoint.Core.Application.Services
{
    public class GameplayService : IGameplayService
    {
        private readonly IApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public GameplayService(IApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<(bool Success, string Message)> CaptureControlPointAsync(int cpId, string userId, string numpadCode)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null || user.NumpadCode != numpadCode)
                return (false, "Invalid user or code.");

            var cp = await _context.ControlPoints.FindAsync(cpId);
            if (cp == null)
                return (false, "Control Point not found.");

            var activeGame = await _context.Games.FirstOrDefaultAsync(g => g.Status == GameStatus.Active);
            if (activeGame == null)
                return (false, "No game is currently active.");


            await LogGameEvent(activeGame.Id, cp.Id, EventType.Capture, userId, cp.ApplicationUserId);

            cp.Status = ControlPointStatus.Controlled;
            cp.ApplicationUserId = user.Id;

            await _context.SaveChangesAsync(default);
            return (true, $"Control Point {cp.Id} captured by team {user.UserName}.");
        }

        private async Task LogGameEvent(int gameId, int cpId, EventType type, string actingUserId, string? previousOwnerId)
        {
            var gameEvent = new GameEvent
            {
                GameId = gameId,
                ControlPointId = cpId,
                Type = type,
                Timestamp = DateTime.UtcNow,
                ActingUserId = actingUserId,
                PreviousOwnerUserId = previousOwnerId
            };
            _context.GameEvents.Add(gameEvent);
        }
    }
}