using DominationPoint.Core.Application;
using DominationPoint.Core.Application.Services;
using DominationPoint.Core.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shouldly;
using System.Linq.Expressions;

namespace DominationPointTests.UnitTests.Services
{
    public class GameplayServiceTests
    {
        private readonly Mock<IApplicationDbContext> _mockContext;
        private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
        private GameplayService _service; // Remove readonly so we can reassign

        // Standard testdata
        private readonly ApplicationUser _testUser;
        private readonly ControlPoint _testControlPoint;
        private readonly Game _activeGame;
        private readonly string _correctNumpadCode = "123a";

        public GameplayServiceTests()
        {
            _mockContext = new Mock<IApplicationDbContext>();

            var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
            _mockUserManager = new Mock<UserManager<ApplicationUser>>(
                userStoreMock.Object, null, null, null, null, null, null, null, null);

            // Initiera återanvändbar testdata
            _testUser = new ApplicationUser { Id = "test-user-id", UserName = "Test Team", ColorHex = "#FFFFFF", NumpadCode = _correctNumpadCode };
            _activeGame = new Game { Id = 1, Status = GameStatus.Active, Name = "Active Game", StartTime = System.DateTime.UtcNow, EndTime = System.DateTime.UtcNow.AddHours(1) };
            _testControlPoint = new ControlPoint { Id = 101, GameId = _activeGame.Id, Status = ControlPointStatus.Inactive, ApplicationUserId = null };
        }

        private void SetupDefaultSuccessMocks()
        {
            // Mocka UserManager för att hitta vår testanvändare
            _mockUserManager.Setup(um => um.FindByIdAsync(_testUser.Id)).ReturnsAsync(_testUser);

            // Mocka DbContext för att hitta kontrollpunkten
            _mockContext.Setup(c => c.ControlPoints.FindAsync(_testControlPoint.Id)).ReturnsAsync(_testControlPoint);

            // Mocka DbContext för att hitta det aktiva spelet
            var games = new List<Game> { _activeGame }.AsQueryable();
            var mockGamesDbSet = MockDbSetHelper.GetMockDbSet(games);
            _mockContext.Setup(c => c.Games).Returns(mockGamesDbSet.Object);

            // Mocka DbContext för GameEvents med backing list
            var gameEventsList = new List<GameEvent>();
            var mockGameEventsDbSet = new Mock<DbSet<GameEvent>>();
            mockGameEventsDbSet.As<IQueryable<GameEvent>>().Setup(m => m.Provider).Returns(gameEventsList.AsQueryable().Provider);
            mockGameEventsDbSet.As<IQueryable<GameEvent>>().Setup(m => m.Expression).Returns(gameEventsList.AsQueryable().Expression);
            mockGameEventsDbSet.As<IQueryable<GameEvent>>().Setup(m => m.ElementType).Returns(gameEventsList.AsQueryable().ElementType);
            mockGameEventsDbSet.As<IQueryable<GameEvent>>().Setup(m => m.GetEnumerator()).Returns(() => gameEventsList.GetEnumerator());
            mockGameEventsDbSet.Setup(m => m.Add(It.IsAny<GameEvent>())).Callback<GameEvent>(e => gameEventsList.Add(e));
            _mockContext.Setup(c => c.GameEvents).Returns(mockGameEventsDbSet.Object);

            // Re-create the service after all setups
            _service = new GameplayService(_mockContext.Object, _mockUserManager.Object);
        }

        #region Positive Scenarios
        [Fact]
        public async Task CaptureControlPointAsync_WithValidData_ShouldReturnSuccessTrueAndCorrectMessage()
        {
            // ARRANGE
            SetupDefaultSuccessMocks();

            // ACT
            var (success, message) = await _service.CaptureControlPointAsync(_testControlPoint.Id, _testUser.Id, _correctNumpadCode);

            // ASSERT
            success.ShouldBeTrue();
            message.ShouldBe($"Control Point {_testControlPoint.Id} captured by team {_testUser.UserName}.");
        }

        [Fact]
        public async Task CaptureControlPointAsync_WithValidData_ShouldUpdateControlPointState()
        {
            // ARRANGE
            SetupDefaultSuccessMocks();

            // ACT
            await _service.CaptureControlPointAsync(_testControlPoint.Id, _testUser.Id, _correctNumpadCode);

            // ASSERT
            _testControlPoint.Status.ShouldBe(ControlPointStatus.Controlled);
            _testControlPoint.ApplicationUserId.ShouldBe(_testUser.Id);
        }

