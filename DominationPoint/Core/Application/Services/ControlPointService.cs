using DominationPoint.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace DominationPoint.Core.Application.Services
{
    public class ControlPointService : IControlPointService
    {
        private readonly IApplicationDbContext _context;

        public ControlPointService(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<ControlPoint>> GetControlPointsForGameAsync(int gameId)
        {
            return await _context.ControlPoints
                .Include(cp => cp.ApplicationUser)
                .Where(cp => cp.GameId == gameId)
                .ToListAsync();
        }


        public async Task DeleteControlPointAsync(int cpId)
        {
            var cpToDelete = await _context.ControlPoints.FindAsync(cpId);
            if (cpToDelete != null)
            {
                _context.ControlPoints.Remove(cpToDelete);
                await _context.SaveChangesAsync(default);
            }
        }

        public async Task UpdateControlPointStateAsync(int cpId, string? ownerId)
        {
            var cp = await _context.ControlPoints.FindAsync(cpId);

            // If the control point doesn't exist, do nothing.
            if (cp == null)
            {
                return;
            }

            // This logic is moved from the old method.
            var previousOwnerId = cp.ApplicationUserId;
            var newOwnerId = string.IsNullOrEmpty(ownerId) ? null : ownerId;

            // Only create a game event if the owner has actually changed.
            if (previousOwnerId != newOwnerId)
            {
                // Log a "Capture" event. This is what generates points!
                _context.GameEvents.Add(new GameEvent
                {
                    GameId = cp.GameId,
                    ControlPointId = cp.Id,
                    Type = EventType.Capture,
                    Timestamp = DateTime.UtcNow,
                    ActingUserId = newOwnerId, // The team that captured it
                    PreviousOwnerUserId = previousOwnerId // The team that lost it
                });

                // Update the control point itself
                cp.ApplicationUserId = newOwnerId;
                cp.Status = string.IsNullOrEmpty(newOwnerId)
                    ? ControlPointStatus.Inactive
                    : ControlPointStatus.Controlled;

                await _context.SaveChangesAsync(default);
            }
        }


        public async Task ToggleControlPointMarkerAsync(int gameId, int x, int y, bool isCp)
        {
            var cp = await _context.ControlPoints
                .FirstOrDefaultAsync(p => p.GameId == gameId && p.PositionX == x && p.PositionY == y);

            if (isCp)
            {

                if (cp == null)
                {
                    var newCp = new ControlPoint
                    {
                        GameId = gameId,
                        PositionX = x,
                        PositionY = y,
                        Status = ControlPointStatus.Inactive
                    };
                    _context.ControlPoints.Add(newCp);
                }
            }
            else
            {

                if (cp != null)
                {
                    _context.ControlPoints.Remove(cp);
                }
            }
            await _context.SaveChangesAsync(default);
        }
    }
}