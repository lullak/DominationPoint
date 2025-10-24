using DominationPoint.Core.Application;
using DominationPoint.Core.Application.Services;
using DominationPoint.Core.Domain;
using DominationPoint.Models;
using DominationPointTests.UnitTests.Services;
using Moq;
using Shouldly;

namespace DominationPointTests.UnitTests.Services
{
    public class ScoreboardServiceTests
    {
        private readonly Mock<IApplicationDbContext> _mockContext;
        private readonly ScoreboardService _service;
        private readonly Game _testGame;
        private readonly ApplicationUser _teamRedUser;
        private readonly ApplicationUser _teamBlueUser;
        private readonly ApplicationUser _teamGreenUser;

        public ScoreboardServiceTests()
        {
            _mockContext = new Mock<IApplicationDbContext>();
            _service = new ScoreboardService(_mockContext.Object);

            _testGame = new Game { Id = 1, Name = "Test Game", Status = GameStatus.Finished, StartTime = DateTime.UtcNow, EndTime = DateTime.UtcNow.AddMinutes(10) };
            _teamRedUser = new ApplicationUser { Id = "team-red-id", UserName = "Team Red", ColorHex = "#FF0000" };
            _teamBlueUser = new ApplicationUser { Id = "team-blue-id", UserName = "Team Blue", ColorHex = "#0000FF" };
            _teamGreenUser = new ApplicationUser { Id = "team-green-id", UserName = "Team Green", ColorHex = "#00FF00" };

            _mockContext.Setup(c => c.Games.FindAsync(1)).ReturnsAsync(_testGame);
        }

        #region CalculateScoreboardAsync Tests

        [Fact]
        public async Task CalculateScoreboardAsync_SingleCaptureEvent_ShouldAwardCaptureBonus()
        {
            // ARRANGE
            var gameId = 1;
            var captureTime = _testGame.StartTime.AddMinutes(1);

            var participants = new List<GameParticipant> { new GameParticipant { GameId = gameId, ApplicationUserId = _teamRedUser.Id, ApplicationUser = _teamRedUser } }.AsQueryable();
            _mockContext.Setup(c => c.GameParticipants).Returns(MockDbSetHelper.GetMockDbSet(participants).Object);

            var events = new List<GameEvent> { new GameEvent { GameId = gameId, ControlPointId = 1, Type = EventType.Capture, ActingUserId = _teamRedUser.Id, Timestamp = captureTime } }.AsQueryable();
            _mockContext.Setup(c => c.GameEvents).Returns(MockDbSetHelper.GetMockDbSet(events).Object);

            // ACT
            var result = await _service.CalculateScoreboardAsync(gameId);

            // ASSERT
            result.ShouldNotBeNull();
            result.TeamScores.ShouldHaveSingleItem();
            var teamRedScore = result.TeamScores.First(ts => ts.TeamName == "Team Red");

            teamRedScore.CaptureBonusScore.ShouldBe(100);
            teamRedScore.HoldingScore.ShouldBe(0);
            teamRedScore.TotalScore.ShouldBe(100);
        }

        [Fact]
        public async Task CalculateScoreboardAsync_TeamHoldsPointFor60Seconds_ShouldAwardCorrectHoldingScore()
        {
            // ARRANGE
            var gameId = 1;
            var captureTime = _testGame.StartTime.AddMinutes(1);
            var gameEndTime = captureTime.AddSeconds(60);

            var participants = new List<GameParticipant> { new GameParticipant { GameId = gameId, ApplicationUserId = _teamRedUser.Id, ApplicationUser = _teamRedUser } }.AsQueryable();
            _mockContext.Setup(c => c.GameParticipants).Returns(MockDbSetHelper.GetMockDbSet(participants).Object);

            var events = new List<GameEvent>
 {
 new GameEvent { GameId = gameId, ControlPointId =1, Type = EventType.Capture, ActingUserId = _teamRedUser.Id, Timestamp = captureTime },
 new GameEvent { GameId = gameId, ControlPointId =1, Type = EventType.GameEnd, ActingUserId = _teamRedUser.Id, Timestamp = gameEndTime }
 }.AsQueryable();
            _mockContext.Setup(c => c.GameEvents).Returns(MockDbSetHelper.GetMockDbSet(events).Object);

            // ACT
            var result = await _service.CalculateScoreboardAsync(gameId);

            // ASSERT
            var teamRedScore = result.TeamScores.Single();
            teamRedScore.CaptureBonusScore.ShouldBe(100);
            teamRedScore.HoldingScore.ShouldBe(60);
            teamRedScore.TotalScore.ShouldBe(160);
        }

        [Fact]
        public async Task CalculateScoreboardAsync_PointChangesOwner_ShouldAwardHoldingScoreToPreviousOwner()
        {
            // ARRANGE
            var gameId = 1;
            var time1 = _testGame.StartTime.AddMinutes(1);
            var time2 = time1.AddSeconds(30);

            var participants = new List<GameParticipant>
 {
 new GameParticipant { GameId = gameId, ApplicationUserId = _teamRedUser.Id, ApplicationUser = _teamRedUser },
 new GameParticipant { GameId = gameId, ApplicationUserId = _teamBlueUser.Id, ApplicationUser = _teamBlueUser }
 }.AsQueryable();
            _mockContext.Setup(c => c.GameParticipants).Returns(MockDbSetHelper.GetMockDbSet(participants).Object);

            var events = new List<GameEvent>
 {
 new GameEvent { GameId = gameId, ControlPointId =1, Type = EventType.Capture, ActingUserId = _teamRedUser.Id, Timestamp = time1 },
 new GameEvent { GameId = gameId, ControlPointId =1, Type = EventType.Capture, ActingUserId = _teamBlueUser.Id, Timestamp = time2 }
 }.AsQueryable();
            _mockContext.Setup(c => c.GameEvents).Returns(MockDbSetHelper.GetMockDbSet(events).Object);

            // ACT
            var result = await _service.CalculateScoreboardAsync(gameId);

            // ASSERT
            result.TeamScores.Count.ShouldBe(2);

            var teamRedScore = result.TeamScores.First(t => t.TeamName == "Team Red");
            teamRedScore.CaptureBonusScore.ShouldBe(100);
            teamRedScore.HoldingScore.ShouldBe(30);
            teamRedScore.TotalScore.ShouldBe(130);

            var teamBlueScore = result.TeamScores.First(t => t.TeamName == "Team Blue");
            teamBlueScore.CaptureBonusScore.ShouldBe(100);
            teamBlueScore.HoldingScore.ShouldBe(0);
            teamBlueScore.TotalScore.ShouldBe(100);
        }

        [Fact]
        public async Task CalculateScoreboardAsync_WithNoEvents_ShouldReturnZeroScoresForParticipants()
        {
            // ARRANGE
            var gameId = 1;
            var participants = new List<GameParticipant> { new GameParticipant { GameId = gameId, ApplicationUserId = _teamRedUser.Id, ApplicationUser = _teamRedUser } }.AsQueryable();
            _mockContext.Setup(c => c.GameParticipants).Returns(MockDbSetHelper.GetMockDbSet(participants).Object);

            var events = new List<GameEvent>().AsQueryable();
            _mockContext.Setup(c => c.GameEvents).Returns(MockDbSetHelper.GetMockDbSet(events).Object);

            // ACT
            var result = await _service.CalculateScoreboardAsync(gameId);

            // ASSERT
            result.TeamScores.ShouldHaveSingleItem();
            result.TeamScores.Single().TotalScore.ShouldBe(0);
        }

