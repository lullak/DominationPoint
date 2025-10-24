using DominationPoint.Core.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DominationPoint.Core.Application.Services
{
    public class GameManagementService : IGameManagementService
    {
        private readonly IApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<GameManagementService> _logger;
        private readonly IScoreboardService _scoreboardService;

        public GameManagementService(IApplicationDbContext context, ILogger<GameManagementService> logger, UserManager<ApplicationUser> userManager, IScoreboardService scoreboardService) 
        {
            _context = context;
            _logger = logger;
            _userManager = userManager;
            _scoreboardService = scoreboardService; 
        }

        public async Task<List<Game>> GetAllGamesAsync() => await _context.Games.OrderByDescending(g => g.StartTime).ToListAsync();

        public async Task<Game?> GetGameByIdAsync(int id) => await _context.Games.FindAsync(id);

        public async Task CreateGameAsync(string name, DateTime startTime, DateTime endTime)
        {
            if (string.IsNullOrWhiteSpace(name) || endTime <= startTime)
                return;
            var newGame = new Game { Name = name, StartTime = startTime, EndTime = endTime, Status = GameStatus.Scheduled };
            _context.Games.Add(newGame);
            await _context.SaveChangesAsync(default);
        }

        public async Task StartGameAsync(int id)
        {
            if (id <=0) return;
            var game = await _context.Games.FindAsync(id);
            if (game != null && game.Status == GameStatus.Scheduled)
            {
                var anyActiveGames = await _context.Games.AnyAsync(g => g.Status == GameStatus.Active);
                if (anyActiveGames)
                {
                    _logger.LogWarning("Attempted to start game {GameId} while another game is already active.", id);
                    return;
                }

                game.Status = GameStatus.Active;
                await _context.SaveChangesAsync(default);
                _logger.LogInformation("Game {GameId} has been manually started.", id);
            }
        }

        public async Task EndGameAsync(int id)
        {
            if (id <= 0) return;
            var game = await _context.Games.FindAsync(id);
            if (game != null && game.Status == GameStatus.Active)
            {
                var ownedCps = await _context.ControlPoints
                    .Where(cp => cp.GameId == id && cp.ApplicationUserId != null)
                    .ToListAsync();

                var gameEndTime = DateTime.UtcNow;

                foreach (var cp in ownedCps)
                {
                    _context.GameEvents.Add(new GameEvent
                    {
                        GameId = id,
                        ControlPointId = cp.Id,
                        Type = EventType.GameEnd,
                        Timestamp = gameEndTime,
                        PreviousOwnerUserId = cp.ApplicationUserId
                    });
                }

                game.Status = GameStatus.Finished;
                await _context.SaveChangesAsync(default);

                _logger.LogInformation("Calculating final scores for game {GameId}.", id);
                var finalScoreboard = await _scoreboardService.CalculateScoreboardAsync(id);

                var oldScores = await _context.GameScores.Where(gs => gs.GameId == id).ToListAsync();
                if (oldScores.Any())
                {
                    _context.GameScores.RemoveRange(oldScores);
                }

                // Save the final scores
                foreach (var teamScore in finalScoreboard.TeamScores)
                {
                    var participant = await _context.GameParticipants
                        .Include(p => p.ApplicationUser)
                        .FirstOrDefaultAsync(p => p.GameId == id && p.ApplicationUser.UserName == teamScore.TeamName);

                    if (participant != null)
                    {
                        var finalScore = new GameScore
                        {
                            GameId = id,
                            ApplicationUserId = participant.ApplicationUserId,
                            Points = teamScore.TotalScore
                        };
                        _context.GameScores.Add(finalScore);
                    }
                }

                var cpsQuery = _context.ControlPoints.Where(cp => cp.GameId == id);
                if (cpsQuery is IAsyncEnumerable<ControlPoint> && cpsQuery.Provider.GetType().Name.Contains("EntityQueryProvider"))
                {
                    await cpsQuery.ExecuteUpdateAsync(s => s.SetProperty(cp => cp.Status, cp => ControlPointStatus.Inactive)
                                               .SetProperty(cp => cp.ApplicationUserId, cp => null));
                }
                else
                {
                    foreach (var cp in cpsQuery.ToList())
                    {
                        cp.Status = ControlPointStatus.Inactive;
                        cp.ApplicationUserId = null;
                    }
                }

                await _context.SaveChangesAsync(default); 
                _logger.LogInformation("Game {GameId} has been ended and final scores saved.", id);
            }
        }

        public async Task ResetGameAsync(int id)
        {
            await EndGameAsync(id);
        }

        public async Task AddParticipantAsync(int gameId, string userId)
        {
            if (string.IsNullOrWhiteSpace(userId) || gameId <=0) return;
            var query = _context.GameParticipants.Where(gp => gp.GameId == gameId && gp.ApplicationUserId == userId);
            bool alreadyExists;
            if (query is IAsyncEnumerable<GameParticipant>)
            {
                alreadyExists = await query.AnyAsync();
            }
            else
            {
                alreadyExists = query.Any();
            }
            if (!alreadyExists)
            {
                _context.GameParticipants.Add(new GameParticipant { GameId = gameId, ApplicationUserId = userId });
                await _context.SaveChangesAsync(default);
            }
        }

        public async Task RemoveParticipantAsync(int gameId, string userId)
        {
            if (string.IsNullOrWhiteSpace(userId) || gameId <=0) return;
            var participant = await _context.GameParticipants.FindAsync(gameId, userId);
            if (participant != null)
            {
                _context.GameParticipants.Remove(participant);
                await _context.SaveChangesAsync(default);
            }
        }

        public async Task<List<ApplicationUser>> GetParticipantsAsync(int gameId)
        {
            return await _context.GameParticipants
                .Where(gp => gp.GameId == gameId)
                .Select(gp => gp.ApplicationUser)
                .ToListAsync();
        }

        public async Task<List<GameParticipant>> GetParticipantsWithIncludesAsync(int gameId)
        {
            return await _context.GameParticipants
                .Include(gp => gp.ApplicationUser)
                .Where(gp => gp.GameId == gameId)
                .ToListAsync();
        }

        public async Task<List<ApplicationUser>> GetNonParticipantsAsync(int gameId)
        {
            var participantIds = await _context.GameParticipants
                .Where(gp => gp.GameId == gameId)
                .Select(gp => gp.ApplicationUserId)
                .ToListAsync();

            var usersQuery = _userManager.Users.Where(u => !participantIds.Contains(u.Id));
            if (usersQuery is IAsyncEnumerable<ApplicationUser>)
            {
                return await usersQuery.ToListAsync();
            }
            else
            {
                return usersQuery.ToList();
            }
        }


    }
}
