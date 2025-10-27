using DominationPoint.Core.Application;
using DominationPoint.Core.Application.Services;
using DominationPoint.Core.Domain;
using DominationPoint.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;

namespace DominationPointTests.UnitTests.Services
{
    public class GameManagementServiceTests
    {
        private readonly Mock<IApplicationDbContext> _mockContext;
        private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
        private readonly Mock<IScoreboardService> _mockScoreboardService;
        private readonly Mock<ILogger<GameManagementService>> _mockLogger;
        private readonly GameManagementService _service;

        public GameManagementServiceTests()
        {
            _mockContext = new Mock<IApplicationDbContext>();
            _mockScoreboardService = new Mock<IScoreboardService>();
            _mockLogger = new Mock<ILogger<GameManagementService>>();

            var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
            _mockUserManager = new Mock<UserManager<ApplicationUser>>(
                userStoreMock.Object, null, null, null, null, null, null, null, null);

            _service = new GameManagementService(
                _mockContext.Object,
                _mockLogger.Object,
                _mockUserManager.Object,
                _mockScoreboardService.Object
            );
        }

        #region CreateGameAsync Tests
        [Fact]
        public async Task CreateGameAsync_WithValidData_ShouldAddGameToContextAndSaveChanges()
        {
            // ARRANGE
            var gameName = "Operation Test";
            var startTime = DateTime.UtcNow.AddDays(1);
            var endTime = DateTime.UtcNow.AddDays(2);
            var mockGamesDbSet = new Mock<DbSet<Game>>();
            _mockContext.Setup(c => c.Games).Returns(mockGamesDbSet.Object);
            _mockContext.Setup(c => c.SaveChangesAsync(default)).ReturnsAsync(1);

            // ACT
            await _service.CreateGameAsync(gameName, startTime, endTime);

            // ASSERT
            _mockContext.Verify(c => c.Games.Add(It.Is<Game>(g =>
                g.Name == gameName &&
                g.StartTime == startTime &&
                g.Status == GameStatus.Scheduled
            )), Times.Once);
            _mockContext.Verify(c => c.SaveChangesAsync(default), Times.Once);
        }

        [Fact]
        public async Task CreateGameAsync_WithMinimalValidName_ShouldSucceed()
        {
            // ARRANGE
            var mockGamesDbSet = new Mock<DbSet<Game>>();
            _mockContext.Setup(c => c.Games).Returns(mockGamesDbSet.Object);

            // ACT
            await _service.CreateGameAsync("A", DateTime.UtcNow, DateTime.UtcNow.AddHours(1));

            // ASSERT
            _mockContext.Verify(c => c.Games.Add(It.IsAny<Game>()), Times.Once);
            _mockContext.Verify(c => c.SaveChangesAsync(default), Times.Once);
        }

        [Fact]
        public async Task CreateGameAsync_WithEndTimeBeforeStartTime_ShouldNotAddGame()
        {
            // ARRANGE
            var mockGamesDbSet = new Mock<DbSet<Game>>();
            _mockContext.Setup(c => c.Games).Returns(mockGamesDbSet.Object);

            // ACT
            await _service.CreateGameAsync("Test", DateTime.UtcNow, DateTime.UtcNow.AddHours(-1));

            // ASSERT
            _mockContext.Verify(c => c.Games.Add(It.IsAny<Game>()), Times.Never);
            _mockContext.Verify(c => c.SaveChangesAsync(default), Times.Never);
        }
        #endregion

        #region StartGameAsync Tests
        [Fact]
        public async Task StartGameAsync_ForScheduledGameWithNoOtherActiveGames_ShouldSetStatusToActive()
        {
            // ARRANGE
            var gameId = 5;
            var scheduledGame = new Game { Id = gameId, Name = "Scheduled Game 5", Status = GameStatus.Scheduled, StartTime = DateTime.UtcNow, EndTime = DateTime.UtcNow.AddHours(1) };
            var games = new List<Game> { scheduledGame, new Game { Id = 6, Name = "Finished Game6", Status = GameStatus.Finished, StartTime = DateTime.UtcNow, EndTime = DateTime.UtcNow.AddHours(1) } }.AsQueryable();
            var mockGamesDbSet = MockDbSetHelper.GetMockDbSet(games);
            _mockContext.Setup(c => c.Games).Returns(mockGamesDbSet.Object);
            _mockContext.Setup(c => c.Games.FindAsync(gameId)).ReturnsAsync(scheduledGame);

            // ACT
            await _service.StartGameAsync(gameId);

            // ASSERT
            scheduledGame.Status.ShouldBe(GameStatus.Active);
            _mockContext.Verify(c => c.SaveChangesAsync(default), Times.Once);
        }

        [Fact]
        public async Task StartGameAsync_WhenAnotherGameIsActive_ShouldDoNothingAndLogWarning()
        {
            // ARRANGE
            var gameId = 5;
            var scheduledGame = new Game { Id = gameId, Name = "Scheduled Game 5", Status = GameStatus.Scheduled, StartTime = DateTime.UtcNow, EndTime = DateTime.UtcNow.AddHours(1) };
            var games = new List<Game> { scheduledGame, new Game { Id = 2, Name = "Active Game2", Status = GameStatus.Active, StartTime = DateTime.UtcNow, EndTime = DateTime.UtcNow.AddHours(1) } }.AsQueryable();
            var mockGamesDbSet = MockDbSetHelper.GetMockDbSet(games);
            _mockContext.Setup(c => c.Games).Returns(mockGamesDbSet.Object);
            _mockContext.Setup(c => c.Games.FindAsync(gameId)).ReturnsAsync(scheduledGame);

            // ACT
            await _service.StartGameAsync(gameId);

            // ASSERT
            scheduledGame.Status.ShouldBe(GameStatus.Scheduled);
            _mockContext.Verify(c => c.SaveChangesAsync(default), Times.Never);
        }

        [Fact]
        public async Task StartGameAsync_ForAlreadyActiveGame_ShouldDoNothing()
        {
            // ARRANGE
            var gameId = 1;
            var activeGame = new Game { Id = gameId, Name = "Active Game10", Status = GameStatus.Active, StartTime = DateTime.UtcNow, EndTime = DateTime.UtcNow.AddHours(1) };
            _mockContext.Setup(c => c.Games.FindAsync(gameId)).ReturnsAsync(activeGame);

            // ACT
            await _service.StartGameAsync(gameId);

            // ASSERT
            _mockContext.Verify(c => c.SaveChangesAsync(default), Times.Never);
        }

        [Fact]
        public async Task StartGameAsync_ForFinishedGame_ShouldDoNothing()
        {
            // ARRANGE
            var gameId = 1;
            var finishedGame = new Game { Id = gameId, Name = "Finished Game", Status = GameStatus.Finished, StartTime = DateTime.UtcNow, EndTime = DateTime.UtcNow.AddHours(1) };
            _mockContext.Setup(c => c.Games.FindAsync(gameId)).ReturnsAsync(finishedGame);

            // ACT
            await _service.StartGameAsync(gameId);

            // ASSERT
            _mockContext.Verify(c => c.SaveChangesAsync(default), Times.Never);
        }

        [Fact]
        public async Task StartGameAsync_ForNonExistentGame_ShouldDoNothing()
        {
            // ARRANGE
            var gameId = 999;
            _mockContext.Setup(c => c.Games.FindAsync(gameId)).ReturnsAsync((Game)null);

            // ACT
            await _service.StartGameAsync(gameId);

            // ASSERT
            _mockContext.Verify(c => c.SaveChangesAsync(default), Times.Never);
        }

        [Fact]
        public async Task StartGameAsync_WithNullId_ShouldDoNothing()
        {
            // ACT
            await _service.StartGameAsync(0);

            // ASSERT
            _mockContext.Verify(c => c.SaveChangesAsync(default), Times.Never);
        }
        #endregion

