using DominationPoint.Core.Application.HostedServices;
using DominationPoint.Core.Application;
using DominationPoint.Core.Domain;
using DominationPoint.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System;

namespace DominationPointTests.UnitTests.Services.Hosted
{
 public class LiveScoreUpdateServiceTests
 {
 // Helper to create a mock service provider with all dependencies
 private IServiceProvider GetServiceProvider(
 Mock<IApplicationDbContext> mockContext,
 Mock<IScoreboardService> mockScoreboard,
 Mock<ILogger<LiveScoreUpdateService>> mockLogger)
 {
 var scopeMock = new Mock<IServiceScope>();
 var providerMock = new Mock<IServiceProvider>();
 // Use GetService (non-extension) so Moq can setup it. The extension GetRequiredService cannot be setup directly.
 providerMock.Setup(p => p.GetService(typeof(IApplicationDbContext))).Returns(mockContext.Object);
 providerMock.Setup(p => p.GetService(typeof(IScoreboardService))).Returns(mockScoreboard.Object);
 scopeMock.Setup(s => s.ServiceProvider).Returns(providerMock.Object);
 var scopeFactoryMock = new Mock<IServiceScopeFactory>();
 scopeFactoryMock.Setup(f => f.CreateScope()).Returns(scopeMock.Object);
 var rootProviderMock = new Mock<IServiceProvider>();
 rootProviderMock.Setup(p => p.GetService(typeof(IServiceScopeFactory))).Returns(scopeFactoryMock.Object);
 // No direct setup for CreateScope on IServiceProvider - the extension method will resolve the scope factory via GetService
 return rootProviderMock.Object;
 }

 // POSITIVE TESTS
 [Fact]
 public async Task StartAsync_InitializesTimerAndLogs()
 {
 var logger = new Mock<ILogger<LiveScoreUpdateService>>();
 var provider = new Mock<IServiceProvider>();
 var service = new LiveScoreUpdateService(logger.Object, provider.Object);
 await service.StartAsync(CancellationToken.None);
 Assert.Contains(logger.Invocations, inv => inv.Method.Name == "Log" && inv.Arguments.Count >2 && inv.Arguments[2]?.ToString()?.Contains("Live Score Update Service is starting.") == true);
 }

 [Fact]
 public async Task StopAsync_StopsTimerAndLogs()
 {
 var logger = new Mock<ILogger<LiveScoreUpdateService>>();
 var provider = new Mock<IServiceProvider>();
 var service = new LiveScoreUpdateService(logger.Object, provider.Object);
 await service.StartAsync(CancellationToken.None);
 await service.StopAsync(CancellationToken.None);
 Assert.Contains(logger.Invocations, inv => inv.Method.Name == "Log" && inv.Arguments.Count >2 && inv.Arguments[2]?.ToString()?.Contains("Live Score Update Service is stopping.") == true);
 }

 [Fact]
 public void Dispose_DisposesTimer()
 {
 var logger = new Mock<ILogger<LiveScoreUpdateService>>();
 var provider = new Mock<IServiceProvider>();
 var service = new LiveScoreUpdateService(logger.Object, provider.Object);
 service.StartAsync(CancellationToken.None).Wait();
 service.Dispose();
 // No exception means success
 }