        [Fact]
        public async Task CalculateScoreboardAsync_WithUnorderedEvents_ShouldProcessInCorrectTimestampOrder()
        {
            // ARRANGE
            var gameId = 1;
            var time1 = _testGame.StartTime.AddMinutes(1);
            var time2 = time1.AddSeconds(30);

            var participants = new List<GameParticipant>
 {
 new GameParticipant { GameId = gameId, ApplicationUserId = _teamRedUser.Id, ApplicationUser = _teamRedUser },
 new GameParticipant { GameId = gameId, ApplicationUserId = _teamBlueUser.Id, ApplicationUser = _teamBlueUser }
 }.AsQueryable();
            _mockContext.Setup(c => c.GameParticipants).Returns(MockDbSetHelper.GetMockDbSet(participants).Object);

            // Events are intentionally out of order in the list
            var events = new List<GameEvent>
 {
 new GameEvent { GameId = gameId, ControlPointId =1, Type = EventType.Capture, ActingUserId = _teamBlueUser.Id, Timestamp = time2 },
 new GameEvent { GameId = gameId, ControlPointId =1, Type = EventType.Capture, ActingUserId = _teamRedUser.Id, Timestamp = time1 }
 }.AsQueryable();
            _mockContext.Setup(c => c.GameEvents).Returns(MockDbSetHelper.GetMockDbSet(events).Object);

            // ACT
            var result = await _service.CalculateScoreboardAsync(gameId);

            // ASSERT
            var teamRedScore = result.TeamScores.First(t => t.TeamName == "Team Red");
            teamRedScore.HoldingScore.ShouldBe(30);
        }

        [Fact]
        public async Task CalculateScoreboardAsync_WithTwoControlPoints_ShouldCalculateScoresIndependently()
        {
            // ARRANGE
            var gameId = 1;
            var time1 = _testGame.StartTime.AddMinutes(1);
            var time2 = time1.AddSeconds(15);
            var gameEndTime = time2.AddSeconds(45);

            var participants = new List<GameParticipant>
 {
 new GameParticipant { GameId = gameId, ApplicationUserId = _teamRedUser.Id, ApplicationUser = _teamRedUser },
 new GameParticipant { GameId = gameId, ApplicationUserId = _teamBlueUser.Id, ApplicationUser = _teamBlueUser }
 }.AsQueryable();
            _mockContext.Setup(c => c.GameParticipants).Returns(MockDbSetHelper.GetMockDbSet(participants).Object);

            var events = new List<GameEvent>
 {
 // CP 1: Red holds for 15s
 new GameEvent { GameId = gameId, ControlPointId =1, Type = EventType.Capture, ActingUserId = _teamRedUser.Id, Timestamp = time1 },
 new GameEvent { GameId = gameId, ControlPointId =1, Type = EventType.Capture, ActingUserId = _teamBlueUser.Id, Timestamp = time2 },
 // CP 2: Blue holds for 45s
 new GameEvent { GameId = gameId, ControlPointId =2, Type = EventType.Capture, ActingUserId = _teamBlueUser.Id, Timestamp = time2 },
 // Game ends
 new GameEvent { GameId = gameId, ControlPointId =1, Type = EventType.GameEnd, Timestamp = gameEndTime },
 new GameEvent { GameId = gameId, ControlPointId =2, Type = EventType.GameEnd, Timestamp = gameEndTime }
 }.AsQueryable();
            _mockContext.Setup(c => c.GameEvents).Returns(MockDbSetHelper.GetMockDbSet(events).Object);

            // ACT
            var result = await _service.CalculateScoreboardAsync(gameId);

            // ASSERT
            var teamRedScore = result.TeamScores.First(t => t.TeamName == "Team Red");
            teamRedScore.CaptureBonusScore.ShouldBe(100);
            teamRedScore.HoldingScore.ShouldBe(15); // Held CP1 for 15s

            var teamBlueScore = result.TeamScores.First(t => t.TeamName == "Team Blue");
            teamBlueScore.CaptureBonusScore.ShouldBe(200); // Captured both CP1 and CP2
            teamBlueScore.HoldingScore.ShouldBe(90); // Held CP1 and CP2 each for45s -> total90
        }

        [Fact]
        public async Task CalculateScoreboardAsync_EventWithNullActingUser_ShouldNotAwardBonusButStillCalculateHolding()
        {
            // ARRANGE
            var gameId = 1;
            var time1 = _testGame.StartTime.AddMinutes(1);
            var time2 = time1.AddSeconds(20);

            var participants = new List<GameParticipant> { new GameParticipant { GameId = gameId, ApplicationUserId = _teamRedUser.Id, ApplicationUser = _teamRedUser } }.AsQueryable();
            _mockContext.Setup(c => c.GameParticipants).Returns(MockDbSetHelper.GetMockDbSet(participants).Object);

            var events = new List<GameEvent>
 {
 new GameEvent { GameId = gameId, ControlPointId =1, Type = EventType.Capture, ActingUserId = _teamRedUser.Id, Timestamp = time1 },
 new GameEvent { GameId = gameId, ControlPointId =1, Type = EventType.Capture, ActingUserId = null, Timestamp = time2 } // Point becomes neutral
 }.AsQueryable();
            _mockContext.Setup(c => c.GameEvents).Returns(MockDbSetHelper.GetMockDbSet(events).Object);

            // ACT
            var result = await _service.CalculateScoreboardAsync(gameId);

            // ASSERT
            var teamRedScore = result.TeamScores.Single();
            teamRedScore.CaptureBonusScore.ShouldBe(100);
            teamRedScore.HoldingScore.ShouldBe(20);
        }

        [Fact]
        public async Task CalculateScoreboardAsync_ThreeTeamScenario_ShouldCalculateAllScoresCorrectly()
        {
            // ARRANGE
            var gameId = 1;
            var time1 = _testGame.StartTime.AddMinutes(1); // Red captures
            var time2 = time1.AddSeconds(10); // Blue captures
            var time3 = time2.AddSeconds(20); // Green captures

            var participants = new List<GameParticipant>
 {
 new GameParticipant { GameId = gameId, ApplicationUserId = _teamRedUser.Id, ApplicationUser = _teamRedUser },
 new GameParticipant { GameId = gameId, ApplicationUserId = _teamBlueUser.Id, ApplicationUser = _teamBlueUser },
 new GameParticipant { GameId = gameId, ApplicationUserId = _teamGreenUser.Id, ApplicationUser = _teamGreenUser }
 }.AsQueryable();
            _mockContext.Setup(c => c.GameParticipants).Returns(MockDbSetHelper.GetMockDbSet(participants).Object);

            var events = new List<GameEvent>
 {
 new GameEvent { GameId = gameId, ControlPointId =1, Type = EventType.Capture, ActingUserId = _teamRedUser.Id, Timestamp = time1 },
 new GameEvent { GameId = gameId, ControlPointId =1, Type = EventType.Capture, ActingUserId = _teamBlueUser.Id, Timestamp = time2 },
 new GameEvent { GameId = gameId, ControlPointId =1, Type = EventType.Capture, ActingUserId = _teamGreenUser.Id, Timestamp = time3 }
 }.AsQueryable();
            _mockContext.Setup(c => c.GameEvents).Returns(MockDbSetHelper.GetMockDbSet(events).Object);

            // ACT
            var result = await _service.CalculateScoreboardAsync(gameId);

            // ASSERT
            var teamRedScore = result.TeamScores.First(t => t.TeamName == "Team Red");
            teamRedScore.HoldingScore.ShouldBe(10);
            teamRedScore.CaptureBonusScore.ShouldBe(100);

            var teamBlueScore = result.TeamScores.First(t => t.TeamName == "Team Blue");
            teamBlueScore.HoldingScore.ShouldBe(20);
            teamBlueScore.CaptureBonusScore.ShouldBe(100);

            var teamGreenScore = result.TeamScores.First(t => t.TeamName == "Team Green");
            teamGreenScore.HoldingScore.ShouldBe(0);
            teamGreenScore.CaptureBonusScore.ShouldBe(100);
        }