        [Fact]
        public async Task CaptureControlPointAsync_WithValidData_ShouldCallSaveChangesAsync()
        {
            // ARRANGE
            SetupDefaultSuccessMocks();

            // ACT
            await _service.CaptureControlPointAsync(_testControlPoint.Id, _testUser.Id, _correctNumpadCode);

            // ASSERT
            _mockContext.Verify(c => c.SaveChangesAsync(default), Times.Once);
        }

        [Fact]
        public async Task CaptureControlPointAsync_WithValidData_ShouldCreateGameEvent()
        {
            // ARRANGE
            SetupDefaultSuccessMocks();

            // ACT
            await _service.CaptureControlPointAsync(_testControlPoint.Id, _testUser.Id, _correctNumpadCode);

            // ASSERT
            _mockContext.Verify(c => c.GameEvents.Add(It.Is<GameEvent>(e =>
                e.GameId == _activeGame.Id &&
                e.ControlPointId == _testControlPoint.Id &&
                e.Type == EventType.Capture &&
                e.ActingUserId == _testUser.Id &&
                e.PreviousOwnerUserId == null
            )), Times.Once);
        }

        [Fact]
        public async Task CaptureControlPointAsync_TakingPointFromAnotherTeam_ShouldSetPreviousOwnerInEvent()
        {
            // ARRANGE
            SetupDefaultSuccessMocks();
            var previousOwnerId = "previous-owner-id";
            _testControlPoint.ApplicationUserId = previousOwnerId; // Set a previous owner

            // ACT
            await _service.CaptureControlPointAsync(_testControlPoint.Id, _testUser.Id, _correctNumpadCode);

            // ASSERT
            _mockContext.Verify(c => c.GameEvents.Add(It.Is<GameEvent>(e =>
                e.PreviousOwnerUserId == previousOwnerId
            )), Times.Once);
        }

        [Fact]
        public async Task CaptureControlPointAsync_RecapturingOwnPoint_ShouldStillCreateEvent()
        {
            // ARRANGE
            SetupDefaultSuccessMocks();
            _testControlPoint.ApplicationUserId = _testUser.Id; // Already owned by the same user

            // ACT
            await _service.CaptureControlPointAsync(_testControlPoint.Id, _testUser.Id, _correctNumpadCode);

            // ASSERT
            // Should create an event where previous and current owner are the same
            _mockContext.Verify(c => c.GameEvents.Add(It.Is<GameEvent>(e =>
                e.PreviousOwnerUserId == _testUser.Id &&
                e.ActingUserId == _testUser.Id
            )), Times.Once);
            _mockContext.Verify(c => c.SaveChangesAsync(default), Times.Once);
        }

        [Fact]
        public async Task CaptureControlPointAsync_WithValidData_ShouldSetEventTimestampToRecentTime()
        {
            // ARRANGE
            SetupDefaultSuccessMocks();
            var before = DateTime.UtcNow.AddSeconds(-1);

            // ACT
            await _service.CaptureControlPointAsync(_testControlPoint.Id, _testUser.Id, _correctNumpadCode);
            var after = DateTime.UtcNow.AddSeconds(1);

            // ASSERT
            _mockContext.Verify(c => c.GameEvents.Add(It.Is<GameEvent>(e =>
                e.Timestamp >= before && e.Timestamp <= after
            )), Times.Once);
        }

        [Fact]
        public async Task CaptureControlPointAsync_WithUserNameNull_ShouldHandleInMessage()
        {
            // ARRANGE
            SetupDefaultSuccessMocks();
            _testUser.UserName = null;

            // ACT
            var (success, message) = await _service.CaptureControlPointAsync(_testControlPoint.Id, _testUser.Id, _correctNumpadCode);

            // ASSERT
            success.ShouldBeTrue();
            message.ShouldBe($"Control Point {_testControlPoint.Id} captured by team .");
        }

        [Fact]
        public async Task CaptureControlPointAsync_WithControlPointBelongingToDifferentGame_ShouldStillSucceed()
        {
            // ARRANGE
            SetupDefaultSuccessMocks();
            _testControlPoint.GameId = 999; // Different from active game

            // ACT
            var (success, message) = await _service.CaptureControlPointAsync(_testControlPoint.Id, _testUser.Id, _correctNumpadCode);

            // ASSERT
            success.ShouldBeTrue();
            _mockContext.Verify(c => c.GameEvents.Add(It.Is<GameEvent>(e => e.GameId == _activeGame.Id)), Times.Once);
        }

