using DominationPoint.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace DominationPoint.Core.Application.HostedServices
{
    public class LiveScoreUpdateService : IHostedService, IDisposable
    {
        private readonly ILogger<LiveScoreUpdateService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private Timer? _timer;

        public LiveScoreUpdateService(ILogger<LiveScoreUpdateService> logger, IServiceProvider serviceProvider)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Live Score Update Service is starting.");
            // Run the scoring logic every 10 seconds
            _timer = new Timer(DoWork, null, TimeSpan.Zero, TimeSpan.FromSeconds(10));
            return Task.CompletedTask;
        }

        private async void DoWork(object? state)
        {
            _logger.LogInformation("Live Score Update Service is running.");
            try
            {
                // Create a new scope to resolve scoped services like DbContext
                using (var scope = _serviceProvider.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
                    var scoreboardService = scope.ServiceProvider.GetRequiredService<IScoreboardService>();

                    // Find all currently active games
                    var activeGames = await context.Games
                        .Where(g => g.Status == Domain.GameStatus.Active)
                        .ToListAsync();

                    if (!activeGames.Any())
                    {
                        _logger.LogInformation("No active games to score.");
                        return;
                    }

                    foreach (var game in activeGames)
                    {
                        _logger.LogInformation("Calculating live scores for game: {GameName}", game.Name);
                        var scoreboard = await scoreboardService.CalculateScoreboardAsync(game.Id);

                        // Clear previous "live" scores for this game
                        var oldScores = await context.GameScores.Where(gs => gs.GameId == game.Id).ToListAsync();
                        if (oldScores.Any())
                        {
                            context.GameScores.RemoveRange(oldScores);
                        }

                        // Save the new scores
                        foreach (var teamScore in scoreboard.TeamScores)
                        {
                            // Find the user ID for the team name
                            var participant = await context.GameParticipants
                                .Include(p => p.ApplicationUser)
                                .FirstOrDefaultAsync(p => p.GameId == game.Id && p.ApplicationUser.UserName == teamScore.TeamName);

                            if (participant != null)
                            {
                                var newScore = new GameScore
                                {
                                    GameId = game.Id,
                                    ApplicationUserId = participant.ApplicationUserId,
                                    Points = teamScore.TotalScore
                                };
                                context.GameScores.Add(newScore);
                            }
                        }
                        await context.SaveChangesAsync(default);
                        _logger.LogInformation("Successfully updated live scores for game: {GameName}", game.Name);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while updating live scores.");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Live Score Update Service is stopping.");
            _timer?.Change(Timeout.Infinite, 0);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}