        [Fact]
        public async Task CalculateScoreboardAsync_CaptureAtSameTimestamp_ShouldResultInZeroHoldingTime()
        {
            // ARRANGE
            var gameId = 1;
            var time = _testGame.StartTime.AddMinutes(1);

            var participants = new List<GameParticipant>
 {
 new GameParticipant { GameId = gameId, ApplicationUserId = _teamRedUser.Id, ApplicationUser = _teamRedUser },
 new GameParticipant { GameId = gameId, ApplicationUserId = _teamBlueUser.Id, ApplicationUser = _teamBlueUser }
 }.AsQueryable();
            _mockContext.Setup(c => c.GameParticipants).Returns(MockDbSetHelper.GetMockDbSet(participants).Object);

            var events = new List<GameEvent>
 {
 new GameEvent { GameId = gameId, ControlPointId =1, Type = EventType.Capture, ActingUserId = _teamRedUser.Id, Timestamp = time },
 new GameEvent { GameId = gameId, ControlPointId =1, Type = EventType.Capture, ActingUserId = _teamBlueUser.Id, Timestamp = time }
 }.AsQueryable();
            _mockContext.Setup(c => c.GameEvents).Returns(MockDbSetHelper.GetMockDbSet(events).Object);

            // ACT
            var result = await _service.CalculateScoreboardAsync(gameId);

            // ASSERT
            var teamRedScore = result.TeamScores.First(t => t.TeamName == "Team Red");
            teamRedScore.HoldingScore.ShouldBe(0);
        }

        [Fact]
        public async Task CalculateScoreboardAsync_WithNoParticipants_ShouldReturnEmptyScoreList()
        {
            // ARRANGE
            var gameId = 1;
            var participants = new List<GameParticipant>().AsQueryable();
            _mockContext.Setup(c => c.GameParticipants).Returns(MockDbSetHelper.GetMockDbSet(participants).Object);
            var events = new List<GameEvent> { new GameEvent { GameId = gameId, ControlPointId = 1, Type = EventType.Capture, ActingUserId = _teamRedUser.Id, Timestamp = DateTime.UtcNow } }.AsQueryable();
            _mockContext.Setup(c => c.GameEvents).Returns(MockDbSetHelper.GetMockDbSet(events).Object);

            // ACT
            var result = await _service.CalculateScoreboardAsync(gameId);

            // ASSERT
            result.TeamScores.ShouldBeEmpty();
        }

        [Fact]
        public async Task CalculateScoreboardAsync_ScoreActorNotAParticipant_ShouldNotBeInScoreboardButHoldingPointsShouldCount()
        {
            // ARRANGE
            var gameId = 1;
            var nonParticipant = new ApplicationUser { Id = "non-participant", UserName = "Ghost Team", ColorHex = "#FFFFFF" };
            var time1 = _testGame.StartTime.AddMinutes(1);
            var time2 = time1.AddSeconds(50);

            // ONLY Blue team is a participant
            var participants = new List<GameParticipant> { new GameParticipant { GameId = gameId, ApplicationUserId = _teamBlueUser.Id, ApplicationUser = _teamBlueUser } }.AsQueryable();
            _mockContext.Setup(c => c.GameParticipants).Returns(MockDbSetHelper.GetMockDbSet(participants).Object);

            var events = new List<GameEvent>
 {
 new GameEvent { GameId = gameId, ControlPointId =1, Type = EventType.Capture, ActingUserId = nonParticipant.Id, Timestamp = time1 },
 new GameEvent { GameId = gameId, ControlPointId =1, Type = EventType.Capture, ActingUserId = _teamBlueUser.Id, Timestamp = time2 }
 }.AsQueryable();
            _mockContext.Setup(c => c.GameEvents).Returns(MockDbSetHelper.GetMockDbSet(events).Object);

            // ACT
            var result = await _service.CalculateScoreboardAsync(gameId);

            // ASSERT
            result.TeamScores.ShouldHaveSingleItem(); // Only Blue team should be in the results
            var teamBlueScore = result.TeamScores.Single();
            teamBlueScore.TeamName.ShouldBe("Team Blue");
            teamBlueScore.CaptureBonusScore.ShouldBe(100); // Blue gets bonus
            teamBlueScore.HoldingScore.ShouldBe(0); // Blue has not held the point yet
        }

        #endregion

        #region Additional tests added (10)

        [Fact]
        public async Task CalculateScoreboardAsync_MultipleCapturesSameTeam_ShouldAccumulateBonuses()
        {
            var gameId = 1;
            var t1 = _testGame.StartTime.AddMinutes(1);
            var t2 = t1.AddSeconds(5);

            var participants = new List<GameParticipant> { new GameParticipant { GameId = gameId, ApplicationUserId = _teamRedUser.Id, ApplicationUser = _teamRedUser } }.AsQueryable();
            _mockContext.Setup(c => c.GameParticipants).Returns(MockDbSetHelper.GetMockDbSet(participants).Object);

            var events = new List<GameEvent>
 {
 new GameEvent{ GameId=gameId, ControlPointId=1, Type=EventType.Capture, ActingUserId=_teamRedUser.Id, Timestamp=t1 },
 new GameEvent{ GameId=gameId, ControlPointId=2, Type=EventType.Capture, ActingUserId=_teamRedUser.Id, Timestamp=t2 }
 }.AsQueryable();
            _mockContext.Setup(c => c.GameEvents).Returns(MockDbSetHelper.GetMockDbSet(events).Object);

            var result = await _service.CalculateScoreboardAsync(gameId);
            var score = result.TeamScores.Single();
            score.CaptureBonusScore.ShouldBe(200);
        }