        #region EndGameAsync Tests
        [Fact]
        public async Task EndGameAsync_ForActiveGame_ShouldUpdateStatusAndSaveFinalScores()
        {
            // ARRANGE
            var gameId = 10;
            var activeGame = new Game { Id = gameId, Name = "Active Game10", Status = GameStatus.Active, StartTime = DateTime.UtcNow, EndTime = DateTime.UtcNow.AddHours(1) };
            var teamUser = new ApplicationUser { Id = "test-user-id", UserName = "Test Team", ColorHex = "#FFFFFF" };
            _mockContext.Setup(c => c.Games.FindAsync(gameId)).ReturnsAsync(activeGame);
            var ownedCps = new List<ControlPoint> { new ControlPoint { Id = 101, GameId = gameId, ApplicationUserId = teamUser.Id } }.AsQueryable();
            var mockCpDbSet = MockDbSetHelper.GetMockDbSet(ownedCps);
            _mockContext.Setup(c => c.ControlPoints).Returns(mockCpDbSet.Object);
            var mockGameEventsDbSet = MockDbSetHelper.GetMockDbSet(new List<GameEvent>().AsQueryable());
            _mockContext.Setup(c => c.GameEvents).Returns(mockGameEventsDbSet.Object);
            var finalScores = new ScoreboardViewModel { TeamScores = new List<TeamScore> { new TeamScore { TeamName = teamUser.UserName, TotalScoreFromDb = 500 } } };
            _mockScoreboardService.Setup(s => s.CalculateScoreboardAsync(gameId)).ReturnsAsync(finalScores);
            var participants = new List<GameParticipant> { new GameParticipant { GameId = gameId, ApplicationUserId = teamUser.Id, ApplicationUser = teamUser } }.AsQueryable();
            var mockParticipantsDbSet = MockDbSetHelper.GetMockDbSet(participants);
            _mockContext.Setup(c => c.GameParticipants).Returns(mockParticipantsDbSet.Object);
            var mockGameScoresDbSet = MockDbSetHelper.GetMockDbSet(new List<GameScore>().AsQueryable());
            _mockContext.Setup(c => c.GameScores).Returns(mockGameScoresDbSet.Object);

            // ACT
            await _service.EndGameAsync(gameId);

            // ASSERT
            activeGame.Status.ShouldBe(GameStatus.Finished);
            mockGameEventsDbSet.Verify(m => m.Add(It.Is<GameEvent>(e => e.Type == EventType.GameEnd)), Times.Once);
            _mockScoreboardService.Verify(s => s.CalculateScoreboardAsync(gameId), Times.Once);
            mockGameScoresDbSet.Verify(m => m.Add(It.Is<GameScore>(s => s.Points == 500 && s.ApplicationUserId == teamUser.Id)), Times.Once);
            _mockContext.Verify(c => c.SaveChangesAsync(default), Times.AtLeast(2));
        }

        [Fact]
        public async Task EndGameAsync_ForGameWithNoOwnedControlPoints_ShouldEndGameWithoutScoreEvents()
        {
            // ARRANGE
            var gameId = 11;
            var activeGame = new Game { Id = gameId, Name = "Active Game10", Status = GameStatus.Active, StartTime = DateTime.UtcNow, EndTime = DateTime.UtcNow.AddHours(1) };
            _mockContext.Setup(c => c.Games.FindAsync(gameId)).ReturnsAsync(activeGame);
            var ownedCps = new List<ControlPoint>().AsQueryable();
            var mockCpDbSet = MockDbSetHelper.GetMockDbSet(ownedCps);
            _mockContext.Setup(c => c.ControlPoints).Returns(mockCpDbSet.Object);
            var mockGameEventsDbSet = MockDbSetHelper.GetMockDbSet(new List<GameEvent>().AsQueryable());
            _mockContext.Setup(c => c.GameEvents).Returns(mockGameEventsDbSet.Object);
            var finalScores = new ScoreboardViewModel { TeamScores = new List<TeamScore>() };
            _mockScoreboardService.Setup(s => s.CalculateScoreboardAsync(gameId)).ReturnsAsync(finalScores);
            var mockGameScoresDbSet = MockDbSetHelper.GetMockDbSet(new List<GameScore>().AsQueryable());
            _mockContext.Setup(c => c.GameScores).Returns(mockGameScoresDbSet.Object);

            // ACT
            await _service.EndGameAsync(gameId);

            // ASSERT
            activeGame.Status.ShouldBe(GameStatus.Finished);
            mockGameEventsDbSet.Verify(m => m.Add(It.IsAny<GameEvent>()), Times.Never);
            mockGameScoresDbSet.Verify(m => m.Add(It.IsAny<GameScore>()), Times.Never);
        }

        [Fact]
        public async Task EndGameAsync_WithPreviousLiveScores_ShouldRemoveOldScoresBeforeAddingFinal()
        {
            // ARRANGE
            var gameId = 12;
            var activeGame = new Game { Id = gameId, Name = "Active Game10", Status = GameStatus.Active, StartTime = DateTime.UtcNow, EndTime = DateTime.UtcNow.AddHours(1) };
            var teamUser = new ApplicationUser { Id = "test-user-id", UserName = "Test Team", ColorHex = "#FFFFFF" };
            var existingLiveScores = new List<GameScore> { new GameScore { Id = 1, GameId = gameId, ApplicationUserId = teamUser.Id, Points = 100 } }.AsQueryable();
            var mockGameScoresDbSet = MockDbSetHelper.GetMockDbSet(existingLiveScores);
            _mockContext.Setup(c => c.Games.FindAsync(gameId)).ReturnsAsync(activeGame);
            _mockContext.Setup(c => c.ControlPoints).Returns(MockDbSetHelper.GetMockDbSet(new List<ControlPoint>().AsQueryable()).Object);
            _mockContext.Setup(c => c.GameEvents).Returns(MockDbSetHelper.GetMockDbSet(new List<GameEvent>().AsQueryable()).Object);
            var finalScores = new ScoreboardViewModel { TeamScores = new List<TeamScore> { new TeamScore { TeamName = teamUser.UserName, TotalScoreFromDb = 500 } } };
            _mockScoreboardService.Setup(s => s.CalculateScoreboardAsync(gameId)).ReturnsAsync(finalScores);
            var participants = new List<GameParticipant> { new GameParticipant { GameId = gameId, ApplicationUserId = teamUser.Id, ApplicationUser = teamUser } }.AsQueryable();
            _mockContext.Setup(c => c.GameParticipants).Returns(MockDbSetHelper.GetMockDbSet(participants).Object);
            _mockContext.Setup(c => c.GameScores).Returns(mockGameScoresDbSet.Object);
            mockGameScoresDbSet.Setup(m => m.RemoveRange(It.IsAny<IEnumerable<GameScore>>())).Verifiable();

            // ACT
            await _service.EndGameAsync(gameId);

            // ASSERT
            mockGameScoresDbSet.Verify(m => m.RemoveRange(It.IsAny<IEnumerable<GameScore>>()), Times.Once);
            mockGameScoresDbSet.Verify(m => m.Add(It.Is<GameScore>(s => s.Points == 500)), Times.Once);
        }