        [Fact]
        public async Task CaptureControlPointAsync_WithAlreadyControlledPoint_ShouldRemainControlled()
        {
            // ARRANGE
            SetupDefaultSuccessMocks();
            _testControlPoint.Status = ControlPointStatus.Controlled;

            // ACT
            await _service.CaptureControlPointAsync(_testControlPoint.Id, _testUser.Id, _correctNumpadCode);

            // ASSERT
            _testControlPoint.Status.ShouldBe(ControlPointStatus.Controlled);
        }

        [Fact]
        public async Task CaptureControlPointAsync_RecapturingOwnPoint_ShouldReturnSameMessage()
        {
            // ARRANGE
            SetupDefaultSuccessMocks();
            _testControlPoint.ApplicationUserId = _testUser.Id;

            // ACT
            var (success, message) = await _service.CaptureControlPointAsync(_testControlPoint.Id, _testUser.Id, _correctNumpadCode);

            // ASSERT
            success.ShouldBeTrue();
            message.ShouldBe($"Control Point {_testControlPoint.Id} captured by team {_testUser.UserName}.");
        }

        #endregion

        #region Negative Scenarios (Validation Failures)

        [Fact]
        public async Task CaptureControlPointAsync_WithInvalidNumpadCode_ShouldReturnFailure()
        {
            // ARRANGE
            SetupDefaultSuccessMocks();
            var wrongCode = "9999";

            // ACT
            var (success, message) = await _service.CaptureControlPointAsync(_testControlPoint.Id, _testUser.Id, wrongCode);

            // ASSERT
            success.ShouldBeFalse();
            message.ShouldBe("Invalid user or code.");
            _mockContext.Verify(c => c.SaveChangesAsync(default), Times.Never);
        }

        [Fact]
        public async Task CaptureControlPointAsync_WithNullNumpadCodeInRequest_ShouldReturnFailure()
        {
            // ARRANGE
            SetupDefaultSuccessMocks();

            // ACT
            var (success, message) = await _service.CaptureControlPointAsync(_testControlPoint.Id, _testUser.Id, null);

            // ASSERT
            success.ShouldBeFalse();
            message.ShouldBe("Invalid user or code.");
            _mockContext.Verify(c => c.SaveChangesAsync(default), Times.Never);
        }

        [Fact]
        public async Task CaptureControlPointAsync_WithUserHavingNullNumpadCode_ShouldReturnFailure()
        {
            // ARRANGE
            _testUser.NumpadCode = null; // User in DB has no code set
            SetupDefaultSuccessMocks();

            // ACT
            var (success, message) = await _service.CaptureControlPointAsync(_testControlPoint.Id, _testUser.Id, _correctNumpadCode);

            // ASSERT
            success.ShouldBeFalse();
            message.ShouldBe("Invalid user or code.");
        }

        [Fact]
        public async Task CaptureControlPointAsync_WithNonExistentUser_ShouldReturnFailure()
        {
            // ARRANGE
            SetupDefaultSuccessMocks();
            var nonExistentUserId = "ghost-user";
            _mockUserManager.Setup(um => um.FindByIdAsync(nonExistentUserId)).ReturnsAsync((ApplicationUser)null);

            // ACT
            var (success, message) = await _service.CaptureControlPointAsync(_testControlPoint.Id, nonExistentUserId, _correctNumpadCode);

            // ASSERT
            success.ShouldBeFalse();
            message.ShouldBe("Invalid user or code.");
            _mockContext.Verify(c => c.SaveChangesAsync(default), Times.Never);
        }

        [Fact]
        public async Task CaptureControlPointAsync_WithNonExistentControlPoint_ShouldReturnFailure()
        {
            // ARRANGE
            SetupDefaultSuccessMocks();
            var nonExistentCpId = 999;
            _mockContext.Setup(c => c.ControlPoints.FindAsync(nonExistentCpId)).ReturnsAsync((ControlPoint)null);

            // ACT
            var (success, message) = await _service.CaptureControlPointAsync(nonExistentCpId, _testUser.Id, _correctNumpadCode);

            // ASSERT
            success.ShouldBeFalse();
            message.ShouldBe("Control Point not found.");
            _mockContext.Verify(c => c.SaveChangesAsync(default), Times.Never);
        }