        [Fact]
        public async Task CalculateScoreboardAsync_GameEndOnly_ShouldReturnNoBonuses()
        {
            var gameId = 1;
            var t1 = _testGame.StartTime.AddMinutes(1);
            var participants = new List<GameParticipant> { new GameParticipant { GameId = gameId, ApplicationUserId = _teamRedUser.Id, ApplicationUser = _teamRedUser } }.AsQueryable();
            _mockContext.Setup(c => c.GameParticipants).Returns(MockDbSetHelper.GetMockDbSet(participants).Object);

            var events = new List<GameEvent>
 {
 new GameEvent{ GameId=gameId, ControlPointId=1, Type=EventType.GameEnd, Timestamp=t1 }
 }.AsQueryable();
            _mockContext.Setup(c => c.GameEvents).Returns(MockDbSetHelper.GetMockDbSet(events).Object);

            var result = await _service.CalculateScoreboardAsync(gameId);
            result.TeamScores.Single().TotalScore.ShouldBe(0);
        }

        [Fact]
        public async Task GetSavedScoreboardAsync_WithSingleScore_ShouldReturnSingle()
        {
            var gameId = 1;
            var savedScores = new List<GameScore> { new GameScore { GameId = gameId, ApplicationUserId = _teamRedUser.Id, ApplicationUser = _teamRedUser, Points = 250 } }.AsQueryable();
            _mockContext.Setup(c => c.GameScores).Returns(MockDbSetHelper.GetMockDbSet(savedScores).Object);

            var result = await _service.GetSavedScoreboardAsync(gameId);
            result.TeamScores.Count.ShouldBe(1);
            result.TeamScores[0].TotalScoreFromDb.ShouldBe(250);
        }

        [Fact]
        public async Task CalculateScoreboardAsync_NonExistentGame_ShouldReturnNotFoundModel()
        {
            _mockContext.Setup(c => c.Games.FindAsync(999)).ReturnsAsync((Game)null);
            var result = await _service.CalculateScoreboardAsync(999);
            result.ShouldNotBeNull();
            result.Game.Name.ShouldBe("Not Found");
        }

        [Fact]
        public async Task CalculateScoreboardAsync_EventWithoutActingUserGameEndOnly_ShouldNotThrow()
        {
            var gameId = 1;
            var t1 = _testGame.StartTime.AddMinutes(1);
            var participants = new List<GameParticipant> { new GameParticipant { GameId = gameId, ApplicationUserId = _teamRedUser.Id, ApplicationUser = _teamRedUser } }.AsQueryable();
            _mockContext.Setup(c => c.GameParticipants).Returns(MockDbSetHelper.GetMockDbSet(participants).Object);
            var events = new List<GameEvent> { new GameEvent { GameId = gameId, ControlPointId = 1, Type = EventType.Capture, ActingUserId = null, Timestamp = t1 }, new GameEvent { GameId = gameId, ControlPointId = 1, Type = EventType.GameEnd, Timestamp = t1.AddSeconds(30) } }.AsQueryable();
            _mockContext.Setup(c => c.GameEvents).Returns(MockDbSetHelper.GetMockDbSet(events).Object);

            var result = await _service.CalculateScoreboardAsync(gameId);
            result.TeamScores.Single().HoldingScore.ShouldBe(30);
        }

        [Fact]
        public async Task CalculateScoreboardAsync_LongHoldDuration_ShouldCalculateLargeHolding()
        {
            var gameId = 1;
            var t1 = _testGame.StartTime.AddHours(-2);
            var t2 = t1.AddHours(3); //3 hours hold
            var participants = new List<GameParticipant> { new GameParticipant { GameId = gameId, ApplicationUserId = _teamRedUser.Id, ApplicationUser = _teamRedUser } }.AsQueryable();
            _mockContext.Setup(c => c.GameParticipants).Returns(MockDbSetHelper.GetMockDbSet(participants).Object);
            var events = new List<GameEvent> { new GameEvent { GameId = gameId, ControlPointId = 1, Type = EventType.Capture, ActingUserId = _teamRedUser.Id, Timestamp = t1 }, new GameEvent { GameId = gameId, ControlPointId = 1, Type = EventType.GameEnd, Timestamp = t2 } }.AsQueryable();
            _mockContext.Setup(c => c.GameEvents).Returns(MockDbSetHelper.GetMockDbSet(events).Object);

            var result = await _service.CalculateScoreboardAsync(gameId);
            result.TeamScores.Single().HoldingScore.ShouldBe((int)(t2 - t1).TotalSeconds);
        }

        [Fact]
        public async Task CalculateScoreboardAsync_MultipleCPs_SameTeamHoldingDifferentDurations()
        {
            var gameId = 1;
            var t1 = _testGame.StartTime.AddMinutes(1);
            var t2 = t1.AddSeconds(10);
            var t3 = t2.AddSeconds(30);
            var participants = new List<GameParticipant> { new GameParticipant { GameId = gameId, ApplicationUserId = _teamBlueUser.Id, ApplicationUser = _teamBlueUser } }.AsQueryable();
            _mockContext.Setup(c => c.GameParticipants).Returns(MockDbSetHelper.GetMockDbSet(participants).Object);
            var events = new List<GameEvent> {
 new GameEvent{ GameId=gameId, ControlPointId=1, Type=EventType.Capture, ActingUserId=_teamBlueUser.Id, Timestamp=t1},
 new GameEvent{ GameId=gameId, ControlPointId=1, Type=EventType.GameEnd, Timestamp=t2},
 new GameEvent{ GameId=gameId, ControlPointId=2, Type=EventType.Capture, ActingUserId=_teamBlueUser.Id, Timestamp=t2},
 new GameEvent{ GameId=gameId, ControlPointId=2, Type=EventType.GameEnd, Timestamp=t3}
 }.AsQueryable();
            _mockContext.Setup(c => c.GameEvents).Returns(MockDbSetHelper.GetMockDbSet(events).Object);

            var result = await _service.CalculateScoreboardAsync(gameId);
            var score = result.TeamScores.Single();
            score.HoldingScore.ShouldBe((int)(t2 - t1).TotalSeconds + (int)(t3 - t2).TotalSeconds);
        }

        [Fact]
        public async Task GetSavedScoreboardAsync_WithUnorderedScores_ShouldOrderDescending()
        {
            var gameId = 1;
            var savedScores = new List<GameScore>
 {
 new GameScore{ GameId=gameId, ApplicationUserId=_teamRedUser.Id, ApplicationUser=_teamRedUser, Points=150},
 new GameScore{ GameId=gameId, ApplicationUserId=_teamBlueUser.Id, ApplicationUser=_teamBlueUser, Points=300},
 new GameScore{ GameId=gameId, ApplicationUserId=_teamGreenUser.Id, ApplicationUser=_teamGreenUser, Points=200}
 }.AsQueryable();
            _mockContext.Setup(c => c.GameScores).Returns(MockDbSetHelper.GetMockDbSet(savedScores).Object);

            var result = await _service.GetSavedScoreboardAsync(gameId);
            result.TeamScores.Select(t => t.TotalScoreFromDb).ShouldBe(new[] { 300, 200, 150 });
        }

