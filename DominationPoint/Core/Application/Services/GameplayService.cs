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
            // Validate control point first
            var cp = await _context.ControlPoints.FindAsync(cpId);
            if (cp == null)
                return (false, "Control Point not found.");

            // Validate userId and numpadCode
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(numpadCode))
                return (false, "Invalid user or code.");

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null || string.IsNullOrEmpty(user.NumpadCode) || user.NumpadCode != numpadCode)
                return (false, "Invalid user or code.");

            // Find all active games, pick the first
            var activeGame = await _context.Games.FirstOrDefaultAsync(g => g.Status == GameStatus.Active);
            if (activeGame == null)
                return (false, "No game is currently active.");

            // Always log event, even if recapturing own point
            var previousOwnerId = cp.ApplicationUserId;
            var gameEvent = new GameEvent
            {
                GameId = activeGame.Id,
                ControlPointId = cp.Id,
                Type = EventType.Capture,
                Timestamp = DateTime.UtcNow,
                ActingUserId = userId,
                PreviousOwnerUserId = previousOwnerId
            };
            _context.GameEvents.Add(gameEvent);

            cp.Status = ControlPointStatus.Controlled;
            cp.ApplicationUserId = user.Id;

            await _context.SaveChangesAsync(default);
            return (true, $"Control Point {cp.Id} captured by team {user.UserName}.");
        }
    }
}