        [Fact]
        public async Task EndGameAsync_ForNonExistentGame_ShouldDoNothing()
        {
            // ARRANGE
            var gameId = 999;
            _mockContext.Setup(c => c.Games.FindAsync(gameId)).ReturnsAsync((Game)null);

            // ACT
            await _service.EndGameAsync(gameId);

            // ASSERT
            _mockContext.Verify(c => c.SaveChangesAsync(default), Times.Never);
            _mockScoreboardService.Verify(s => s.CalculateScoreboardAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task EndGameAsync_ForScheduledGame_ShouldDoNothing()
        {
            // ARRANGE
            var gameId = 1;
            var scheduledGame = new Game { Id = gameId, Name = "Scheduled Game5", Status = GameStatus.Scheduled, StartTime = DateTime.UtcNow, EndTime = DateTime.UtcNow.AddHours(1) };
            _mockContext.Setup(c => c.Games.FindAsync(gameId)).ReturnsAsync(scheduledGame);

            // ACT
            await _service.EndGameAsync(gameId);

            // ASSERT
            scheduledGame.Status.ShouldBe(GameStatus.Scheduled);
            _mockContext.Verify(c => c.SaveChangesAsync(default), Times.Never);
        }

        [Fact]
        public async Task EndGameAsync_ForFinishedGame_ShouldDoNothing()
        {
            // ARRANGE
            var gameId = 1;
            var finishedGame = new Game { Id = gameId, Name = "Finished Game", Status = GameStatus.Finished, StartTime = DateTime.UtcNow, EndTime = DateTime.UtcNow.AddHours(1) };
            _mockContext.Setup(c => c.Games.FindAsync(gameId)).ReturnsAsync(finishedGame);

            // ACT
            await _service.EndGameAsync(gameId);

            // ASSERT
            _mockContext.Verify(c => c.SaveChangesAsync(default), Times.Never);
        }

        [Fact]
        public async Task EndGameAsync_WhenScoreboardCalculationFails_ShouldStillEndGame()
        {
            // ARRANGE
            var gameId = 15;
            var activeGame = new Game { Id = gameId, Name = "Active Game10", Status = GameStatus.Active, StartTime = DateTime.UtcNow, EndTime = DateTime.UtcNow.AddHours(1) };
            _mockContext.Setup(c => c.Games.FindAsync(gameId)).ReturnsAsync(activeGame);
            _mockContext.Setup(c => c.ControlPoints).Returns(MockDbSetHelper.GetMockDbSet(new List<ControlPoint>().AsQueryable()).Object);
            _mockContext.Setup(c => c.GameEvents).Returns(MockDbSetHelper.GetMockDbSet(new List<GameEvent>().AsQueryable()).Object);
            _mockScoreboardService.Setup(s => s.CalculateScoreboardAsync(gameId)).ThrowsAsync(new InvalidOperationException("Calculation failed"));

            // ACT & ASSERT
            await Should.ThrowAsync<InvalidOperationException>(async () => await _service.EndGameAsync(gameId));
            activeGame.Status.ShouldBe(GameStatus.Finished); // Status should be updated before score calculation
            _mockContext.Verify(c => c.GameScores.Add(It.IsAny<GameScore>()), Times.Never); // No scores should be added
        }

        [Fact]
        public async Task EndGameAsync_WithMultipleOwnedCPs_ShouldCreateMultipleGameEndEvents()
        {
            // ARRANGE
            var gameId = 16;
            var activeGame = new Game { Id = gameId, Name = "Active Game10", Status = GameStatus.Active, StartTime = DateTime.UtcNow, EndTime = DateTime.UtcNow.AddHours(1) };
            var teamUser = new ApplicationUser { Id = "test-user-id", UserName = "Test Team", ColorHex = "#FFFFFF" };
            var ownedCps = new List<ControlPoint>
        {
            new ControlPoint { Id = 1, GameId = gameId, ApplicationUserId = teamUser.Id },
            new ControlPoint { Id = 2, GameId = gameId, ApplicationUserId = teamUser.Id }
        }.AsQueryable();
            var mockCpDbSet = MockDbSetHelper.GetMockDbSet(ownedCps);
            _mockContext.Setup(c => c.Games.FindAsync(gameId)).ReturnsAsync(activeGame);
            _mockContext.Setup(c => c.ControlPoints).Returns(mockCpDbSet.Object);
            var mockGameEventsDbSet = MockDbSetHelper.GetMockDbSet(new List<GameEvent>().AsQueryable());
            _mockContext.Setup(c => c.GameEvents).Returns(mockGameEventsDbSet.Object);
            _mockScoreboardService.Setup(s => s.CalculateScoreboardAsync(gameId)).ReturnsAsync(new ScoreboardViewModel());
            _mockContext.Setup(c => c.GameScores).Returns(MockDbSetHelper.GetMockDbSet(new List<GameScore>().AsQueryable()).Object);

            // ACT
            await _service.EndGameAsync(gameId);

            // ASSERT
            mockGameEventsDbSet.Verify(m => m.Add(It.IsAny<GameEvent>()), Times.Exactly(2));
        }
        #endregion

        #region Participant Management Tests

        [Fact]
        public async Task AddParticipantAsync_WithNewUser_ShouldAddParticipant()
        {
            // ARRANGE
            var gameId = 1;
            var userId = "new-user";
            var participants = new List<GameParticipant>().AsQueryable();
            var mockParticipantsDbSet = MockDbSetHelper.GetMockDbSet(participants);
            _mockContext.Setup(c => c.GameParticipants).Returns(mockParticipantsDbSet.Object);

            // ACT
            await _service.AddParticipantAsync(gameId, userId);

            // ASSERT
            _mockContext.Verify(c => c.GameParticipants.Add(It.Is<GameParticipant>(p => p.GameId == gameId && p.ApplicationUserId == userId)), Times.Once);
            _mockContext.Verify(c => c.SaveChangesAsync(default), Times.Once);
        }

        [Fact]
        public async Task AddParticipantAsync_ForExistingParticipant_ShouldDoNothing()
        {
            // ARRANGE
            var gameId = 1;
            var userId = "existing-user";
            var participants = new List<GameParticipant> { new GameParticipant { GameId = gameId, ApplicationUserId = userId } }.AsQueryable();
            var mockParticipantsDbSet = MockDbSetHelper.GetMockDbSet(participants);
            _mockContext.Setup(c => c.GameParticipants).Returns(mockParticipantsDbSet.Object);

            // ACT
            await _service.AddParticipantAsync(gameId, userId);

            // ASSERT
            _mockContext.Verify(c => c.GameParticipants.Add(It.IsAny<GameParticipant>()), Times.Never);
            _mockContext.Verify(c => c.SaveChangesAsync(default), Times.Never);
        }

        [Fact]
        public async Task RemoveParticipantAsync_WithExistingParticipant_ShouldRemoveParticipant()
        {
            // ARRANGE
            var gameId = 1;
            var userId = "user-to-remove";
            var participantToRemove = new GameParticipant { GameId = gameId, ApplicationUserId = userId };
            var mockParticipantsDbSet = new Mock<DbSet<GameParticipant>>();
            _mockContext.Setup(c => c.GameParticipants).Returns(mockParticipantsDbSet.Object);
            mockParticipantsDbSet.Setup(m => m.FindAsync(gameId, userId)).ReturnsAsync(participantToRemove);

            // ACT
            await _service.RemoveParticipantAsync(gameId, userId);

            // ASSERT
            _mockContext.Verify(c => c.GameParticipants.Remove(participantToRemove), Times.Once);
            _mockContext.Verify(c => c.SaveChangesAsync(default), Times.Once);
        }

        [Fact]
        public async Task RemoveParticipantAsync_WithNonExistentParticipant_ShouldDoNothing()
        {
            // ARRANGE
            var gameId = 1;
            var userId = "non-existent-user";
            var mockParticipantsDbSet = new Mock<DbSet<GameParticipant>>();
            _mockContext.Setup(c => c.GameParticipants).Returns(mockParticipantsDbSet.Object);
            mockParticipantsDbSet.Setup(m => m.FindAsync(gameId, userId)).ReturnsAsync((GameParticipant)null);

            // ACT
            await _service.RemoveParticipantAsync(gameId, userId);

            // ASSERT
            _mockContext.Verify(c => c.GameParticipants.Remove(It.IsAny<GameParticipant>()), Times.Never);
            _mockContext.Verify(c => c.SaveChangesAsync(default), Times.Never);
        }

        [Fact]
        public async Task AddParticipantAsync_WithNullUserId_ShouldNotThrowAndDoNothing()
        {
            // ARRANGE
            var gameId = 1;
            string userId = null;

            // ACT & ASSERT
            await _service.AddParticipantAsync(gameId, userId);
            _mockContext.Verify(c => c.GameParticipants.Add(It.IsAny<GameParticipant>()), Times.Never);
            _mockContext.Verify(c => c.SaveChangesAsync(default), Times.Never);
        }

        [Fact]
        public async Task RemoveParticipantAsync_WithNullUserId_ShouldNotThrowAndDoNothing()
        {
            // ARRANGE
            var gameId = 1;
            string userId = null;
            var mockParticipantsDbSet = new Mock<DbSet<GameParticipant>>();
            _mockContext.Setup(c => c.GameParticipants).Returns(mockParticipantsDbSet.Object);

            // ACT & ASSERT
            await _service.RemoveParticipantAsync(gameId, userId);
            _mockContext.Verify(c => c.GameParticipants.Remove(It.IsAny<GameParticipant>()), Times.Never);
            _mockContext.Verify(c => c.SaveChangesAsync(default), Times.Never);
        }

        [Fact]
        public async Task AddParticipantAsync_WithWhitespaceUserId_ShouldNotAdd()
        {
            // ARRANGE
            var gameId = 1;
            var userId = " ";

            // ACT
            await _service.AddParticipantAsync(gameId, userId);

            // ASSERT
            _mockContext.Verify(c => c.GameParticipants.Add(It.IsAny<GameParticipant>()), Times.Never);
        }

        [Fact]
        public async Task RemoveParticipantAsync_WithWhitespaceUserId_ShouldNotRemove()
        {
            // ARRANGE
            var gameId = 1;
            var userId = " ";
            var mockParticipantsDbSet = new Mock<DbSet<GameParticipant>>();
            _mockContext.Setup(c => c.GameParticipants).Returns(mockParticipantsDbSet.Object);

            // ACT
            await _service.RemoveParticipantAsync(gameId, userId);

            // ASSERT
            _mockContext.Verify(c => c.GameParticipants.Remove(It.IsAny<GameParticipant>()), Times.Never);
        }

        [Fact]
        public async Task AddParticipantAsync_WithInvalidGameId_ShouldNotAdd()
        {
            // ARRANGE
            var participants = new List<GameParticipant>().AsQueryable();
            var mockParticipantsDbSet = MockDbSetHelper.GetMockDbSet(participants);
            _mockContext.Setup(c => c.GameParticipants).Returns(mockParticipantsDbSet.Object);

            // ACT
            await _service.AddParticipantAsync(-1, "user");

            // ASSERT
            _mockContext.Verify(c => c.GameParticipants.Add(It.IsAny<GameParticipant>()), Times.Never);
        }

        [Fact]
        public async Task RemoveParticipantAsync_WithInvalidGameId_ShouldNotRemove()
        {
            // ARRANGE
            var mockParticipantsDbSet = new Mock<DbSet<GameParticipant>>();
            _mockContext.Setup(c => c.GameParticipants).Returns(mockParticipantsDbSet.Object);
            mockParticipantsDbSet.Setup(m => m.FindAsync(-1, "user")).ReturnsAsync((GameParticipant)null);

            // ACT
            await _service.RemoveParticipantAsync(-1, "user");

            // ASSERT
            _mockContext.Verify(c => c.GameParticipants.Remove(It.IsAny<GameParticipant>()), Times.Never);
        }
        #endregion

        #region Get Participants Tests

        [Fact]
        public async Task GetParticipantsAsync_WithMultipleParticipants_ShouldReturnCorrectUsers()
        {
            // ARRANGE
            var gameId = 1;
            var user1 = new ApplicationUser { Id = "user1", UserName = "User One", ColorHex = "#000000" };
            var user2 = new ApplicationUser { Id = "user2", UserName = "User Two", ColorHex = "#000000" };
            var participants = new List<GameParticipant>
        {
            new GameParticipant { GameId = gameId, ApplicationUserId = user1.Id, ApplicationUser = user1 },
            new GameParticipant { GameId = gameId, ApplicationUserId = user2.Id, ApplicationUser = user2 },
            new GameParticipant { GameId = 2, ApplicationUserId = "other", ApplicationUser = new ApplicationUser{Id="other", ColorHex="#000000"} }
        }.AsQueryable();
            var mockParticipantsDbSet = MockDbSetHelper.GetMockDbSet(participants);
            _mockContext.Setup(c => c.GameParticipants).Returns(mockParticipantsDbSet.Object);

            // ACT
            var result = await _service.GetParticipantsAsync(gameId);

            // ASSERT
            result.Count.ShouldBe(2);
            result.ShouldContain(u => u.Id == "user1");
            result.ShouldContain(u => u.Id == "user2");
        }

        [Fact]
        public async Task GetParticipantsAsync_WithNoParticipants_ShouldReturnEmptyList()
        {
            // ARRANGE
            var gameId = 1;
            var participants = new List<GameParticipant>().AsQueryable();
            var mockParticipantsDbSet = MockDbSetHelper.GetMockDbSet(participants);
            _mockContext.Setup(c => c.GameParticipants).Returns(mockParticipantsDbSet.Object);

            // ACT
            var result = await _service.GetParticipantsAsync(gameId);

            // ASSERT
            result.ShouldBeEmpty();
        }

        [Fact]
        public async Task GetParticipantsWithIncludesAsync_WithParticipants_ShouldReturnCorrectList()
        {
            // ARRANGE
            var gameId = 1;
            var participants = new List<GameParticipant>
        {
            new GameParticipant { GameId = gameId, ApplicationUserId = "user1" },
            new GameParticipant { GameId = 2, ApplicationUserId = "user2" }
        }.AsQueryable();
            var mockParticipantsDbSet = MockDbSetHelper.GetMockDbSet(participants);
            _mockContext.Setup(c => c.GameParticipants).Returns(mockParticipantsDbSet.Object);

            // ACT
            var result = await _service.GetParticipantsWithIncludesAsync(gameId);

            // ASSERT
            result.ShouldHaveSingleItem();
            result.First().ApplicationUserId.ShouldBe("user1");
        }

        [Fact]
        public async Task GetParticipantsWithIncludesAsync_WithNoParticipants_ShouldReturnEmptyList()
        {
            // ARRANGE
            var gameId = 1;
            var participants = new List<GameParticipant>().AsQueryable();
            var mockParticipantsDbSet = MockDbSetHelper.GetMockDbSet(participants);
            _mockContext.Setup(c => c.GameParticipants).Returns(mockParticipantsDbSet.Object);

            // ACT
            var result = await _service.GetParticipantsWithIncludesAsync(gameId);

            // ASSERT
            result.ShouldBeEmpty();
        }

        [Fact]
        public async Task GetParticipantsAsync_ForNonExistentGame_ShouldReturnEmptyList()
        {
            // ARRANGE
            var gameId = 999;
            var participants = new List<GameParticipant> { new GameParticipant { GameId = 1, ApplicationUserId = "user1" } }.AsQueryable();
            var mockParticipantsDbSet = MockDbSetHelper.GetMockDbSet(participants);
            _mockContext.Setup(c => c.GameParticipants).Returns(mockParticipantsDbSet.Object);

            // ACT
            var result = await _service.GetParticipantsAsync(gameId);

            // ASSERT
            result.ShouldBeEmpty();
        }
        #endregion

        #region Get Non-Participants Tests

        [Fact]
        public async Task GetNonParticipantsAsync_WithMixedUsers_ShouldReturnOnlyNonParticipants()
        {
            // ARRANGE
            var gameId = 1;
            var participantUser = new ApplicationUser { Id = "participant1", UserName = "Participant One", ColorHex = "#000000" };
            var nonParticipantUser = new ApplicationUser { Id = "nonparticipant1", UserName = "Non-Participant One", ColorHex = "#000000" };
            var allUsers = new List<ApplicationUser> { participantUser, nonParticipantUser }.AsQueryable();
            var participants = new List<GameParticipant> { new GameParticipant { GameId = gameId, ApplicationUserId = participantUser.Id } }.AsQueryable();

            var mockParticipantsDbSet = MockDbSetHelper.GetMockDbSet(participants);
            _mockContext.Setup(c => c.GameParticipants).Returns(mockParticipantsDbSet.Object);
            _mockUserManager.Setup(um => um.Users).Returns(allUsers);

            // ACT
            var result = await _service.GetNonParticipantsAsync(gameId);

            // ASSERT
            result.ShouldHaveSingleItem();
            result.First().Id.ShouldBe(nonParticipantUser.Id);
        }

        [Fact]
        public async Task GetNonParticipantsAsync_WhenAllUsersAreParticipants_ShouldReturnEmptyList()
        {
            // ARRANGE
            var gameId = 1;
            var user1 = new ApplicationUser { Id = "user1", ColorHex = "#000000" };
            var user2 = new ApplicationUser { Id = "user2", ColorHex = "#000000" };
            var allUsers = new List<ApplicationUser> { user1, user2 }.AsQueryable();
            var participants = new List<GameParticipant>
        {
            new GameParticipant { GameId = gameId, ApplicationUserId = user1.Id },
            new GameParticipant { GameId = gameId, ApplicationUserId = user2.Id }
        }.AsQueryable();
            var mockParticipantsDbSet = MockDbSetHelper.GetMockDbSet(participants);
            _mockContext.Setup(c => c.GameParticipants).Returns(mockParticipantsDbSet.Object);
            _mockUserManager.Setup(um => um.Users).Returns(allUsers);

            // ACT
            var result = await _service.GetNonParticipantsAsync(gameId);

            // ASSERT
            result.ShouldBeEmpty();
        }

        [Fact]
        public async Task GetNonParticipantsAsync_WhenNoUsersAreParticipants_ShouldReturnAllUsers()
        {
            // ARRANGE
            var gameId = 1;
            var user1 = new ApplicationUser { Id = "user1", ColorHex = "#000000" };
            var user2 = new ApplicationUser { Id = "user2", ColorHex = "#000000" };
            var allUsers = new List<ApplicationUser> { user1, user2 }.AsQueryable();
            var participants = new List<GameParticipant>().AsQueryable();
            var mockParticipantsDbSet = MockDbSetHelper.GetMockDbSet(participants);
            _mockContext.Setup(c => c.GameParticipants).Returns(mockParticipantsDbSet.Object);
            _mockUserManager.Setup(um => um.Users).Returns(allUsers);

            // ACT
            var result = await _service.GetNonParticipantsAsync(gameId);

            // ASSERT
            result.Count.ShouldBe(2);
        }

        [Fact]
        public async Task GetNonParticipantsAsync_WithNoUsersInSystem_ShouldReturnEmptyList()
        {
            // ARRANGE
            var gameId = 1;
            var allUsers = new List<ApplicationUser>().AsQueryable();
            var participants = new List<GameParticipant>().AsQueryable();
            var mockParticipantsDbSet = MockDbSetHelper.GetMockDbSet(participants);
            _mockContext.Setup(c => c.GameParticipants).Returns(mockParticipantsDbSet.Object);
            _mockUserManager.Setup(um => um.Users).Returns(allUsers);

            // ACT
            var result = await _service.GetNonParticipantsAsync(gameId);

            // ASSERT
            result.ShouldBeEmpty();
        }

        [Fact]
        public async Task GetNonParticipantsAsync_WhenParticipantListIsEmptyForGame_ShouldReturnAllUsers()
        {
            // ARRANGE
            var gameId = 1;
            var user1 = new ApplicationUser { Id = "user1", ColorHex = "#000000" };
            var user2 = new ApplicationUser { Id = "user2", ColorHex = "#000000" };
            var allUsers = new List<ApplicationUser> { user1, user2 }.AsQueryable();
            // Participants exist, but for another game
            var participants = new List<GameParticipant> { new GameParticipant { GameId = 99, ApplicationUserId = user1.Id } }.AsQueryable();
            var mockParticipantsDbSet = MockDbSetHelper.GetMockDbSet(participants);
            _mockContext.Setup(c => c.GameParticipants).Returns(mockParticipantsDbSet.Object);
            _mockUserManager.Setup(um => um.Users).Returns(allUsers);

            // ACT
            var result = await _service.GetNonParticipantsAsync(gameId);

            // ASSERT
            result.Count.ShouldBe(2);
        }
        #endregion

        #region Get All/By ID Tests

        [Fact]
        public async Task GetAllGamesAsync_WithMultipleGames_ShouldReturnOrderedByStartTimeDescending()
        {
            // ARRANGE
            var game1 = new Game { Id = 1, Name = "Old Game", StartTime = DateTime.UtcNow.AddDays(-2), EndTime = DateTime.UtcNow.AddHours(1) };
            var game2 = new Game { Id = 2, Name = "New Game", StartTime = DateTime.UtcNow.AddDays(-1), EndTime = DateTime.UtcNow.AddHours(1) };
            var games = new List<Game> { game1, game2 }.AsQueryable();
            var mockGamesDbSet = MockDbSetHelper.GetMockDbSet(games);
            _mockContext.Setup(c => c.Games).Returns(mockGamesDbSet.Object);

            // ACT
            var result = await _service.GetAllGamesAsync();

            // ASSERT
            result.Count.ShouldBe(2);
            result[0].Name.ShouldBe("New Game");
            result[1].Name.ShouldBe("Old Game");
        }

        [Fact]
        public async Task GetAllGamesAsync_WithUnorderedGames_ShouldReturnCorrectlyOrdered()
        {
            // ARRANGE
            var game1 = new Game { Id = 1, Name = "Middle Game", StartTime = DateTime.UtcNow.AddDays(-5), EndTime = DateTime.UtcNow.AddHours(1) };
            var game2 = new Game { Id = 2, Name = "Latest Game", StartTime = DateTime.UtcNow.AddDays(-1), EndTime = DateTime.UtcNow.AddHours(1) };
            var game3 = new Game { Id = 3, Name = "Oldest Game", StartTime = DateTime.UtcNow.AddDays(-10), EndTime = DateTime.UtcNow.AddHours(1) };
            var games = new List<Game> { game1, game2, game3 }.AsQueryable();
            var mockGamesDbSet = MockDbSetHelper.GetMockDbSet(games);
            _mockContext.Setup(c => c.Games).Returns(mockGamesDbSet.Object);

            // ACT
            var result = await _service.GetAllGamesAsync();

            // ASSERT
            result.Count.ShouldBe(3);
            result[0].Name.ShouldBe("Latest Game");
            result[1].Name.ShouldBe("Middle Game");
            result[2].Name.ShouldBe("Oldest Game");
        }

        [Fact]
        public async Task GetAllGamesAsync_WithNoGames_ShouldReturnEmptyList()
        {
            // ARRANGE
            var games = new List<Game>().AsQueryable();
            var mockGamesDbSet = MockDbSetHelper.GetMockDbSet(games);
            _mockContext.Setup(c => c.Games).Returns(mockGamesDbSet.Object);

            // ACT
            var result = await _service.GetAllGamesAsync();

            // ASSERT
            result.ShouldBeEmpty();
        }

        [Fact]
        public async Task GetGameByIdAsync_WithExistingId_ShouldReturnGame()
        {
            // ARRANGE
            var gameId = 42;
            var expectedGame = new Game { Id = gameId, Name = "The Game", StartTime = DateTime.UtcNow, EndTime = DateTime.UtcNow.AddHours(1) };
            _mockContext.Setup(c => c.Games.FindAsync(gameId)).ReturnsAsync(expectedGame);

            // ACT
            var result = await _service.GetGameByIdAsync(gameId);

            // ASSERT
            result.ShouldNotBeNull();
            result.Id.ShouldBe(gameId);
        }

        [Fact]
        public async Task GetGameByIdAsync_WithNonExistentId_ShouldReturnNull()
        {
            // ARRANGE
            var gameId = 999;
            _mockContext.Setup(c => c.Games.FindAsync(gameId)).ReturnsAsync((Game)null);

            // ACT
            var result = await _service.GetGameByIdAsync(gameId);

            // ASSERT
            result.ShouldBeNull();
        }
        #endregion

        #region Additional Positive Tests
        [Fact]
        public async Task CreateGameAsync_WithMaxLengthName_ShouldSucceed()
        {
            var name = new string('A',128);
            var mockGamesDbSet = new Mock<DbSet<Game>>();
            _mockContext.Setup(c => c.Games).Returns(mockGamesDbSet.Object);
            await _service.CreateGameAsync(name, DateTime.UtcNow, DateTime.UtcNow.AddHours(1));
            _mockContext.Verify(c => c.Games.Add(It.Is<Game>(g => g.Name == name)), Times.Once);
        }

        [Fact]
        public async Task CreateGameAsync_WithMinValidDateRange_ShouldSucceed()
        {
            var mockGamesDbSet = new Mock<DbSet<Game>>();
            _mockContext.Setup(c => c.Games).Returns(mockGamesDbSet.Object);
            var now = DateTime.UtcNow;
            await _service.CreateGameAsync("Test", now, now.AddSeconds(1));
            _mockContext.Verify(c => c.Games.Add(It.IsAny<Game>()), Times.Once);
        }

        [Fact]
        public async Task EndGameAsync_WithMultipleControlPoints_ShouldSetAllInactive()
        {
            var gameId =2;
            var activeGame = new Game { Id = gameId, Name = "Active", Status = GameStatus.Active, StartTime = DateTime.UtcNow, EndTime = DateTime.UtcNow.AddHours(1) };
            _mockContext.Setup(c => c.Games.FindAsync(gameId)).ReturnsAsync(activeGame);
            var cps = new List<ControlPoint> {
                new ControlPoint { Id =1, GameId = gameId, ApplicationUserId = "u1", Status = ControlPointStatus.Controlled },
                new ControlPoint { Id =2, GameId = gameId, ApplicationUserId = "u2", Status = ControlPointStatus.Controlled }
            }.AsQueryable();
            _mockContext.Setup(c => c.ControlPoints).Returns(MockDbSetHelper.GetMockDbSet(cps).Object);
            _mockContext.Setup(c => c.GameEvents).Returns(MockDbSetHelper.GetMockDbSet(new List<GameEvent>().AsQueryable()).Object);
            _mockScoreboardService.Setup(s => s.CalculateScoreboardAsync(gameId)).ReturnsAsync(new ScoreboardViewModel { TeamScores = new List<TeamScore>() });
            _mockContext.Setup(c => c.GameScores).Returns(MockDbSetHelper.GetMockDbSet(new List<GameScore>().AsQueryable()).Object);
            await _service.EndGameAsync(gameId);
            activeGame.Status.ShouldBe(GameStatus.Finished);
        }

        [Fact]
        public async Task AddParticipantAsync_WithSpecialCharacterUserId_ShouldAdd()
        {
            var gameId =1;
            var userId = "user@id";
            var participants = new List<GameParticipant>().AsQueryable();
            var mockParticipantsDbSet = MockDbSetHelper.GetMockDbSet(participants);
            _mockContext.Setup(c => c.GameParticipants).Returns(mockParticipantsDbSet.Object);
            await _service.AddParticipantAsync(gameId, userId);
            _mockContext.Verify(c => c.GameParticipants.Add(It.Is<GameParticipant>(p => p.ApplicationUserId == userId)), Times.Once);
        }

        [Fact]
        public async Task RemoveParticipantAsync_WithValidUserId_ShouldRemove()
        {
            var gameId =1;
            var userId = "user1";
            var participant = new GameParticipant { GameId = gameId, ApplicationUserId = userId };
            var mockParticipantsDbSet = new Mock<DbSet<GameParticipant>>();
            _mockContext.Setup(c => c.GameParticipants).Returns(mockParticipantsDbSet.Object);
            mockParticipantsDbSet.Setup(m => m.FindAsync(gameId, userId)).ReturnsAsync(participant);
            await _service.RemoveParticipantAsync(gameId, userId);
            _mockContext.Verify(c => c.GameParticipants.Remove(participant), Times.Once);
        }

        [Fact]
        public async Task GetParticipantsAsync_WithMultipleGames_ShouldReturnCorrectCount()
        {
            var gameId =1;
            var user1 = new ApplicationUser { Id = "u1", ColorHex = "#000000" };
            var user2 = new ApplicationUser { Id = "u2", ColorHex = "#000000" };
            var participants = new List<GameParticipant> {
                new GameParticipant { GameId = gameId, ApplicationUserId = user1.Id, ApplicationUser = user1 },
                new GameParticipant { GameId =2, ApplicationUserId = user2.Id, ApplicationUser = user2 }
            }.AsQueryable();
            _mockContext.Setup(c => c.GameParticipants).Returns(MockDbSetHelper.GetMockDbSet(participants).Object);
            var result = await _service.GetParticipantsAsync(gameId);
            result.Count.ShouldBe(1);
        }

        [Fact]
        public async Task GetNonParticipantsAsync_WithMultipleUsers_ShouldReturnCorrectUsers()
        {
            var gameId =1;
            var user1 = new ApplicationUser { Id = "u1", ColorHex = "#000000" };
            var user2 = new ApplicationUser { Id = "u2", ColorHex = "#000000" };
            var allUsers = new List<ApplicationUser> { user1, user2 }.AsQueryable();
            var participants = new List<GameParticipant> { new GameParticipant { GameId = gameId, ApplicationUserId = user1.Id } }.AsQueryable();
            _mockContext.Setup(c => c.GameParticipants).Returns(MockDbSetHelper.GetMockDbSet(participants).Object);
            _mockUserManager.Setup(um => um.Users).Returns(allUsers);
            var result = await _service.GetNonParticipantsAsync(gameId);
            result.Count.ShouldBe(1);
            result.First().Id.ShouldBe("u2");
        }

        [Fact]
        public async Task GetGameByIdAsync_WithValidId_ShouldReturnGame()
        {
            var gameId =123;
            var game = new Game { Id = gameId, Name = "Valid Game", StartTime = DateTime.UtcNow, EndTime = DateTime.UtcNow.AddHours(1) };
            _mockContext.Setup(c => c.Games.FindAsync(gameId)).ReturnsAsync(game);
            var result = await _service.GetGameByIdAsync(gameId);
            result.ShouldNotBeNull();
            result.Name.ShouldBe("Valid Game");
        }

        #endregion

        #region Additional Negative Tests
        [Fact]
        public async Task CreateGameAsync_WithEmptyName_ShouldNotAddGame()
        {
            var mockGamesDbSet = new Mock<DbSet<Game>>();
            _mockContext.Setup(c => c.Games).Returns(mockGamesDbSet.Object);
            await _service.CreateGameAsync("", DateTime.UtcNow, DateTime.UtcNow.AddHours(1));
            _mockContext.Verify(c => c.Games.Add(It.IsAny<Game>()), Times.Never);
        }

        [Fact]
        public async Task CreateGameAsync_WithWhitespaceName_ShouldNotAddGame()
        {
            var mockGamesDbSet = new Mock<DbSet<Game>>();
            _mockContext.Setup(c => c.Games).Returns(mockGamesDbSet.Object);
            await _service.CreateGameAsync(" ", DateTime.UtcNow, DateTime.UtcNow.AddHours(1));
            _mockContext.Verify(c => c.Games.Add(It.IsAny<Game>()), Times.Never);
        }

        [Fact]
        public async Task CreateGameAsync_WithEndTimeBeforeStartTime_ShouldNotAddGame_Negative()
        {
            var mockGamesDbSet = new Mock<DbSet<Game>>();
            _mockContext.Setup(c => c.Games).Returns(mockGamesDbSet.Object);
            await _service.CreateGameAsync("Test", DateTime.UtcNow, DateTime.UtcNow.AddHours(-1));
            _mockContext.Verify(c => c.Games.Add(It.IsAny<Game>()), Times.Never);
        }

        [Fact]
        public async Task StartGameAsync_WithInvalidId_ShouldDoNothing()
        {
            await _service.StartGameAsync(-1);
            _mockContext.Verify(c => c.SaveChangesAsync(default), Times.Never);
        }

        [Fact]
        public async Task StartGameAsync_WhenAllGamesAreFinished_ShouldDoNothing()
        {
            var gameId =1;
            var finishedGame = new Game { Id = gameId, Name = "Scheduled", Status = GameStatus.Finished, StartTime = DateTime.UtcNow, EndTime = DateTime.UtcNow.AddHours(1) };
            var games = new List<Game> { finishedGame }.AsQueryable();
            var mockGamesDbSet = MockDbSetHelper.GetMockDbSet(games);
            _mockContext.Setup(c => c.Games).Returns(mockGamesDbSet.Object);
            _mockContext.Setup(c => c.Games.FindAsync(gameId)).ReturnsAsync(finishedGame);
            await _service.StartGameAsync(gameId);
            _mockContext.Verify(c => c.SaveChangesAsync(default), Times.Never);
        }

        [Fact]
        public async Task EndGameAsync_WithInvalidId_ShouldDoNothing()
        {
            await _service.EndGameAsync(-1);
            _mockContext.Verify(c => c.SaveChangesAsync(default), Times.Never);
        }

        [Fact]
        public async Task AddParticipantAsync_WithNullUserId_ShouldNotAdd()
        {
            var gameId =1;
            await _service.AddParticipantAsync(gameId, null);
            _mockContext.Verify(c => c.GameParticipants.Add(It.IsAny<GameParticipant>()), Times.Never);
        }

        [Fact]
        public async Task RemoveParticipantAsync_WithNullUserId_ShouldNotRemove()
        {
            var gameId =1;
            var mockParticipantsDbSet = new Mock<DbSet<GameParticipant>>();
            _mockContext.Setup(c => c.GameParticipants).Returns(mockParticipantsDbSet.Object);
            await _service.RemoveParticipantAsync(gameId, null);
            _mockContext.Verify(c => c.GameParticipants.Remove(It.IsAny<GameParticipant>()), Times.Never);
        }

        [Fact]
        public async Task GetParticipantsAsync_ForNonExistentGame_ShouldReturnEmpty()
        {
            var gameId =999;
            var participants = new List<GameParticipant>().AsQueryable();
            _mockContext.Setup(c => c.GameParticipants).Returns(MockDbSetHelper.GetMockDbSet(participants).Object);
            var result = await _service.GetParticipantsAsync(gameId);
            result.ShouldBeEmpty();
        }

        [Fact]
        public async Task GetNonParticipantsAsync_WhenNoUsersExist_ShouldReturnEmpty()
        {
            var gameId =1;
            var allUsers = new List<ApplicationUser>().AsQueryable();
            var participants = new List<GameParticipant>().AsQueryable();
            _mockContext.Setup(c => c.GameParticipants).Returns(MockDbSetHelper.GetMockDbSet(participants).Object);
            _mockUserManager.Setup(um => um.Users).Returns(allUsers);
            var result = await _service.GetNonParticipantsAsync(gameId);
            result.ShouldBeEmpty();
        }
        #endregion


        #region ResetGameAsync Tests

        [Fact]
        public async Task ResetGameAsync_ShouldCallEndGameAsync()
        {
            // ARRANGE
            var gameId = 10;
            var game = new Game
            {
                Id = gameId,
                Name = "Active Game",
                Status = GameStatus.Active,
                StartTime = DateTime.UtcNow,
                EndTime = DateTime.UtcNow.AddHours(1)
            };

            var emptyControlPoints = new List<ControlPoint>().AsQueryable();
            var emptyGameScores = new List<GameScore>().AsQueryable();
            var emptyGameParticipants = new List<GameParticipant>().AsQueryable();

            _mockContext.Setup(c => c.Games.FindAsync(gameId)).ReturnsAsync(game);
            _mockContext.Setup(c => c.ControlPoints).Returns(MockDbSetHelper.GetMockDbSet(emptyControlPoints).Object);
            _mockContext.Setup(c => c.GameScores).Returns(MockDbSetHelper.GetMockDbSet(emptyGameScores).Object);
            _mockContext.Setup(c => c.GameParticipants).Returns(MockDbSetHelper.GetMockDbSet(emptyGameParticipants).Object);
            _mockContext.Setup(c => c.GameEvents).Returns(MockDbSetHelper.GetMockDbSet(new List<GameEvent>().AsQueryable()).Object);

            _mockScoreboardService.Setup(s => s.CalculateScoreboardAsync(gameId))
                .ReturnsAsync(new ScoreboardViewModel { TeamScores = new List<TeamScore>() });

            // ACT
            await _service.ResetGameAsync(gameId);

            // ASSERT
            game.Status.ShouldBe(GameStatus.Finished);
            _mockContext.Verify(c => c.SaveChangesAsync(default), Times.AtLeastOnce);
        }

        [Fact]
        public async Task ResetGameAsync_WithNonExistentGame_ShouldDoNothing()
        {
            // ARRANGE
            var gameId = 999;
            _mockContext.Setup(c => c.Games.FindAsync(gameId)).ReturnsAsync((Game)null);

            // ACT
            await _service.ResetGameAsync(gameId);

            // ASSERT
            _mockContext.Verify(c => c.SaveChangesAsync(default), Times.Never);
        }

        [Fact]
        public async Task ResetGameAsync_WithScheduledGame_ShouldNotEnd()
        {
            // ARRANGE
            var gameId = 11;
            var game = new Game
            {
                Id = gameId,
                Name = "Scheduled Game",
                Status = GameStatus.Scheduled,
                StartTime = DateTime.UtcNow,
                EndTime = DateTime.UtcNow.AddHours(1)
            };

            _mockContext.Setup(c => c.Games.FindAsync(gameId)).ReturnsAsync(game);

            // ACT
            await _service.ResetGameAsync(gameId);

            // ASSERT
            game.Status.ShouldBe(GameStatus.Scheduled);
            _mockContext.Verify(c => c.SaveChangesAsync(default), Times.Never);
        }

        #endregion

        #region EndGameAsync Additional Edge Cases

        [Fact]
        public async Task EndGameAsync_WithOwnedControlPoints_ShouldCreateGameEndEvents()
        {
            // ARRANGE
            var gameId = 20;
            var userId = "user123";
            var game = new Game
            {
                Id = gameId,
                Name = "Game With Owned CPs",
                Status = GameStatus.Active,
                StartTime = DateTime.UtcNow,
                EndTime = DateTime.UtcNow.AddHours(1)
            };

            var ownedControlPoints = new List<ControlPoint>
    {
        new ControlPoint { Id = 1, GameId = gameId, ApplicationUserId = userId, PositionX = 1, PositionY = 1 },
        new ControlPoint { Id = 2, GameId = gameId, ApplicationUserId = userId, PositionX = 2, PositionY = 2 }
    }.AsQueryable();

            var mockCpDbSet = MockDbSetHelper.GetMockDbSet(ownedControlPoints);
            var mockGameEventsDbSet = MockDbSetHelper.GetMockDbSet(new List<GameEvent>().AsQueryable());
            var mockGameScoresDbSet = MockDbSetHelper.GetMockDbSet(new List<GameScore>().AsQueryable());
            var mockGameParticipantsDbSet = MockDbSetHelper.GetMockDbSet(new List<GameParticipant>().AsQueryable());

            _mockContext.Setup(c => c.Games.FindAsync(gameId)).ReturnsAsync(game);
            _mockContext.Setup(c => c.ControlPoints).Returns(mockCpDbSet.Object);
            _mockContext.Setup(c => c.GameEvents).Returns(mockGameEventsDbSet.Object);
            _mockContext.Setup(c => c.GameScores).Returns(mockGameScoresDbSet.Object);
            _mockContext.Setup(c => c.GameParticipants).Returns(mockGameParticipantsDbSet.Object);

            _mockScoreboardService.Setup(s => s.CalculateScoreboardAsync(gameId))
                .ReturnsAsync(new ScoreboardViewModel { TeamScores = new List<TeamScore>() });

            // ACT
            await _service.EndGameAsync(gameId);

            // ASSERT
            mockGameEventsDbSet.Verify(m => m.Add(It.IsAny<GameEvent>()), Times.Exactly(2));
            game.Status.ShouldBe(GameStatus.Finished);
        }

        [Fact]
        public async Task EndGameAsync_ShouldSaveFinalScoresForParticipants()
        {
            // ARRANGE
            var gameId = 22;
            var userId = "user123";
            var userName = "Team Red";

            var game = new Game
            {
                Id = gameId,
                Name = "Game With Participants",
                Status = GameStatus.Active,
                StartTime = DateTime.UtcNow,
                EndTime = DateTime.UtcNow.AddHours(1)
            };

            var user = new ApplicationUser
            {
                Id = userId,
                UserName = userName,
                ColorHex = "#FF0000"
            };

            var participants = new List<GameParticipant>
    {
        new GameParticipant { GameId = gameId, ApplicationUserId = userId, ApplicationUser = user }
    }.AsQueryable();

            var mockParticipantsDbSet = MockDbSetHelper.GetMockDbSet(participants);
            var mockGameScoresDbSet = MockDbSetHelper.GetMockDbSet(new List<GameScore>().AsQueryable());
            var mockCpDbSet = MockDbSetHelper.GetMockDbSet(new List<ControlPoint>().AsQueryable());
            var mockGameEventsDbSet = MockDbSetHelper.GetMockDbSet(new List<GameEvent>().AsQueryable());

            _mockContext.Setup(c => c.Games.FindAsync(gameId)).ReturnsAsync(game);
            _mockContext.Setup(c => c.GameParticipants).Returns(mockParticipantsDbSet.Object);
            _mockContext.Setup(c => c.GameScores).Returns(mockGameScoresDbSet.Object);
            _mockContext.Setup(c => c.ControlPoints).Returns(mockCpDbSet.Object);
            _mockContext.Setup(c => c.GameEvents).Returns(mockGameEventsDbSet.Object);

            var scoreboard = new ScoreboardViewModel
            {
                TeamScores = new List<TeamScore>
        {
            new TeamScore { TeamName = userName, TotalScoreFromDb = 500 }
        }
            };

            _mockScoreboardService.Setup(s => s.CalculateScoreboardAsync(gameId))
                .ReturnsAsync(scoreboard);

            // ACT
            await _service.EndGameAsync(gameId);

            // ASSERT
            mockGameScoresDbSet.Verify(m => m.Add(It.Is<GameScore>(gs =>
                gs.GameId == gameId &&
                gs.ApplicationUserId == userId &&
                gs.Points == 500)), Times.Once);
            game.Status.ShouldBe(GameStatus.Finished);
        }

        [Fact]
        public async Task EndGameAsync_WithInvalidGameId_ShouldReturnEarly()
        {
            // ARRANGE
            var invalidGameId = -1;

            // ACT
            await _service.EndGameAsync(invalidGameId);

            // ASSERT
            _mockContext.Verify(c => c.Games.FindAsync(It.IsAny<int>()), Times.Never);
            _mockContext.Verify(c => c.SaveChangesAsync(default), Times.Never);
        }

        [Fact]
        public async Task EndGameAsync_WithZeroGameId_ShouldReturnEarly()
        {
            // ARRANGE & ACT
            await _service.EndGameAsync(0);

            // ASSERT
            _mockContext.Verify(c => c.Games.FindAsync(It.IsAny<int>()), Times.Never);
            _mockContext.Verify(c => c.SaveChangesAsync(default), Times.Never);
        }

        [Fact]
        public async Task EndGameAsync_WithFinishedGame_ShouldNotEnd()
        {
            // ARRANGE
            var gameId = 23;
            var game = new Game
            {
                Id = gameId,
                Name = "Already Finished",
                Status = GameStatus.Finished,
                StartTime = DateTime.UtcNow,
                EndTime = DateTime.UtcNow.AddHours(1)
            };

            _mockContext.Setup(c => c.Games.FindAsync(gameId)).ReturnsAsync(game);

            // ACT
            await _service.EndGameAsync(gameId);

            // ASSERT
            game.Status.ShouldBe(GameStatus.Finished);
            _mockContext.Verify(c => c.SaveChangesAsync(default), Times.Never);
        }

        [Fact]
        public async Task EndGameAsync_ShouldSetAllControlPointsToInactive()
        {
            // ARRANGE
            var gameId = 24;
            var game = new Game
            {
                Id = gameId,
                Name = "Game To End",
                Status = GameStatus.Active,
                StartTime = DateTime.UtcNow,
                EndTime = DateTime.UtcNow.AddHours(1)
            };

            var controlPoints = new List<ControlPoint>
    {
        new ControlPoint { Id = 1, GameId = gameId, Status = ControlPointStatus.Controlled, ApplicationUserId = "user1" },
        new ControlPoint { Id = 2, GameId = gameId, Status = ControlPointStatus.Controlled, ApplicationUserId = "user2" }
    }.AsQueryable();

            var mockCpDbSet = MockDbSetHelper.GetMockDbSet(controlPoints);

            _mockContext.Setup(c => c.Games.FindAsync(gameId)).ReturnsAsync(game);
            _mockContext.Setup(c => c.ControlPoints).Returns(mockCpDbSet.Object);
            _mockContext.Setup(c => c.GameEvents).Returns(MockDbSetHelper.GetMockDbSet(new List<GameEvent>().AsQueryable()).Object);
            _mockContext.Setup(c => c.GameScores).Returns(MockDbSetHelper.GetMockDbSet(new List<GameScore>().AsQueryable()).Object);
            _mockContext.Setup(c => c.GameParticipants).Returns(MockDbSetHelper.GetMockDbSet(new List<GameParticipant>().AsQueryable()).Object);

            _mockScoreboardService.Setup(s => s.CalculateScoreboardAsync(gameId))
                .ReturnsAsync(new ScoreboardViewModel { TeamScores = new List<TeamScore>() });

            // ACT
            await _service.EndGameAsync(gameId);

            // ASSERT - Control points should be set to inactive
            foreach (var cp in controlPoints)
            {
                cp.Status.ShouldBe(ControlPointStatus.Inactive);
                cp.ApplicationUserId.ShouldBeNull();
            }
            game.Status.ShouldBe(GameStatus.Finished);
        }

        #endregion



    }
}