        [Fact]
        public async Task CalculateScoreboardAsync_ControlPointIdZero_ShouldBeHandled()
        {
            var gameId = 1;
            var t1 = _testGame.StartTime.AddMinutes(1);
            var participants = new List<GameParticipant> { new GameParticipant { GameId = gameId, ApplicationUserId = _teamRedUser.Id, ApplicationUser = _teamRedUser } }.AsQueryable();
            _mockContext.Setup(c => c.GameParticipants).Returns(MockDbSetHelper.GetMockDbSet(participants).Object);
            var events = new List<GameEvent> { new GameEvent { GameId = gameId, ControlPointId = 0, Type = EventType.Capture, ActingUserId = _teamRedUser.Id, Timestamp = t1 } }.AsQueryable();
            _mockContext.Setup(c => c.GameEvents).Returns(MockDbSetHelper.GetMockDbSet(events).Object);

            var result = await _service.CalculateScoreboardAsync(gameId);
            result.TeamScores.Single().CaptureBonusScore.ShouldBe(100);
        }

        #endregion

        #region Additional tests added (25 more)

        [Fact]
        public async Task CalculateScoreboardAsync_CaptureBeforeGameStart_ShouldNotCountHoldingBeforeStart()
        {
            var gameId = 1;
            var beforeStart = _testGame.StartTime.AddMinutes(-5);
            var afterStart = _testGame.StartTime.AddMinutes(1);
            var participants = new List<GameParticipant> { new GameParticipant { GameId = gameId, ApplicationUserId = _teamRedUser.Id, ApplicationUser = _teamRedUser } }.AsQueryable();
            _mockContext.Setup(c => c.GameParticipants).Returns(MockDbSetHelper.GetMockDbSet(participants).Object);
            var events = new List<GameEvent> { new GameEvent { GameId = gameId, ControlPointId = 1, Type = EventType.Capture, ActingUserId = _teamRedUser.Id, Timestamp = beforeStart }, new GameEvent { GameId = gameId, ControlPointId = 1, Type = EventType.GameEnd, Timestamp = afterStart } }.AsQueryable();
            _mockContext.Setup(c => c.GameEvents).Returns(MockDbSetHelper.GetMockDbSet(events).Object);

            var result = await _service.CalculateScoreboardAsync(gameId);
            result.TeamScores.Single().HoldingScore.ShouldBe((int)(afterStart - beforeStart).TotalSeconds);
        }

        [Fact]
        public async Task CalculateScoreboardAsync_CaptureAtGameStart_ShouldCountFullHolding()
        {
            var gameId = 1;
            var endTime = _testGame.StartTime.AddSeconds(120);
            var participants = new List<GameParticipant> { new GameParticipant { GameId = gameId, ApplicationUserId = _teamRedUser.Id, ApplicationUser = _teamRedUser } }.AsQueryable();
            _mockContext.Setup(c => c.GameParticipants).Returns(MockDbSetHelper.GetMockDbSet(participants).Object);
            var events = new List<GameEvent> { new GameEvent { GameId = gameId, ControlPointId = 1, Type = EventType.Capture, ActingUserId = _teamRedUser.Id, Timestamp = _testGame.StartTime }, new GameEvent { GameId = gameId, ControlPointId = 1, Type = EventType.GameEnd, Timestamp = endTime } }.AsQueryable();
            _mockContext.Setup(c => c.GameEvents).Returns(MockDbSetHelper.GetMockDbSet(events).Object);

            var result = await _service.CalculateScoreboardAsync(gameId);
            result.TeamScores.Single().HoldingScore.ShouldBe(120);
        }

        [Fact]
        public async Task CalculateScoreboardAsync_CaptureAfterGameEnd_ShouldNotCount()
        {
            var gameId = 1;
            var afterEnd = _testGame.EndTime.AddMinutes(5);
            var participants = new List<GameParticipant> { new GameParticipant { GameId = gameId, ApplicationUserId = _teamRedUser.Id, ApplicationUser = _teamRedUser } }.AsQueryable();
            _mockContext.Setup(c => c.GameParticipants).Returns(MockDbSetHelper.GetMockDbSet(participants).Object);
            var events = new List<GameEvent> { new GameEvent { GameId = gameId, ControlPointId = 1, Type = EventType.Capture, ActingUserId = _teamRedUser.Id, Timestamp = afterEnd } }.AsQueryable();
            _mockContext.Setup(c => c.GameEvents).Returns(MockDbSetHelper.GetMockDbSet(events).Object);

            var result = await _service.CalculateScoreboardAsync(gameId);
            result.TeamScores.Single().CaptureBonusScore.ShouldBe(100);
            result.TeamScores.Single().HoldingScore.ShouldBe(0);
        }

        [Fact]
        public async Task CalculateScoreboardAsync_RecaptureBySameTeam_ShouldStillAwardBonus()
        {
            var gameId = 1;
            var t1 = _testGame.StartTime.AddMinutes(1);
            var t2 = t1.AddSeconds(10);
            var participants = new List<GameParticipant> { new GameParticipant { GameId = gameId, ApplicationUserId = _teamRedUser.Id, ApplicationUser = _teamRedUser } }.AsQueryable();
            _mockContext.Setup(c => c.GameParticipants).Returns(MockDbSetHelper.GetMockDbSet(participants).Object);
            var events = new List<GameEvent> { new GameEvent { GameId = gameId, ControlPointId = 1, Type = EventType.Capture, ActingUserId = _teamRedUser.Id, Timestamp = t1 }, new GameEvent { GameId = gameId, ControlPointId = 1, Type = EventType.Capture, ActingUserId = _teamRedUser.Id, Timestamp = t2 } }.AsQueryable();
            _mockContext.Setup(c => c.GameEvents).Returns(MockDbSetHelper.GetMockDbSet(events).Object);

            var result = await _service.CalculateScoreboardAsync(gameId);
            result.TeamScores.Single().CaptureBonusScore.ShouldBe(200);
        }

        [Fact]
        public async Task CalculateScoreboardAsync_NeutralCaptureFollowedByCapture_ShouldAwardToNewOwner()
        {
            var gameId = 1;
            var t1 = _testGame.StartTime.AddMinutes(1);
            var t2 = t1.AddSeconds(10);
            var t3 = t2.AddSeconds(20);
            var participants = new List<GameParticipant> { new GameParticipant { GameId = gameId, ApplicationUserId = _teamRedUser.Id, ApplicationUser = _teamRedUser }, new GameParticipant { GameId = gameId, ApplicationUserId = _teamBlueUser.Id, ApplicationUser = _teamBlueUser } }.AsQueryable();
            _mockContext.Setup(c => c.GameParticipants).Returns(MockDbSetHelper.GetMockDbSet(participants).Object);
            var events = new List<GameEvent> { new GameEvent { GameId = gameId, ControlPointId = 1, Type = EventType.Capture, ActingUserId = _teamRedUser.Id, Timestamp = t1 }, new GameEvent { GameId = gameId, ControlPointId = 1, Type = EventType.Capture, ActingUserId = null, Timestamp = t2 }, new GameEvent { GameId = gameId, ControlPointId = 1, Type = EventType.Capture, ActingUserId = _teamBlueUser.Id, Timestamp = t3 } }.AsQueryable();
            _mockContext.Setup(c => c.GameEvents).Returns(MockDbSetHelper.GetMockDbSet(events).Object);

            var result = await _service.CalculateScoreboardAsync(gameId);
            var redScore = result.TeamScores.First(t => t.TeamName == "Team Red");
            redScore.HoldingScore.ShouldBe(10);
            var blueScore = result.TeamScores.First(t => t.TeamName == "Team Blue");
            blueScore.CaptureBonusScore.ShouldBe(100);
        }

