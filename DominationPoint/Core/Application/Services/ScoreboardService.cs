using DominationPoint.Core.Domain;
using DominationPoint.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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

            // 1. Beräkna bonuspoäng för varje övertagande (Capture)
            var captureEvents = events.Where(e => e.Type == EventType.Capture && e.ActingUserId != null);
            foreach (var cev in captureEvents)
            {
                if (teamScores.ContainsKey(cev.ActingUserId!))
                {
                    teamScores[cev.ActingUserId!].CaptureBonusScore += 100;
                }
            }

            // 2. Beräkna poäng för att hålla CPs (sekund-för-sekund)
            var cpEvents = events.Where(e => e.Type == EventType.Capture || e.Type == EventType.GameEnd);
            foreach (var group in cpEvents.GroupBy(e => e.ControlPointId))
            {
                GameEvent? lastEvent = null;
                foreach (var currentEvent in group)
                {
                    // ====================================================================
                    // ==                 HÄR VAR BUGGEN - NU KORRIGERAD                 ==
                    // ====================================================================
                    // Vi ska ge poäng till den som agerade i den FÖREGÅENDE händelsen,
                    // eftersom de ägde punkten under intervallet.
                    if (lastEvent != null && lastEvent.ActingUserId != null)
                    {
                        if (teamScores.ContainsKey(lastEvent.ActingUserId))
                        {
                            var secondsHeld = (int)(currentEvent.Timestamp - lastEvent.Timestamp).TotalSeconds;
                            teamScores[lastEvent.ActingUserId].HoldingScore += secondsHeld;
                        }
                    }
                    // ====================================================================
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
                // We only have the total score now, so we assign it here.
                // If you wanted to preserve the breakdown, you'd need to add more columns to GameScore.
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