        [Fact]
        public async Task CaptureControlPointAsync_WhenNoGameIsActive_ShouldReturnFailure()
        {
            // ARRANGE
            SetupDefaultSuccessMocks();
            var games = new List<Game> {
            new Game { Id = 2, Status = GameStatus.Scheduled, Name = "Scheduled Game", StartTime = System.DateTime.UtcNow, EndTime = System.DateTime.UtcNow.AddHours(1) },
            new Game { Id = 3, Status = GameStatus.Finished, Name = "Finished Game", StartTime = System.DateTime.UtcNow, EndTime = System.DateTime.UtcNow.AddHours(1) }
        }.AsQueryable();
            var mockGamesDbSet = MockDbSetHelper.GetMockDbSet(games);
            _mockContext.Setup(c => c.Games).Returns(mockGamesDbSet.Object);

            // ACT
            var (success, message) = await _service.CaptureControlPointAsync(_testControlPoint.Id, _testUser.Id, _correctNumpadCode);

            // ASSERT
            success.ShouldBeFalse();
            message.ShouldBe("No game is currently active.");
            _mockContext.Verify(c => c.SaveChangesAsync(default), Times.Never);
        }

        [Fact]
        public async Task CaptureControlPointAsync_WhenMultipleGamesAreActive_ShouldSucceedAndUseFirstName()
        {
            // ARRANGE
            SetupDefaultSuccessMocks();
            var anotherActiveGame = new Game { Id = 2, Status = GameStatus.Active, Name = "Another Active Game", StartTime = System.DateTime.UtcNow, EndTime = System.DateTime.UtcNow.AddHours(1) };
            var games = new List<Game> { _activeGame, anotherActiveGame }.AsQueryable();
            var mockGamesDbSet = MockDbSetHelper.GetMockDbSet(games);
            _mockContext.Setup(c => c.Games).Returns(mockGamesDbSet.Object);

            // ACT
            var (success, message) = await _service.CaptureControlPointAsync(_testControlPoint.Id, _testUser.Id, _correctNumpadCode);

            // ASSERT
            success.ShouldBeTrue(); // Should still succeed by picking the first one
            _mockContext.Verify(c => c.GameEvents.Add(It.Is<GameEvent>(e => e.GameId == _activeGame.Id)), Times.Once);
            _mockContext.Verify(c => c.SaveChangesAsync(default), Times.Once);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task CaptureControlPointAsync_WithInvalidControlPointId_ShouldReturnFailure(int invalidCpId)
        {
            // ARRANGE
            SetupDefaultSuccessMocks();
            _mockContext.Setup(c => c.ControlPoints.FindAsync(invalidCpId)).ReturnsAsync((ControlPoint)null);

            // ACT
            var (success, message) = await _service.CaptureControlPointAsync(invalidCpId, _testUser.Id, _correctNumpadCode);

            // ASSERT
            success.ShouldBeFalse();
            message.ShouldBe("Control Point not found.");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task CaptureControlPointAsync_WithInvalidUserId_ShouldReturnFailure(string invalidUserId)
        {
            // ARRANGE
            SetupDefaultSuccessMocks();
            _mockUserManager.Setup(um => um.FindByIdAsync(invalidUserId)).ReturnsAsync((ApplicationUser)null);

            // ACT
            var (success, message) = await _service.CaptureControlPointAsync(_testControlPoint.Id, invalidUserId, _correctNumpadCode);

            // ASSERT
            success.ShouldBeFalse();
            message.ShouldBe("Invalid user or code.");
        }

        [Fact]
        public async Task CaptureControlPointAsync_WithEmptyStringNumpadCode_ShouldReturnFailure()
        {
            // ARRANGE
            SetupDefaultSuccessMocks();

            // ACT
            var (success, message) = await _service.CaptureControlPointAsync(_testControlPoint.Id, _testUser.Id, "");

            // ASSERT
            success.ShouldBeFalse();
            message.ShouldBe("Invalid user or code.");
            _mockContext.Verify(c => c.SaveChangesAsync(default), Times.Never);
        }

        [Fact]
        public async Task CaptureControlPointAsync_WithWhitespaceUserId_ShouldReturnFailure()
        {
            // ARRANGE
            SetupDefaultSuccessMocks();

            // ACT
            var (success, message) = await _service.CaptureControlPointAsync(_testControlPoint.Id, "   ", _correctNumpadCode);

            // ASSERT
            success.ShouldBeFalse();
            message.ShouldBe("Invalid user or code.");
            _mockContext.Verify(c => c.SaveChangesAsync(default), Times.Never);
        }

        [Fact]
        public async Task CaptureControlPointAsync_WithWhitespaceNumpadCode_ShouldReturnFailure()
        {
            // ARRANGE
            SetupDefaultSuccessMocks();

            // ACT
            var (success, message) = await _service.CaptureControlPointAsync(_testControlPoint.Id, _testUser.Id, "   ");

            // ASSERT
            success.ShouldBeFalse();
            message.ShouldBe("Invalid user or code.");
            _mockContext.Verify(c => c.SaveChangesAsync(default), Times.Never);
        }

        [Fact]
        public async Task CaptureControlPointAsync_WithUserHavingWhitespaceNumpadCode_ShouldReturnFailure()
        {
            // ARRANGE
            _testUser.NumpadCode = "   "; // User in DB has whitespace code
            SetupDefaultSuccessMocks();

            // ACT
            var (success, message) = await _service.CaptureControlPointAsync(_testControlPoint.Id, _testUser.Id, _correctNumpadCode);

            // ASSERT
            success.ShouldBeFalse();
            message.ShouldBe("Invalid user or code.");
        }

        [Fact]
        public async Task CaptureControlPointAsync_WhenMultipleActiveGames_ShouldUseFirstByOrder()
        {
            // ARRANGE
            SetupDefaultSuccessMocks();
            var firstActiveGame = new Game { Id = 1, Status = GameStatus.Active, Name = "First Active Game", StartTime = System.DateTime.UtcNow, EndTime = System.DateTime.UtcNow.AddHours(1) };
            var secondActiveGame = new Game { Id = 2, Status = GameStatus.Active, Name = "Second Active Game", StartTime = System.DateTime.UtcNow, EndTime = System.DateTime.UtcNow.AddHours(1) };
            var games = new List<Game> { secondActiveGame, firstActiveGame }.AsQueryable(); // Second first in list
            var mockGamesDbSet = MockDbSetHelper.GetMockDbSet(games);
            _mockContext.Setup(c => c.Games).Returns(mockGamesDbSet.Object);

            // ACT
            var (success, message) = await _service.CaptureControlPointAsync(_testControlPoint.Id, _testUser.Id, _correctNumpadCode);

            // ASSERT
            success.ShouldBeTrue();
            _mockContext.Verify(c => c.GameEvents.Add(It.Is<GameEvent>(e => e.GameId == secondActiveGame.Id)), Times.Once); // First in list
        }

        #endregion

        #region Exception Scenarios

        [Fact]
        public async Task CaptureControlPointAsync_WhenControlPointsFindAsyncThrows_ShouldThrow()
        {
            // ARRANGE
            SetupDefaultSuccessMocks();
            _mockContext.Setup(c => c.ControlPoints.FindAsync(_testControlPoint.Id)).ThrowsAsync(new Exception("DB error"));

            // ACT & ASSERT
            await Should.ThrowAsync<Exception>(() => _service.CaptureControlPointAsync(_testControlPoint.Id, _testUser.Id, _correctNumpadCode));
        }

        [Fact]
        public async Task CaptureControlPointAsync_WhenUserManagerFindByIdAsyncThrows_ShouldThrow()
        {
            // ARRANGE
            SetupDefaultSuccessMocks();
            _mockUserManager.Setup(um => um.FindByIdAsync(_testUser.Id)).ThrowsAsync(new Exception("User manager error"));

            // ACT & ASSERT
            await Should.ThrowAsync<Exception>(() => _service.CaptureControlPointAsync(_testControlPoint.Id, _testUser.Id, _correctNumpadCode));
        }

        [Fact]
        public async Task CaptureControlPointAsync_WhenGameEventsAddThrows_ShouldThrow()
        {
            // ARRANGE
            SetupDefaultSuccessMocks();
            _mockContext.Setup(c => c.GameEvents.Add(It.IsAny<GameEvent>())).Throws(new Exception("Add error"));

            // ACT & ASSERT
            await Should.ThrowAsync<Exception>(() => _service.CaptureControlPointAsync(_testControlPoint.Id, _testUser.Id, _correctNumpadCode));
        }

        [Fact]
        public async Task CaptureControlPointAsync_WhenSaveChangesAsyncThrows_ShouldThrow()
        {
            // ARRANGE
            SetupDefaultSuccessMocks();
            _mockContext.Setup(c => c.SaveChangesAsync(default)).ThrowsAsync(new Exception("Save error"));

            // ACT & ASSERT
            await Should.ThrowAsync<Exception>(() => _service.CaptureControlPointAsync(_testControlPoint.Id, _testUser.Id, _correctNumpadCode));
        }

        [Fact]
        public async Task CaptureControlPointAsync_WithNumpadCodeCaseMismatch_ShouldReturnFailure()
        {
            // ARRANGE
            SetupDefaultSuccessMocks();

            // ACT
            var (success, message) = await _service.CaptureControlPointAsync(_testControlPoint.Id, _testUser.Id, _correctNumpadCode.ToUpper());

            // ASSERT
            success.ShouldBeFalse();
            message.ShouldBe("Invalid user or code.");
        }

        #endregion
    }
}