 [Fact]
 public void DoWork_ProcessesSingleActiveGameWithNoScoresOrParticipants()
 {
 var logger = new Mock<ILogger<LiveScoreUpdateService>>();
 var mockContext = new Mock<IApplicationDbContext>();
 var mockScoreboard = new Mock<IScoreboardService>();
 var games = new List<Game> { new Game { Id =1, Name = "Game1", Status = GameStatus.Active } };
 var mockGamesSet = MockDbSetHelper.GetMockDbSet(games.AsQueryable());
 mockContext.Setup(c => c.Games).Returns(mockGamesSet.Object);
 mockScoreboard.Setup(s => s.CalculateScoreboardAsync(1)).ReturnsAsync(new ScoreboardViewModel { Game = games[0], TeamScores = new List<TeamScore>() });
 var mockScoresSet = MockDbSetHelper.GetMockDbSet(new List<GameScore>().AsQueryable());
 mockContext.Setup(c => c.GameScores).Returns(mockScoresSet.Object);
 var mockParticipantsSet = MockDbSetHelper.GetMockDbSet(new List<GameParticipant>().AsQueryable());
 mockContext.Setup(c => c.GameParticipants).Returns(mockParticipantsSet.Object);
 var provider = GetServiceProvider(mockContext, mockScoreboard, logger);
 var service = new LiveScoreUpdateService(logger.Object, provider);
 service.GetType().GetMethod("DoWork", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(service, new object[] { null });
 Assert.Contains(logger.Invocations, inv => inv.Method.Name == "Log" && inv.Arguments.Count >2 && inv.Arguments[2]?.ToString()?.Contains("Calculating live scores for game: Game1") == true);
 Assert.Contains(logger.Invocations, inv => inv.Method.Name == "Log" && inv.Arguments.Count >2 && inv.Arguments[2]?.ToString()?.Contains("Successfully updated live scores for game: Game1") == true);
 }

        [Fact]
        public void DoWork_ProcessesMultipleActiveGames()
        {
            var logger = new Mock<ILogger<LiveScoreUpdateService>>();
            var mockContext = new Mock<IApplicationDbContext>();
            var mockScoreboard = new Mock<IScoreboardService>();
            var games = new List<Game>
 {
 new Game { Id = 1, Name = "Game1", Status = GameStatus.Active },
 new Game { Id = 2, Name = "Game2", Status = GameStatus.Active }
 };
            var mockGamesSet = MockDbSetHelper.GetMockDbSet(games.AsQueryable());
            mockContext.Setup(c => c.Games).Returns(mockGamesSet.Object);
            mockScoreboard.Setup(s => s.CalculateScoreboardAsync(1)).ReturnsAsync(new ScoreboardViewModel { Game = games[0], TeamScores = new List<TeamScore>() });
            mockScoreboard.Setup(s => s.CalculateScoreboardAsync(2)).ReturnsAsync(new ScoreboardViewModel { Game = games[1], TeamScores = new List<TeamScore>() });
            var mockScoresSet = MockDbSetHelper.GetMockDbSet(new List<GameScore>().AsQueryable());
            mockContext.Setup(c => c.GameScores).Returns(mockScoresSet.Object);
            var mockParticipantsSet = MockDbSetHelper.GetMockDbSet(new List<GameParticipant>().AsQueryable());
            mockContext.Setup(c => c.GameParticipants).Returns(mockParticipantsSet.Object);
            var provider = GetServiceProvider(mockContext, mockScoreboard, logger);
            var service = new LiveScoreUpdateService(logger.Object, provider);
            service.GetType().GetMethod("DoWork", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(service, new object[] { null });
            Assert.Contains(logger.Invocations, inv => inv.Method.Name == "Log" && inv.Arguments.Count > 2 && inv.Arguments[2]?.ToString()?.Contains("Calculating live scores for game: Game1") == true);
            Assert.Contains(logger.Invocations, inv => inv.Method.Name == "Log" && inv.Arguments.Count > 2 && inv.Arguments[2]?.ToString()?.Contains("Successfully updated live scores for game: Game2") == true);
 }

 [Fact]
 public void DoWork_SkipsInactiveGames()
 {
 var logger = new Mock<ILogger<LiveScoreUpdateService>>();
 var mockContext = new Mock<IApplicationDbContext>();
 var mockScoreboard = new Mock<IScoreboardService>();
 var games = new List<Game>
 {
 new Game { Id =1, Name = "Game1", Status = GameStatus.Scheduled }, // Only inactive
 };
 var mockGamesSet = MockDbSetHelper.GetMockDbSet(games.AsQueryable());
 mockContext.Setup(c => c.Games).Returns(mockGamesSet.Object);
 var mockScoresSet = MockDbSetHelper.GetMockDbSet(new List<GameScore>().AsQueryable());
 mockContext.Setup(c => c.GameScores).Returns(mockScoresSet.Object);
 var mockParticipantsSet = MockDbSetHelper.GetMockDbSet(new List<GameParticipant>().AsQueryable());
 mockContext.Setup(c => c.GameParticipants).Returns(mockParticipantsSet.Object);
 var provider = GetServiceProvider(mockContext, mockScoreboard, logger);
 var service = new LiveScoreUpdateService(logger.Object, provider);
 service.GetType().GetMethod("DoWork", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(service, new object[] { null });
 Assert.Contains(logger.Invocations, inv => inv.Method.Name == "Log" && inv.Arguments.Count >2 && inv.Arguments[2]?.ToString()?.Contains("Live Score Update Service is running.") == true);
 Assert.Contains(logger.Invocations, inv => inv.Method.Name == "Log" && inv.Arguments.Count >2 && inv.Arguments[2]?.ToString()?.Contains("No active games to score.") == true);
 }

 [Fact]
 public void DoWork_HandlesGameWithNewParticipants()
 {
 var logger = new Mock<ILogger<LiveScoreUpdateService>>();
 var mockContext = new Mock<IApplicationDbContext>();
 var mockScoreboard = new Mock<IScoreboardService>();
 var games = new List<Game> { new Game { Id =1, Name = "Game1", Status = GameStatus.Active } };
 var participants = new List<GameParticipant> { new GameParticipant { GameId =1, ApplicationUserId = "user1" } }; // Fixed: remove Id, TeamId
 var mockGamesSet = MockDbSetHelper.GetMockDbSet(games.AsQueryable());
 mockContext.Setup(c => c.Games).Returns(mockGamesSet.Object);
 mockScoreboard.Setup(s => s.CalculateScoreboardAsync(1)).ReturnsAsync(new ScoreboardViewModel { Game = games[0], TeamScores = new List<TeamScore>() });
 var mockScoresSet = MockDbSetHelper.GetMockDbSet(new List<GameScore>().AsQueryable());
 mockContext.Setup(c => c.GameScores).Returns(mockScoresSet.Object);
 var mockParticipantsSet = MockDbSetHelper.GetMockDbSet(participants.AsQueryable());
 mockContext.Setup(c => c.GameParticipants).Returns(mockParticipantsSet.Object);
 var provider = GetServiceProvider(mockContext, mockScoreboard, logger);
 var service = new LiveScoreUpdateService(logger.Object, provider);
 service.GetType().GetMethod("DoWork", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(service, new object[] { null });
 Assert.Contains(logger.Invocations, inv => inv.Method.Name == "Log" && inv.Arguments.Count >2 && inv.Arguments[2]?.ToString()?.Contains("Calculating live scores for game: Game1") == true);
 Assert.Contains(logger.Invocations, inv => inv.Method.Name == "Log" && inv.Arguments.Count >2 && inv.Arguments[2]?.ToString()?.Contains("Successfully updated live scores for game: Game1") == true);
 }

 [Fact]
 public void DoWork_HandlesGameWithUpdatedParticipants()
 {
 var logger = new Mock<ILogger<LiveScoreUpdateService>>();
 var mockContext = new Mock<IApplicationDbContext>();
 var mockScoreboard = new Mock<IScoreboardService>();
 var games = new List<Game> { new Game { Id =1, Name = "Game1", Status = GameStatus.Active } };
 var participants = new List<GameParticipant>
 {
 new GameParticipant { GameId =1, ApplicationUserId = "user1" }, // Fixed: remove Id, TeamId
 new GameParticipant { GameId =1, ApplicationUserId = "user2" }
 };
 var mockGamesSet = MockDbSetHelper.GetMockDbSet(games.AsQueryable());
 mockContext.Setup(c => c.Games).Returns(mockGamesSet.Object);
 mockScoreboard.Setup(s => s.CalculateScoreboardAsync(1)).ReturnsAsync(new ScoreboardViewModel { Game = games[0], TeamScores = new List<TeamScore>() });
 var mockScoresSet = MockDbSetHelper.GetMockDbSet(new List<GameScore>().AsQueryable());
 mockContext.Setup(c => c.GameScores).Returns(mockScoresSet.Object);
 var mockParticipantsSet = MockDbSetHelper.GetMockDbSet(participants.AsQueryable());
 mockContext.Setup(c => c.GameParticipants).Returns(mockParticipantsSet.Object);
 var provider = GetServiceProvider(mockContext, mockScoreboard, logger);
 var service = new LiveScoreUpdateService(logger.Object, provider);
 service.GetType().GetMethod("DoWork", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(service, new object[] { null });
 Assert.Contains(logger.Invocations, inv => inv.Method.Name == "Log" && inv.Arguments.Count >2 && inv.Arguments[2]?.ToString()?.Contains("Calculating live scores for game: Game1") == true);
 Assert.Contains(logger.Invocations, inv => inv.Method.Name == "Log" && inv.Arguments.Count >2 && inv.Arguments[2]?.ToString()?.Contains("Successfully updated live scores for game: Game1") == true);
 }

 [Fact]
 public void DoWork_AddThrows_LogsError()
 {
 var logger = new Mock<ILogger<LiveScoreUpdateService>>();
 var mockContext = new Mock<IApplicationDbContext>();
 var mockScoreboard = new Mock<IScoreboardService>();
 var games = new List<Game> { new Game { Id =1, Name = "Game1", Status = GameStatus.Active } };
 var mockGamesSet = MockDbSetHelper.GetMockDbSet(games.AsQueryable());
 mockContext.Setup(c => c.Games).Returns(mockGamesSet.Object);
 mockScoreboard.Setup(s => s.CalculateScoreboardAsync(1)).ReturnsAsync(new ScoreboardViewModel {
 Game = games[0],
 TeamScores = new List<TeamScore> { new TeamScore { TeamName = "Team1", HoldingScore =100 } }
 });
 var mockScoresSet = MockDbSetHelper.GetMockDbSet(new List<GameScore>().AsQueryable());
 mockContext.Setup(c => c.GameScores).Returns(mockScoresSet.Object);
 // Simulate Add throwing
 mockScoresSet.Setup(s => s.Add(It.IsAny<GameScore>())).Throws(new Exception("Add error"));
 var mockParticipantsSet = MockDbSetHelper.GetMockDbSet(new List<GameParticipant> {
 new GameParticipant { GameId =1, ApplicationUserId = "user1", ApplicationUser = new ApplicationUser { UserName = "Team1", ColorHex = "#000000" } }
 }.AsQueryable());
 mockContext.Setup(c => c.GameParticipants).Returns(mockParticipantsSet.Object);
 var provider = GetServiceProvider(mockContext, mockScoreboard, logger);
 var service = new LiveScoreUpdateService(logger.Object, provider);
 try {
 service.GetType().GetMethod("DoWork", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(service, new object[] { null });
 } catch {}
 Assert.Contains(logger.Invocations, inv => inv.Method.Name == "Log" && inv.Arguments.Count >2 && inv.Arguments[2]?.ToString()?.Contains("Error occurred while updating live scores.") == true);
 }

 // NEGATIVE TESTS
 [Fact]
 public void Constructor_NullLogger_Throws()
 {
 var provider = new Mock<IServiceProvider>();
 Assert.Throws<ArgumentNullException>(() => new LiveScoreUpdateService(null!, provider.Object));
 }

 [Fact]
 public void Constructor_NullProvider_Throws()
 {
 var logger = new Mock<ILogger<LiveScoreUpdateService>>();
 Assert.Throws<ArgumentNullException>(() => new LiveScoreUpdateService(logger.Object, null!));
 }

 [Fact]
 public void DoWork_NullScope_LogsError()
 {
 var logger = new Mock<ILogger<LiveScoreUpdateService>>();
 var scopeFactoryMock = new Mock<IServiceScopeFactory>();
 // Simulate CreateScope throwing
 scopeFactoryMock.Setup(f => f.CreateScope()).Throws(new NullReferenceException());
 var rootProviderMock = new Mock<IServiceProvider>();
 rootProviderMock.Setup(p => p.GetService(typeof(IServiceScopeFactory))).Returns(scopeFactoryMock.Object);
 var service = new LiveScoreUpdateService(logger.Object, rootProviderMock.Object);
 service.GetType().GetMethod("DoWork", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(service, new object[] { null });
 Assert.Contains(logger.Invocations, inv => inv.Method.Name == "Log" && inv.Arguments.Count >2 && inv.Arguments[2]?.ToString()?.Contains("Error occurred while updating live scores.") == true);
 }

 [Fact]
 public void DoWork_NullContext_LogsError()
 {
 var logger = new Mock<ILogger<LiveScoreUpdateService>>();
 var scopeMock = new Mock<IServiceScope>();
 var providerMock = new Mock<IServiceProvider>();
 // Return null for IApplicationDbContext
 providerMock.Setup(p => p.GetService(typeof(IApplicationDbContext))).Returns((IApplicationDbContext)null!);
 scopeMock.Setup(s => s.ServiceProvider).Returns(providerMock.Object);
 var scopeFactoryMock = new Mock<IServiceScopeFactory>();
 scopeFactoryMock.Setup(f => f.CreateScope()).Returns(scopeMock.Object);
 var rootProviderMock = new Mock<IServiceProvider>();
 // Only mock GetService for IServiceScopeFactory
 rootProviderMock.Setup(p => p.GetService(typeof(IServiceScopeFactory))).Returns(scopeFactoryMock.Object);
 var service = new LiveScoreUpdateService(logger.Object, rootProviderMock.Object);
 service.GetType().GetMethod("DoWork", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(service, new object[] { null });
 Assert.Contains(logger.Invocations, inv => inv.Method.Name == "Log" && inv.Arguments.Count >2 && inv.Arguments[2]?.ToString()?.Contains("Error occurred while updating live scores.") == true);
 }

 [Fact]
 public void DoWork_NullScoreboardService_LogsError()
 {
 var logger = new Mock<ILogger<LiveScoreUpdateService>>();
 var scopeMock = new Mock<IServiceScope>();
 var providerMock = new Mock<IServiceProvider>();
 providerMock.Setup(p => p.GetService(typeof(IApplicationDbContext))).Returns(new Mock<IApplicationDbContext>().Object);
 providerMock.Setup(p => p.GetService(typeof(IScoreboardService))).Returns((IScoreboardService)null!);
 scopeMock.Setup(s => s.ServiceProvider).Returns(providerMock.Object);
 var scopeFactoryMock = new Mock<IServiceScopeFactory>();
 scopeFactoryMock.Setup(f => f.CreateScope()).Returns(scopeMock.Object);
 var rootProviderMock = new Mock<IServiceProvider>();
 rootProviderMock.Setup(p => p.GetService(typeof(IServiceScopeFactory))).Returns(scopeFactoryMock.Object);
 var service = new LiveScoreUpdateService(logger.Object, rootProviderMock.Object);
 service.GetType().GetMethod("DoWork", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(service, new object[] { null });
 Assert.Contains(logger.Invocations, inv => inv.Method.Name == "Log" && inv.Arguments.Count >2 && inv.Arguments[2]?.ToString()?.Contains("Error occurred while updating live scores.") == true);
 }

 [Fact]
 public void DoWork_ThrowsException_LogsError()
 {
 var logger = new Mock<ILogger<LiveScoreUpdateService>>();
 var mockContext = new Mock<IApplicationDbContext>();
 var mockScoreboard = new Mock<IScoreboardService>();
 var games = new List<Game> { new Game { Id =1, Name = "Game1", Status = GameStatus.Active } };
 var mockGamesSet = MockDbSetHelper.GetMockDbSet(games.AsQueryable());
 // Simulate exception when accessing Games
 mockContext.Setup(c => c.Games).Returns(mockGamesSet.Object);
 mockContext.SetupGet(c => c.Games).Throws(new Exception("Test exception"));
 var provider = GetServiceProvider(mockContext, mockScoreboard, logger);
 var service = new LiveScoreUpdateService(logger.Object, provider);
 service.GetType().GetMethod("DoWork", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(service, new object[] { null });
 Assert.Contains(logger.Invocations, inv => inv.Method.Name == "Log" && inv.Arguments.Count >2 && inv.Arguments[2]?.ToString()?.Contains("Error occurred while updating live scores.") == true);
 }

 [Fact]
 public void DoWork_SaveChangesAsyncThrows_LogsError()
 {
 var logger = new Mock<ILogger<LiveScoreUpdateService>>();
 var mockContext = new Mock<IApplicationDbContext>();
 var mockScoreboard = new Mock<IScoreboardService>();
 var games = new List<Game> { new Game { Id =1, Name = "Game1", Status = GameStatus.Active } };
 var mockGamesSet = MockDbSetHelper.GetMockDbSet(games.AsQueryable());
 mockContext.Setup(c => c.Games).Returns(mockGamesSet.Object);
 mockScoreboard.Setup(s => s.CalculateScoreboardAsync(1)).ReturnsAsync(new ScoreboardViewModel { Game = games[0], TeamScores = new List<TeamScore>() });
 mockContext.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new Exception("Save error"));
 var mockScoresSet = MockDbSetHelper.GetMockDbSet(new List<GameScore>().AsQueryable());
 mockContext.Setup(c => c.GameScores).Returns(mockScoresSet.Object);
 var mockParticipantsSet = MockDbSetHelper.GetMockDbSet(new List<GameParticipant>().AsQueryable());
 mockContext.Setup(c => c.GameParticipants).Returns(mockParticipantsSet.Object);
 var provider = GetServiceProvider(mockContext, mockScoreboard, logger);
 var service = new LiveScoreUpdateService(logger.Object, provider);
 service.GetType().GetMethod("DoWork", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(service, new object[] { null });
 Assert.Contains(logger.Invocations, inv => inv.Method.Name == "Log" && inv.Arguments.Count >2 && inv.Arguments[2]?.ToString()?.Contains("Error occurred while updating live scores.") == true);
 }

 [Fact]
 public void DoWork_RemoveRangeThrows_LogsError()
 {
 var logger = new Mock<ILogger<LiveScoreUpdateService>>();
 var mockContext = new Mock<IApplicationDbContext>();
 var mockScoreboard = new Mock<IScoreboardService>();
 var games = new List<Game> { new Game { Id =1, Name = "Game1", Status = GameStatus.Active } };
 var mockGamesSet = MockDbSetHelper.GetMockDbSet(games.AsQueryable());
 mockContext.Setup(c => c.Games).Returns(mockGamesSet.Object);
 mockScoreboard.Setup(s => s.CalculateScoreboardAsync(1)).ReturnsAsync(new ScoreboardViewModel { Game = games[0], TeamScores = new List<TeamScore>() });
 var mockScoresSet = MockDbSetHelper.GetMockDbSet(new List<GameScore> { new GameScore { GameId =1, ApplicationUserId = "user1", Points =10 } }.AsQueryable());
 mockContext.Setup(c => c.GameScores).Returns(mockScoresSet.Object);
 // Simulate RemoveRange throwing
 mockScoresSet.Setup(s => s.RemoveRange(It.IsAny<IEnumerable<GameScore>>())).Throws(new Exception("RemoveRange error"));
 var mockParticipantsSet = MockDbSetHelper.GetMockDbSet(new List<GameParticipant>().AsQueryable());
 mockContext.Setup(c => c.GameParticipants).Returns(mockParticipantsSet.Object);
 var provider = GetServiceProvider(mockContext, mockScoreboard, logger);
 var service = new LiveScoreUpdateService(logger.Object, provider);
 try {
 service.GetType().GetMethod("DoWork", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(service, new object[] { null });
 } catch {}
 Assert.Contains(logger.Invocations, inv => inv.Method.Name == "Log" && inv.Arguments.Count >2 && inv.Arguments[2]?.ToString()?.Contains("Error occurred while updating live scores.") == true);
 }
 }
}