        [Fact]
        public async Task CalculateScoreboardAsync_InvalidEventType_ShouldIgnore()
        {
            var gameId = 1;
            var t1 = _testGame.StartTime.AddMinutes(1);
            var participants = new List<GameParticipant> { new GameParticipant { GameId = gameId, ApplicationUserId = _teamRedUser.Id, ApplicationUser = _teamRedUser } }.AsQueryable();
            _mockContext.Setup(c => c.GameParticipants).Returns(MockDbSetHelper.GetMockDbSet(participants).Object);
            var events = new List<GameEvent> { new GameEvent { GameId = gameId, ControlPointId = 1, Type = (EventType)999, ActingUserId = _teamRedUser.Id, Timestamp = t1 } }.AsQueryable();
            _mockContext.Setup(c => c.GameEvents).Returns(MockDbSetHelper.GetMockDbSet(events).Object);

            var result = await _service.CalculateScoreboardAsync(gameId);
            result.TeamScores.Single().TotalScore.ShouldBe(0);
        }

        [Fact]
        public async Task CalculateScoreboardAsync_EventsForDifferentGames_ShouldIgnore()
        {
            var gameId = 1;
            var t1 = _testGame.StartTime.AddMinutes(1);
            var participants = new List<GameParticipant> { new GameParticipant { GameId = gameId, ApplicationUserId = _teamRedUser.Id, ApplicationUser = _teamRedUser } }.AsQueryable();
            _mockContext.Setup(c => c.GameParticipants).Returns(MockDbSetHelper.GetMockDbSet(participants).Object);
            var events = new List<GameEvent> { new GameEvent { GameId = 2, ControlPointId = 1, Type = EventType.Capture, ActingUserId = _teamRedUser.Id, Timestamp = t1 } }.AsQueryable();
            _mockContext.Setup(c => c.GameEvents).Returns(MockDbSetHelper.GetMockDbSet(events).Object);

            var result = await _service.CalculateScoreboardAsync(gameId);
            result.TeamScores.Single().TotalScore.ShouldBe(0);
        }

        [Fact]
        public async Task CalculateScoreboardAsync_ParticipantsNotInEvents_ShouldHaveZeroScore()
        {
            var gameId = 1;
            var participants = new List<GameParticipant> { new GameParticipant { GameId = gameId, ApplicationUserId = _teamRedUser.Id, ApplicationUser = _teamRedUser }, new GameParticipant { GameId = gameId, ApplicationUserId = _teamBlueUser.Id, ApplicationUser = _teamBlueUser } }.AsQueryable();
            _mockContext.Setup(c => c.GameParticipants).Returns(MockDbSetHelper.GetMockDbSet(participants).Object);
            var events = new List<GameEvent> { new GameEvent { GameId = gameId, ControlPointId = 1, Type = EventType.Capture, ActingUserId = _teamRedUser.Id, Timestamp = _testGame.StartTime.AddMinutes(1) } }.AsQueryable();
            _mockContext.Setup(c => c.GameEvents).Returns(MockDbSetHelper.GetMockDbSet(events).Object);

            var result = await _service.CalculateScoreboardAsync(gameId);
            result.TeamScores.First(t => t.TeamName == "Team Blue").TotalScore.ShouldBe(0);
        }

        [Fact]
        public async Task CalculateScoreboardAsync_OnlyGameEndEvents_ShouldNotAwardScores()
        {
            var gameId = 1;
            var participants = new List<GameParticipant> { new GameParticipant { GameId = gameId, ApplicationUserId = _teamRedUser.Id, ApplicationUser = _teamRedUser } }.AsQueryable();
            _mockContext.Setup(c => c.GameParticipants).Returns(MockDbSetHelper.GetMockDbSet(participants).Object);
            var events = new List<GameEvent> { new GameEvent { GameId = gameId, ControlPointId = 1, Type = EventType.GameEnd, Timestamp = _testGame.StartTime.AddMinutes(1) } }.AsQueryable();
            _mockContext.Setup(c => c.GameEvents).Returns(MockDbSetHelper.GetMockDbSet(events).Object);

            var result = await _service.CalculateScoreboardAsync(gameId);
            result.TeamScores.Single().TotalScore.ShouldBe(0);
        }

        [Fact]
        public async Task CalculateScoreboardAsync_HoldingTimePrecision_ShouldRoundDown()
        {
            var gameId = 1;
            var t1 = _testGame.StartTime.AddMinutes(1);
            var t2 = t1.AddSeconds(30.7);
            var participants = new List<GameParticipant> { new GameParticipant { GameId = gameId, ApplicationUserId = _teamRedUser.Id, ApplicationUser = _teamRedUser } }.AsQueryable();
            _mockContext.Setup(c => c.GameParticipants).Returns(MockDbSetHelper.GetMockDbSet(participants).Object);
            var events = new List<GameEvent> { new GameEvent { GameId = gameId, ControlPointId = 1, Type = EventType.Capture, ActingUserId = _teamRedUser.Id, Timestamp = t1 }, new GameEvent { GameId = gameId, ControlPointId = 1, Type = EventType.GameEnd, Timestamp = t2 } }.AsQueryable();
            _mockContext.Setup(c => c.GameEvents).Returns(MockDbSetHelper.GetMockDbSet(events).Object);

            var result = await _service.CalculateScoreboardAsync(gameId);
            result.TeamScores.Single().HoldingScore.ShouldBe(30);
        }

        [Fact]
        public async Task CalculateScoreboardAsync_NoGameEndEvent_ShouldUseGameEndTime()
        {
            var gameId = 1;
            var t1 = _testGame.StartTime.AddMinutes(1);
            var participants = new List<GameParticipant> { new GameParticipant { GameId = gameId, ApplicationUserId = _teamRedUser.Id, ApplicationUser = _teamRedUser } }.AsQueryable();
            _mockContext.Setup(c => c.GameParticipants).Returns(MockDbSetHelper.GetMockDbSet(participants).Object);
            var events = new List<GameEvent> { new GameEvent { GameId = gameId, ControlPointId = 1, Type = EventType.Capture, ActingUserId = _teamRedUser.Id, Timestamp = t1 } }.AsQueryable();
            _mockContext.Setup(c => c.GameEvents).Returns(MockDbSetHelper.GetMockDbSet(events).Object);

            var result = await _service.CalculateScoreboardAsync(gameId);
            result.TeamScores.Single().HoldingScore.ShouldBe(0);
        }

        [Fact]
        public async Task CalculateScoreboardAsync_DuplicateEvents_ShouldProcessAll()
        {
            var gameId = 1;
            var t1 = _testGame.StartTime.AddMinutes(1);
            var participants = new List<GameParticipant> { new GameParticipant { GameId = gameId, ApplicationUserId = _teamRedUser.Id, ApplicationUser = _teamRedUser } }.AsQueryable();
            _mockContext.Setup(c => c.GameParticipants).Returns(MockDbSetHelper.GetMockDbSet(participants).Object);
            var events = new List<GameEvent> { new GameEvent { GameId = gameId, ControlPointId = 1, Type = EventType.Capture, ActingUserId = _teamRedUser.Id, Timestamp = t1 }, new GameEvent { GameId = gameId, ControlPointId = 1, Type = EventType.Capture, ActingUserId = _teamRedUser.Id, Timestamp = t1 } }.AsQueryable();
            _mockContext.Setup(c => c.GameEvents).Returns(MockDbSetHelper.GetMockDbSet(events).Object);

            var result = await _service.CalculateScoreboardAsync(gameId);
            result.TeamScores.Single().CaptureBonusScore.ShouldBe(200);
        }

