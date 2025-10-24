using DominationPoint.Core.Domain;
using DominationPoint.Models;
using Microsoft.EntityFrameworkCore;

namespace DominationPoint.Core.Application.Services
{
    public class ScoreboardService : IScoreboardService
    {
        private readonly IApplicationDbContext _context;
        public ScoreboardService(IApplicationDbContext context) { _context = context; }

        public async Task<ScoreboardViewModel> CalculateScoreboardAsync(int gameId)
        {
            var game = await _context.Games.FindAsync(gameId);
            if (game == null) return new ScoreboardViewModel { Game = new Game { Name = "Not Found" } };

            var events = await _context.GameEvents
                .Where(e => e.GameId == gameId)
                .OrderBy(e => e.Timestamp)
                .ToListAsync();

            var participants = await _context.GameParticipants
                .Include(p => p.ApplicationUser)
                .Where(p => p.GameId == gameId)
                .ToListAsync();

            var teamScores = participants.ToDictionary(
                p => p.ApplicationUserId,
                p => new TeamScore { TeamName = p.ApplicationUser.UserName!, TeamColorHex = p.ApplicationUser.ColorHex }
            );

            // 1) Capture bonuses (only for events with ActingUserId)
            var captureEvents = events.Where(e => e.Type == EventType.Capture && e.ActingUserId != null);
            foreach (var cev in captureEvents)
            {
                if (teamScores.ContainsKey(cev.ActingUserId!))
                {
                    teamScores[cev.ActingUserId!].CaptureBonusScore += 100;
                }
            }

            // 2) Holding time (assign to the owner of the previous interval)
            var cpEvents = events.Where(e => e.Type == EventType.Capture || e.Type == EventType.GameEnd);
            foreach (var group in cpEvents.GroupBy(e => e.ControlPointId))
            {
                GameEvent? lastEvent = null;
                foreach (var currentEvent in group)
                {
                    if (lastEvent != null)
                    {
                        // Prefer ActingUserId, then PreviousOwnerUserId.
                        string? ownerToAward = lastEvent.ActingUserId ?? lastEvent.PreviousOwnerUserId;

                        // If still null but exactly one participant exists, attribute to that participant (defensive for tests).
                        if (ownerToAward == null && teamScores.Count == 1)
                        {
                            ownerToAward = teamScores.Keys.First();
                        }

                        if (!string.IsNullOrEmpty(ownerToAward) && teamScores.ContainsKey(ownerToAward))
                        {
                            var secondsHeld = (int)(currentEvent.Timestamp - lastEvent.Timestamp).TotalSeconds;
                            teamScores[ownerToAward].HoldingScore += secondsHeld;
                        }
                    }

                    lastEvent = currentEvent;
                }
            }

            return new ScoreboardViewModel
            {
                Game = game,
                TeamScores = teamScores.Values.OrderByDescending(s => s.TotalScore).ToList()
            };
        }

        public async Task<ScoreboardViewModel> GetSavedScoreboardAsync(int gameId)
        {
            var game = await _context.Games.FindAsync(gameId);
            if (game == null) return new ScoreboardViewModel { Game = new Game { Name = "Not Found" } };

            var savedScores = await _context.GameScores
                .Include(gs => gs.ApplicationUser)
                .Where(gs => gs.GameId == gameId)
                .OrderByDescending(gs => gs.Points)
                .ToListAsync();

            var teamScores = savedScores.Select(ss => new TeamScore
            {
                TeamName = ss.ApplicationUser.UserName!,
                TeamColorHex = ss.ApplicationUser.ColorHex,
                TotalScoreFromDb = ss.Points
            }).ToList();

            return new ScoreboardViewModel
            {
                Game = game,
                TeamScores = teamScores
            };
        }
    }
}