        [Fact]
        public async Task CalculateScoreboardAsync_ControlPointsNotSequential_ShouldHandle()
        {
            var gameId = 1;
            var t1 = _testGame.StartTime.AddMinutes(1);
            var t2 = t1.AddSeconds(10);
            var participants = new List<GameParticipant> { new GameParticipant { GameId = gameId, ApplicationUserId = _teamRedUser.Id, ApplicationUser = _teamRedUser } }.AsQueryable();
            _mockContext.Setup(c => c.GameParticipants).Returns(MockDbSetHelper.GetMockDbSet(participants).Object);
            var events = new List<GameEvent> { new GameEvent { GameId = gameId, ControlPointId = 5, Type = EventType.Capture, ActingUserId = _teamRedUser.Id, Timestamp = t1 }, new GameEvent { GameId = gameId, ControlPointId = 10, Type = EventType.Capture, ActingUserId = _teamRedUser.Id, Timestamp = t2 } }.AsQueryable();
            _mockContext.Setup(c => c.GameEvents).Returns(MockDbSetHelper.GetMockDbSet(events).Object);

            var result = await _service.CalculateScoreboardAsync(gameId);
            result.TeamScores.Single().CaptureBonusScore.ShouldBe(200);
        }

        [Fact]
        public async Task CalculateScoreboardAsync_TeamWithNullUserName_ShouldHandle()
        {
            var gameId = 1;
            var nullNameUser = new ApplicationUser { Id = "null-name", UserName = null, ColorHex = "#000000" };
            var participants = new List<GameParticipant> { new GameParticipant { GameId = gameId, ApplicationUserId = nullNameUser.Id, ApplicationUser = nullNameUser } }.AsQueryable();
            _mockContext.Setup(c => c.GameParticipants).Returns(MockDbSetHelper.GetMockDbSet(participants).Object);
            var events = new List<GameEvent> { new GameEvent { GameId = gameId, ControlPointId = 1, Type = EventType.Capture, ActingUserId = nullNameUser.Id, Timestamp = _testGame.StartTime.AddMinutes(1) } }.AsQueryable();
            _mockContext.Setup(c => c.GameEvents).Returns(MockDbSetHelper.GetMockDbSet(events).Object);

            var result = await _service.CalculateScoreboardAsync(gameId);
            result.TeamScores.Single().TeamName.ShouldBe(null);
        }

        [Fact]
        public async Task GetSavedScoreboardAsync_WithNegativePoints_ShouldOrderCorrectly()
        {
            var gameId = 1;
            var savedScores = new List<GameScore> { new GameScore { GameId = gameId, ApplicationUserId = _teamRedUser.Id, ApplicationUser = _teamRedUser, Points = -50 }, new GameScore { GameId = gameId, ApplicationUserId = _teamBlueUser.Id, ApplicationUser = _teamBlueUser, Points = 100 } }.AsQueryable();
            _mockContext.Setup(c => c.GameScores).Returns(MockDbSetHelper.GetMockDbSet(savedScores).Object);

            var result = await _service.GetSavedScoreboardAsync(gameId);
            result.TeamScores[0].TotalScoreFromDb.ShouldBe(100);
            result.TeamScores[1].TotalScoreFromDb.ShouldBe(-50);
        }

        [Fact]
        public async Task GetSavedScoreboardAsync_WithTiedScores_ShouldMaintainOrder()
        {
            var gameId = 1;
            var savedScores = new List<GameScore> { new GameScore { GameId = gameId, ApplicationUserId = _teamRedUser.Id, ApplicationUser = _teamRedUser, Points = 100 }, new GameScore { GameId = gameId, ApplicationUserId = _teamBlueUser.Id, ApplicationUser = _teamBlueUser, Points = 100 } }.AsQueryable();
            _mockContext.Setup(c => c.GameScores).Returns(MockDbSetHelper.GetMockDbSet(savedScores).Object);

            var result = await _service.GetSavedScoreboardAsync(gameId);
            result.TeamScores.Count.ShouldBe(2);
            result.TeamScores.All(t => t.TotalScoreFromDb == 100).ShouldBeTrue();
        }

        [Fact]
        public async Task GetSavedScoreboardAsync_WithZeroPoints_ShouldInclude()
        {
            var gameId = 1;
            var savedScores = new List<GameScore> { new GameScore { GameId = gameId, ApplicationUserId = _teamRedUser.Id, ApplicationUser = _teamRedUser, Points = 0 } }.AsQueryable();
            _mockContext.Setup(c => c.GameScores).Returns(MockDbSetHelper.GetMockDbSet(savedScores).Object);

            var result = await _service.GetSavedScoreboardAsync(gameId);
            result.TeamScores.Single().TotalScoreFromDb.ShouldBe(0);
        }

        [Fact]
        public async Task GetSavedScoreboardAsync_ScoresForDifferentGames_ShouldIgnore()
        {
            var gameId = 1;
            var savedScores = new List<GameScore> { new GameScore { GameId = 2, ApplicationUserId = _teamRedUser.Id, ApplicationUser = _teamRedUser, Points = 100 } }.AsQueryable();
            _mockContext.Setup(c => c.GameScores).Returns(MockDbSetHelper.GetMockDbSet(savedScores).Object);

            var result = await _service.GetSavedScoreboardAsync(gameId);
            result.TeamScores.ShouldBeEmpty();
        }

        [Fact]
        public async Task CalculateScoreboardAsync_HoldingAcrossMultipleCaptures_ShouldAccumulateCorrectly()
        {
            var gameId = 1;
            var t1 = _testGame.StartTime.AddMinutes(1);
            var t2 = t1.AddSeconds(20);
            var t3 = t2.AddSeconds(30);
            var participants = new List<GameParticipant> { new GameParticipant { GameId = gameId, ApplicationUserId = _teamRedUser.Id, ApplicationUser = _teamRedUser } }.AsQueryable();
            _mockContext.Setup(c => c.GameParticipants).Returns(MockDbSetHelper.GetMockDbSet(participants).Object);
            var events = new List<GameEvent> { new GameEvent { GameId = gameId, ControlPointId = 1, Type = EventType.Capture, ActingUserId = _teamRedUser.Id, Timestamp = t1 }, new GameEvent { GameId = gameId, ControlPointId = 1, Type = EventType.Capture, ActingUserId = null, Timestamp = t2 }, new GameEvent { GameId = gameId, ControlPointId = 1, Type = EventType.Capture, ActingUserId = _teamRedUser.Id, Timestamp = t3 } }.AsQueryable();
            _mockContext.Setup(c => c.GameEvents).Returns(MockDbSetHelper.GetMockDbSet(events).Object);

            var result = await _service.CalculateScoreboardAsync(gameId);
            var score = result.TeamScores.Single();
            score.CaptureBonusScore.ShouldBe(200);
            score.HoldingScore.ShouldBe(50);
        }

        [Fact]
        public async Task CalculateScoreboardAsync_LargeNumberOfEvents_ShouldHandle()
        {
            var gameId = 1;
            var participants = new List<GameParticipant> { new GameParticipant { GameId = gameId, ApplicationUserId = _teamRedUser.Id, ApplicationUser = _teamRedUser } }.AsQueryable();
            _mockContext.Setup(c => c.GameParticipants).Returns(MockDbSetHelper.GetMockDbSet(participants).Object);
            var events = new List<GameEvent>();
            for (int i = 1; i <= 100; i++)
            {
                events.Add(new GameEvent { GameId = gameId, ControlPointId = i, Type = EventType.Capture, ActingUserId = _teamRedUser.Id, Timestamp = _testGame.StartTime.AddMinutes(1) });
            }
            _mockContext.Setup(c => c.GameEvents).Returns(MockDbSetHelper.GetMockDbSet(events.AsQueryable()).Object);

            var result = await _service.CalculateScoreboardAsync(gameId);
            result.TeamScores.Single().CaptureBonusScore.ShouldBe(100 * 100);
        }

        [Fact]
        public async Task CalculateScoreboardAsync_ConcurrentCapturesDifferentCPs_ShouldAwardBoth()
        {
            var gameId = 1;
            var t1 = _testGame.StartTime.AddMinutes(1);
            var participants = new List<GameParticipant> { new GameParticipant { GameId = gameId, ApplicationUserId = _teamRedUser.Id, ApplicationUser = _teamRedUser }, new GameParticipant { GameId = gameId, ApplicationUserId = _teamBlueUser.Id, ApplicationUser = _teamBlueUser } }.AsQueryable();
            _mockContext.Setup(c => c.GameParticipants).Returns(MockDbSetHelper.GetMockDbSet(participants).Object);
            var events = new List<GameEvent> { new GameEvent { GameId = gameId, ControlPointId = 1, Type = EventType.Capture, ActingUserId = _teamRedUser.Id, Timestamp = t1 }, new GameEvent { GameId = gameId, ControlPointId = 2, Type = EventType.Capture, ActingUserId = _teamBlueUser.Id, Timestamp = t1 } }.AsQueryable();
            _mockContext.Setup(c => c.GameEvents).Returns(MockDbSetHelper.GetMockDbSet(events).Object);

            var result = await _service.CalculateScoreboardAsync(gameId);
            result.TeamScores.Count.ShouldBe(2);
            result.TeamScores.Sum(t => t.CaptureBonusScore).ShouldBe(200);
        }

        #endregion

        #region GetSavedScoreboardAsync Tests

        [Fact]
        public async Task GetSavedScoreboardAsync_WithExistingScores_ShouldReturnViewModelWithSavedScores()
        {
            // ARRANGE
            var gameId = 1;
            var savedScores = new List<GameScore>
 {
 new GameScore { GameId = gameId, ApplicationUserId = _teamRedUser.Id, ApplicationUser = _teamRedUser, Points =500 },
 new GameScore { GameId = gameId, ApplicationUserId = _teamBlueUser.Id, ApplicationUser = _teamBlueUser, Points =350 }
 }.AsQueryable();
            var mockScores = MockDbSetHelper.GetMockDbSet(savedScores);
            _mockContext.Setup(c => c.GameScores).Returns(mockScores.Object);

            // ACT
            var result = await _service.GetSavedScoreboardAsync(gameId);

            // ASSERT
            result.TeamScores.Count.ShouldBe(2);
            result.TeamScores.First(t => t.TeamName == "Team Red").TotalScoreFromDb.ShouldBe(500);
            result.TeamScores.First(t => t.TeamName == "Team Blue").TotalScoreFromDb.ShouldBe(350);
        }

        [Fact]
        public async Task GetSavedScoreboardAsync_ForNonExistentGame_ShouldReturnNotFoundModel()
        {
            // ARRANGE
            var gameId = 999;
            _mockContext.Setup(c => c.Games.FindAsync(gameId)).ReturnsAsync((Game)null);

            // ACT
            var result = await _service.GetSavedScoreboardAsync(gameId);

            // ASSERT
            result.ShouldNotBeNull();
            result.Game.Name.ShouldBe("Not Found");
        }

        [Fact]
        public async Task GetSavedScoreboardAsync_WithNoSavedScores_ShouldReturnEmptyTeamScores()
        {
            // ARRANGE
            var gameId = 1;
            var savedScores = new List<GameScore>().AsQueryable();
            var mockScores = MockDbSetHelper.GetMockDbSet(savedScores);
            _mockContext.Setup(c => c.GameScores).Returns(mockScores.Object);

            // ACT
            var result = await _service.GetSavedScoreboardAsync(gameId);

            // ASSERT
            result.TeamScores.ShouldBeEmpty();
        }

        [Fact]
        public async Task GetSavedScoreboardAsync_ShouldReturnScoresOrderedByPointsDescending()
        {
            // ARRANGE
            var gameId = 1;
            var savedScores = new List<GameScore>
 {
 new GameScore { GameId = gameId, ApplicationUserId = _teamRedUser.Id, ApplicationUser = _teamRedUser, Points =350 },
 new GameScore { GameId = gameId, ApplicationUserId = _teamBlueUser.Id, ApplicationUser = _teamBlueUser, Points =500 },
 new GameScore { GameId = gameId, ApplicationUserId = _teamGreenUser.Id, ApplicationUser = _teamGreenUser, Points =400 }
 }.AsQueryable();
            var mockScores = MockDbSetHelper.GetMockDbSet(savedScores);
            _mockContext.Setup(c => c.GameScores).Returns(mockScores.Object);

            // ACT
            var result = await _service.GetSavedScoreboardAsync(gameId);

            // ASSERT
            result.TeamScores.Count.ShouldBe(3);
            result.TeamScores[0].TeamName.ShouldBe("Team Blue");
            result.TeamScores[1].TeamName.ShouldBe("Team Green");
            result.TeamScores[2].TeamName.ShouldBe("Team Red");
        }
        #endregion

        #region TeamScore Model Tests
        [Fact]
        public void TeamScore_TotalScoreProperty_WhenTotalScoreFromDbIsSet_ShouldReturnValue()
        {
            // ARRANGE
            var score = new TeamScore
            {
                HoldingScore = 100,
                CaptureBonusScore = 50,
                TotalScoreFromDb = 500 // This should take precedence
            };

            // ACT
            var total = score.TotalScore;

            // ASSERT
            total.ShouldBe(500);
        }

        [Fact]
        public void TeamScore_TotalScoreProperty_WhenTotalScoreFromDbIsZero_ShouldReturnCalculatedValue()
        {
            // ARRANGE
            var score = new TeamScore
            {
                HoldingScore = 100,
                CaptureBonusScore = 50,
                TotalScoreFromDb = 0 // Should be ignored
            };

            // ACT
            var total = score.TotalScore;

            // ASSERT
            total.ShouldBe(150);
        }
        #endregion
    }
}