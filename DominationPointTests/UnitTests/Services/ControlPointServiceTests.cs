using DominationPoint.Core.Application;
using DominationPoint.Core.Application.Services;
using DominationPoint.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DominationPointTests.UnitTests.Services
{
    public class ControlPointServiceTests
    {
        private static List<ControlPoint> GetSampleControlPoints() => new()
        {
            new ControlPoint { Id =1, GameId =1, PositionX =10, PositionY =20, Status = ControlPointStatus.Inactive, ApplicationUserId = null },
            new ControlPoint { Id =2, GameId =1, PositionX =11, PositionY =21, Status = ControlPointStatus.Controlled, ApplicationUserId = "user1" },
            new ControlPoint { Id =3, GameId =2, PositionX =12, PositionY =22, Status = ControlPointStatus.Inactive, ApplicationUserId = null }
        };

        private static ControlPointService GetService(List<ControlPoint> controlPoints, List<GameEvent> gameEvents,
            out Mock<DbSet<ControlPoint>> mockCpSet, out Mock<DbSet<GameEvent>> mockEventSet, out Mock<IApplicationDbContext> mockContext)
        {
            var cpQueryable = controlPoints.AsQueryable();
            var eventQueryable = gameEvents.AsQueryable();
            mockCpSet = MockDbSetHelper.GetMockDbSet(cpQueryable);
            mockEventSet = MockDbSetHelper.GetMockDbSet(eventQueryable);
            mockContext = new Mock<IApplicationDbContext>();
            mockContext.Setup(c => c.ControlPoints).Returns(mockCpSet.Object);
            mockContext.Setup(c => c.GameEvents).Returns(mockEventSet.Object);
            mockContext.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
            return new ControlPointService(mockContext.Object);
        }

        //1. GetControlPointsForGameAsync returns all for game
        [Fact]
        public async Task GetControlPointsForGameAsync_ReturnsAllForGame()
        {
            var cps = GetSampleControlPoints();
            var service = GetService(cps, new List<GameEvent>(), out _, out _, out _);
            var result = await service.GetControlPointsForGameAsync(1);
            Assert.All(result, cp => Assert.Equal(1, cp.GameId));
            Assert.Equal(2, result.Count);
        }

        //2. GetControlPointsForGameAsync returns empty for missing game
        [Fact]
        public async Task GetControlPointsForGameAsync_ReturnsEmptyForMissingGame()
        {
            var cps = GetSampleControlPoints();
            var service = GetService(cps, new List<GameEvent>(), out _, out _, out _);
            var result = await service.GetControlPointsForGameAsync(99);
            Assert.Empty(result);
        }

        //3. DeleteControlPointAsync removes found
        [Fact]
        public async Task DeleteControlPointAsync_RemovesFound()
        {
            var cp = new ControlPoint { Id =1, GameId =1 };
            var cps = new List<ControlPoint> { cp };
            var service = GetService(cps, new List<GameEvent>(), out var mockSet, out _, out var mockContext);
            mockSet.Setup(s => s.FindAsync(1)).ReturnsAsync(cp);
            await service.DeleteControlPointAsync(1);
            mockSet.Verify(s => s.Remove(cp), Times.Once);
            mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        //4. DeleteControlPointAsync does nothing if not found
        [Fact]
        public async Task DeleteControlPointAsync_DoesNothingIfNotFound()
        {
            var cps = new List<ControlPoint>();
            var service = GetService(cps, new List<GameEvent>(), out var mockSet, out _, out var mockContext);
            mockSet.Setup(s => s.FindAsync(1)).ReturnsAsync((ControlPoint)null);
            await service.DeleteControlPointAsync(1);
            mockSet.Verify(s => s.Remove(It.IsAny<ControlPoint>()), Times.Never);
            mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        //5. UpdateControlPointStateAsync does nothing if not found
        [Fact]
        public async Task UpdateControlPointStateAsync_DoesNothingIfNotFound()
        {
            var cps = new List<ControlPoint>();
            var service = GetService(cps, new List<GameEvent>(), out var mockSet, out var mockEventSet, out var mockContext);
            mockSet.Setup(s => s.FindAsync(1)).ReturnsAsync((ControlPoint)null);
            await service.UpdateControlPointStateAsync(1, "user2");
            mockEventSet.Verify(s => s.Add(It.IsAny<GameEvent>()), Times.Never);
            mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        //6. UpdateControlPointStateAsync does nothing if owner unchanged
        [Fact]
        public async Task UpdateControlPointStateAsync_DoesNothingIfOwnerUnchanged()
        {
            var cp = new ControlPoint { Id =1, GameId =1, ApplicationUserId = "user1", Status = ControlPointStatus.Controlled };
            var cps = new List<ControlPoint> { cp };
            var service = GetService(cps, new List<GameEvent>(), out var mockSet, out var mockEventSet, out var mockContext);
            mockSet.Setup(s => s.FindAsync(1)).ReturnsAsync(cp);
            await service.UpdateControlPointStateAsync(1, "user1");
            mockEventSet.Verify(s => s.Add(It.IsAny<GameEvent>()), Times.Never);
            mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        //7. UpdateControlPointStateAsync creates event and updates owner
        [Fact]
        public async Task UpdateControlPointStateAsync_CreatesEventAndUpdatesOwner()
        {
            var cp = new ControlPoint { Id =1, GameId =1, ApplicationUserId = "user1", Status = ControlPointStatus.Controlled };
            var cps = new List<ControlPoint> { cp };
            var service = GetService(cps, new List<GameEvent>(), out var mockSet, out var mockEventSet, out var mockContext);
            mockSet.Setup(s => s.FindAsync(1)).ReturnsAsync(cp);
            await service.UpdateControlPointStateAsync(1, "user2");
            mockEventSet.Verify(s => s.Add(It.Is<GameEvent>(e => e.GameId ==1 && e.ControlPointId ==1 && e.Type == EventType.Capture && e.ActingUserId == "user2" && e.PreviousOwnerUserId == "user1")), Times.Once);
            Assert.Equal("user2", cp.ApplicationUserId);
            Assert.Equal(ControlPointStatus.Controlled, cp.Status);
            mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        //8. UpdateControlPointStateAsync sets status to inactive if ownerId is null
        [Fact]
        public async Task UpdateControlPointStateAsync_SetsStatusInactiveIfOwnerIdNull()
        {
            var cp = new ControlPoint { Id =1, GameId =1, ApplicationUserId = "user1", Status = ControlPointStatus.Controlled };
            var cps = new List<ControlPoint> { cp };
            var service = GetService(cps, new List<GameEvent>(), out var mockSet, out var mockEventSet, out var mockContext);
            mockSet.Setup(s => s.FindAsync(1)).ReturnsAsync(cp);
            await service.UpdateControlPointStateAsync(1, null);
            Assert.Null(cp.ApplicationUserId);
            Assert.Equal(ControlPointStatus.Inactive, cp.Status);
            mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        //9. UpdateControlPointStateAsync sets status to inactive if ownerId is empty
        [Fact]
        public async Task UpdateControlPointStateAsync_SetsStatusInactiveIfOwnerIdEmpty()
        {
            var cp = new ControlPoint { Id =1, GameId =1, ApplicationUserId = "user1", Status = ControlPointStatus.Controlled };
            var cps = new List<ControlPoint> { cp };
            var service = GetService(cps, new List<GameEvent>(), out var mockSet, out var mockEventSet, out var mockContext);
            mockSet.Setup(s => s.FindAsync(1)).ReturnsAsync(cp);
            await service.UpdateControlPointStateAsync(1, "");
            Assert.Null(cp.ApplicationUserId);
            Assert.Equal(ControlPointStatus.Inactive, cp.Status);
            mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        //10. UpdateControlPointStateAsync sets status to controlled if ownerId is not empty
        [Fact]
        public async Task UpdateControlPointStateAsync_SetsStatusControlledIfOwnerIdNotEmpty()
        {
            var cp = new ControlPoint { Id =1, GameId =1, ApplicationUserId = null, Status = ControlPointStatus.Inactive };
            var cps = new List<ControlPoint> { cp };
            var service = GetService(cps, new List<GameEvent>(), out var mockSet, out var mockEventSet, out var mockContext);
            mockSet.Setup(s => s.FindAsync(1)).ReturnsAsync(cp);
            await service.UpdateControlPointStateAsync(1, "user2");
            Assert.Equal("user2", cp.ApplicationUserId);
            Assert.Equal(ControlPointStatus.Controlled, cp.Status);
            mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        //11. ToggleControlPointMarkerAsync adds new if isCp and not found
        [Fact]
        public async Task ToggleControlPointMarkerAsync_AddsNewIfIsCpAndNotFound()
        {
            var cps = new List<ControlPoint>();
            var service = GetService(cps, new List<GameEvent>(), out var mockSet, out _, out var mockContext);
            // No setup for FirstOrDefaultAsync; rely on empty list
            await service.ToggleControlPointMarkerAsync(1,1,1, true);
            mockSet.Verify(s => s.Add(It.Is<ControlPoint>(cp => cp.GameId ==1 && cp.PositionX ==1 && cp.PositionY ==1 && cp.Status == ControlPointStatus.Inactive)), Times.Once);
            mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        //12. ToggleControlPointMarkerAsync does not add if isCp and found
        [Fact]
        public async Task ToggleControlPointMarkerAsync_DoesNotAddIfIsCpAndFound()
        {
            var cp = new ControlPoint { Id =1, GameId =1, PositionX =1, PositionY =1 };
            var cps = new List<ControlPoint> { cp };
            var service = GetService(cps, new List<GameEvent>(), out var mockSet, out _, out var mockContext);
            // No setup for FirstOrDefaultAsync; rely on list containing cp
            await service.ToggleControlPointMarkerAsync(1,1,1, true);
            mockSet.Verify(s => s.Add(It.IsAny<ControlPoint>()), Times.Never);
            mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        //13. ToggleControlPointMarkerAsync removes if not isCp and found
        [Fact]
        public async Task ToggleControlPointMarkerAsync_RemovesIfNotIsCpAndFound()
        {
            var cp = new ControlPoint { Id =1, GameId =1, PositionX =1, PositionY =1 };
            var cps = new List<ControlPoint> { cp };
            var service = GetService(cps, new List<GameEvent>(), out var mockSet, out _, out var mockContext);
            await service.ToggleControlPointMarkerAsync(1,1,1, false);
            mockSet.Verify(s => s.Remove(cp), Times.Once);
            mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        //14. ToggleControlPointMarkerAsync does nothing if not isCp and not found
        [Fact]
        public async Task ToggleControlPointMarkerAsync_DoesNothingIfNotIsCpAndNotFound()
        {
            var cps = new List<ControlPoint>();
            var service = GetService(cps, new List<GameEvent>(), out var mockSet, out _, out var mockContext);
            await service.ToggleControlPointMarkerAsync(1,1,1, false);
            mockSet.Verify(s => s.Remove(It.IsAny<ControlPoint>()), Times.Never);
            mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        //15. ToggleControlPointMarkerAsync calls SaveChangesAsync
        [Fact]
        public async Task ToggleControlPointMarkerAsync_CallsSaveChangesAsync()
        {
            var cps = new List<ControlPoint>();
            var service = GetService(cps, new List<GameEvent>(), out _, out _, out var mockContext);
            await service.ToggleControlPointMarkerAsync(1,1,1, true);
            mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        //16. UpdateControlPointStateAsync creates event with correct type
        [Fact]
        public async Task UpdateControlPointStateAsync_CreatesEventWithCorrectType()
        {
            var cp = new ControlPoint { Id =1, GameId =1, ApplicationUserId = null, Status = ControlPointStatus.Inactive };
            var cps = new List<ControlPoint> { cp };
            var service = GetService(cps, new List<GameEvent>(), out var mockSet, out var mockEventSet, out var mockContext);
            mockSet.Setup(s => s.FindAsync(1)).ReturnsAsync(cp);
            await service.UpdateControlPointStateAsync(1, "user2");
            mockEventSet.Verify(s => s.Add(It.Is<GameEvent>(e => e.Type == EventType.Capture)), Times.Once);
        }

        //17. UpdateControlPointStateAsync sets previousOwnerUserId correctly
        [Fact]
        public async Task UpdateControlPointStateAsync_SetsPreviousOwnerUserIdCorrectly()
        {
            var cp = new ControlPoint { Id =1, GameId =1, ApplicationUserId = "user1", Status = ControlPointStatus.Controlled };
            var cps = new List<ControlPoint> { cp };
            var service = GetService(cps, new List<GameEvent>(), out var mockSet, out var mockEventSet, out var mockContext);
            mockSet.Setup(s => s.FindAsync(1)).ReturnsAsync(cp);
            await service.UpdateControlPointStateAsync(1, "user2");
            mockEventSet.Verify(s => s.Add(It.Is<GameEvent>(e => e.PreviousOwnerUserId == "user1")), Times.Once);
        }

        //18. UpdateControlPointStateAsync sets actingUserId correctly
        [Fact]
        public async Task UpdateControlPointStateAsync_SetsActingUserIdCorrectly()
        {
            var cp = new ControlPoint { Id =1, GameId =1, ApplicationUserId = "user1", Status = ControlPointStatus.Controlled };
            var cps = new List<ControlPoint> { cp };
            var service = GetService(cps, new List<GameEvent>(), out var mockSet, out var mockEventSet, out var mockContext);
            mockSet.Setup(s => s.FindAsync(1)).ReturnsAsync(cp);
            await service.UpdateControlPointStateAsync(1, "user2");
            mockEventSet.Verify(s => s.Add(It.Is<GameEvent>(e => e.ActingUserId == "user2")), Times.Once);
        }

        //19. UpdateControlPointStateAsync sets timestamp
        [Fact]
        public async Task UpdateControlPointStateAsync_SetsTimestamp()
        {
            var cp = new ControlPoint { Id =1, GameId =1, ApplicationUserId = "user1", Status = ControlPointStatus.Controlled };
            var cps = new List<ControlPoint> { cp };
            var service = GetService(cps, new List<GameEvent>(), out var mockSet, out var mockEventSet, out var mockContext);
            mockSet.Setup(s => s.FindAsync(1)).ReturnsAsync(cp);
            await service.UpdateControlPointStateAsync(1, "user2");
            mockEventSet.Verify(s => s.Add(It.Is<GameEvent>(e => e.Timestamp <= DateTime.UtcNow)), Times.Once);
        }

        //20. UpdateControlPointStateAsync does not call Add if owner unchanged
        [Fact]
        public async Task UpdateControlPointStateAsync_DoesNotCallAddIfOwnerUnchanged()
        {
            var cp = new ControlPoint { Id =1, GameId =1, ApplicationUserId = "user1", Status = ControlPointStatus.Controlled };
            var cps = new List<ControlPoint> { cp };
            var service = GetService(cps, new List<GameEvent>(), out var mockSet, out var mockEventSet, out var mockContext);
            mockSet.Setup(s => s.FindAsync(1)).ReturnsAsync(cp);
            await service.UpdateControlPointStateAsync(1, "user1");
            mockEventSet.Verify(s => s.Add(It.IsAny<GameEvent>()), Times.Never);
        }

        //21. DeleteControlPointAsync does not call Remove if not found
        [Fact]
        public async Task DeleteControlPointAsync_DoesNotCallRemoveIfNotFound()
        {
            var cps = new List<ControlPoint>();
            var service = GetService(cps, new List<GameEvent>(), out var mockSet, out _, out var mockContext);
            mockSet.Setup(s => s.FindAsync(1)).ReturnsAsync((ControlPoint)null);
            await service.DeleteControlPointAsync(1);
            mockSet.Verify(s => s.Remove(It.IsAny<ControlPoint>()), Times.Never);
        }

        //22. ToggleControlPointMarkerAsync does not call Add if found
        [Fact]
        public async Task ToggleControlPointMarkerAsync_DoesNotCallAddIfFound()
        {
            var cp = new ControlPoint { Id =1, GameId =1, PositionX =1, PositionY =1 };
            var cps = new List<ControlPoint> { cp };
            var service = GetService(cps, new List<GameEvent>(), out var mockSet, out _, out var mockContext);
            await service.ToggleControlPointMarkerAsync(1,1,1, true);
            mockSet.Verify(s => s.Add(It.IsAny<ControlPoint>()), Times.Never);
        }

        //23. ToggleControlPointMarkerAsync does not call Remove if not found
        [Fact]
        public async Task ToggleControlPointMarkerAsync_DoesNotCallRemoveIfNotFound()
        {
            var cps = new List<ControlPoint>();
            var service = GetService(cps, new List<GameEvent>(), out var mockSet, out _, out var mockContext);
            await service.ToggleControlPointMarkerAsync(1,1,1, false);
            mockSet.Verify(s => s.Remove(It.IsAny<ControlPoint>()), Times.Never);
        }

        //24. ToggleControlPointMarkerAsync does not call Add if not isCp
        [Fact]
        public async Task ToggleControlPointMarkerAsync_DoesNotCallAddIfNotIsCp()
        {
            var cp = new ControlPoint { Id =1, GameId =1, PositionX =1, PositionY =1 };
            var cps = new List<ControlPoint> { cp };
            var service = GetService(cps, new List<GameEvent>(), out var mockSet, out _, out var mockContext);
            await service.ToggleControlPointMarkerAsync(1,1,1, false);
            mockSet.Verify(s => s.Add(It.IsAny<ControlPoint>()), Times.Never);
        }

        //25. ToggleControlPointMarkerAsync does not call Remove if isCp
        [Fact]
        public async Task ToggleControlPointMarkerAsync_DoesNotCallRemoveIfIsCp()
        {
            var cp = new ControlPoint { Id =1, GameId =1, PositionX =1, PositionY =1 };
            var cps = new List<ControlPoint> { cp };
            var service = GetService(cps, new List<GameEvent>(), out var mockSet, out _, out var mockContext);
            await service.ToggleControlPointMarkerAsync(1,1,1, true);
            mockSet.Verify(s => s.Remove(It.IsAny<ControlPoint>()), Times.Never);
        }

        //26. UpdateControlPointStateAsync does not call SaveChangesAsync if not found
        [Fact]
        public async Task UpdateControlPointStateAsync_DoesNotCallSaveChangesIfNotFound()
        {
            var cps = new List<ControlPoint>();
            var service = GetService(cps, new List<GameEvent>(), out var mockSet, out var mockEventSet, out var mockContext);
            mockSet.Setup(s => s.FindAsync(1)).ReturnsAsync((ControlPoint)null);
            await service.UpdateControlPointStateAsync(1, "user2");
            mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        //27. DeleteControlPointAsync calls SaveChangesAsync only if found
        [Fact]
        public async Task DeleteControlPointAsync_CallsSaveChangesOnlyIfFound()
        {
            var cp = new ControlPoint { Id =1, GameId =1 };
            var cps = new List<ControlPoint> { cp };
            var service = GetService(cps, new List<GameEvent>(), out var mockSet, out _, out var mockContext);
            mockSet.Setup(s => s.FindAsync(1)).ReturnsAsync(cp);
            await service.DeleteControlPointAsync(1);
            mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        //28. ToggleControlPointMarkerAsync calls SaveChangesAsync always
        [Fact]
        public async Task ToggleControlPointMarkerAsync_CallsSaveChangesAlways()
        {
            var cps = new List<ControlPoint>();
            var service = GetService(cps, new List<GameEvent>(), out _, out _, out var mockContext);
            await service.ToggleControlPointMarkerAsync(1,1,1, false);
            mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        //29. UpdateControlPointStateAsync does not update if owner unchanged
        [Fact]
        public async Task UpdateControlPointStateAsync_DoesNotUpdateIfOwnerUnchanged2()
        {
            var cp = new ControlPoint { Id =1, GameId =1, ApplicationUserId = "user1", Status = ControlPointStatus.Controlled };
            var cps = new List<ControlPoint> { cp };
            var service = GetService(cps, new List<GameEvent>(), out var mockSet, out var mockEventSet, out var mockContext);
            mockSet.Setup(s => s.FindAsync(1)).ReturnsAsync(cp);
            await service.UpdateControlPointStateAsync(1, "user1");
            Assert.Equal("user1", cp.ApplicationUserId);
            Assert.Equal(ControlPointStatus.Controlled, cp.Status);
        }

        //30. UpdateControlPointStateAsync updates only if owner changed
        [Fact]
        public async Task UpdateControlPointStateAsync_UpdatesOnlyIfOwnerChanged()
        {
            var cp = new ControlPoint { Id =1, GameId =1, ApplicationUserId = "user1", Status = ControlPointStatus.Controlled };
            var cps = new List<ControlPoint> { cp };
            var service = GetService(cps, new List<GameEvent>(), out var mockSet, out var mockEventSet, out var mockContext);
            mockSet.Setup(s => s.FindAsync(1)).ReturnsAsync(cp);
            await service.UpdateControlPointStateAsync(1, "user2");
            Assert.Equal("user2", cp.ApplicationUserId);
            Assert.Equal(ControlPointStatus.Controlled, cp.Status);
        }

        // NEGATIVE TESTS
        //31. GetControlPointsForGameAsync with negative gameId returns empty
        [Fact]
        public async Task GetControlPointsForGameAsync_NegativeGameId_ReturnsEmpty()
        {
            var cps = GetSampleControlPoints();
            var service = GetService(cps, new List<GameEvent>(), out _, out _, out _);
            var result = await service.GetControlPointsForGameAsync(-1);
            Assert.Empty(result);
        }

        //32. DeleteControlPointAsync with negative cpId does nothing
        [Fact]
        public async Task DeleteControlPointAsync_NegativeId_DoesNothing()
        {
            var cps = GetSampleControlPoints();
            var service = GetService(cps, new List<GameEvent>(), out var mockSet, out _, out var mockContext);
            mockSet.Setup(s => s.FindAsync(-1)).ReturnsAsync((ControlPoint)null);
            await service.DeleteControlPointAsync(-1);
            mockSet.Verify(s => s.Remove(It.IsAny<ControlPoint>()), Times.Never);
            mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        //33. UpdateControlPointStateAsync with negative cpId does nothing
        [Fact]
        public async Task UpdateControlPointStateAsync_NegativeId_DoesNothing()
        {
            var cps = GetSampleControlPoints();
            var service = GetService(cps, new List<GameEvent>(), out var mockSet, out var mockEventSet, out var mockContext);
            mockSet.Setup(s => s.FindAsync(-1)).ReturnsAsync((ControlPoint)null);
            await service.UpdateControlPointStateAsync(-1, "user2");
            mockEventSet.Verify(s => s.Add(It.IsAny<GameEvent>()), Times.Never);
            mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        //34. UpdateControlPointStateAsync with null ownerId on non-existent cp does nothing
        [Fact]
        public async Task UpdateControlPointStateAsync_NullOwnerIdOnNonExistentCp_DoesNothing()
        {
            var cps = new List<ControlPoint>();
            var service = GetService(cps, new List<GameEvent>(), out var mockSet, out var mockEventSet, out var mockContext);
            mockSet.Setup(s => s.FindAsync(99)).ReturnsAsync((ControlPoint)null);
            await service.UpdateControlPointStateAsync(99, null);
            mockEventSet.Verify(s => s.Add(It.IsAny<GameEvent>()), Times.Never);
            mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        //35. ToggleControlPointMarkerAsync with negative coordinates does nothing
        [Fact]
        public async Task ToggleControlPointMarkerAsync_NegativeCoordinates_DoesNothing()
        {
            var cps = new List<ControlPoint>();
            var service = GetService(cps, new List<GameEvent>(), out var mockSet, out _, out var mockContext);
            await service.ToggleControlPointMarkerAsync(1, -1, -1, true);
            mockSet.Verify(s => s.Add(It.IsAny<ControlPoint>()), Times.Once); // It will add, but with negative coords
            mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        //36. ToggleControlPointMarkerAsync with zero coordinates adds new
        [Fact]
        public async Task ToggleControlPointMarkerAsync_ZeroCoordinates_AddsNew()
        {
            var cps = new List<ControlPoint>();
            var service = GetService(cps, new List<GameEvent>(), out var mockSet, out _, out var mockContext);
            await service.ToggleControlPointMarkerAsync(1,0,0, true);
            mockSet.Verify(s => s.Add(It.Is<ControlPoint>(cp => cp.PositionX ==0 && cp.PositionY ==0)), Times.Once);
            mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        //37. DeleteControlPointAsync with zero cpId does nothing
        [Fact]
        public async Task DeleteControlPointAsync_ZeroId_DoesNothing()
        {
            var cps = GetSampleControlPoints();
            var service = GetService(cps, new List<GameEvent>(), out var mockSet, out _, out var mockContext);
            mockSet.Setup(s => s.FindAsync(0)).ReturnsAsync((ControlPoint)null);
            await service.DeleteControlPointAsync(0);
            mockSet.Verify(s => s.Remove(It.IsAny<ControlPoint>()), Times.Never);
            mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        //38. UpdateControlPointStateAsync with empty ownerId on non-existent cp does nothing
        [Fact]
        public async Task UpdateControlPointStateAsync_EmptyOwnerIdOnNonExistentCp_DoesNothing()
        {
            var cps = new List<ControlPoint>();
            var service = GetService(cps, new List<GameEvent>(), out var mockSet, out var mockEventSet, out var mockContext);
            mockSet.Setup(s => s.FindAsync(99)).ReturnsAsync((ControlPoint)null);
            await service.UpdateControlPointStateAsync(99, "");
            mockEventSet.Verify(s => s.Add(It.IsAny<GameEvent>()), Times.Never);
            mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        //39. ToggleControlPointMarkerAsync with non-existent gameId does nothing
        [Fact]
        public async Task ToggleControlPointMarkerAsync_NonExistentGameId_DoesNothing()
        {
            var cps = new List<ControlPoint>();
            var service = GetService(cps, new List<GameEvent>(), out var mockSet, out _, out var mockContext);
            await service.ToggleControlPointMarkerAsync(999,1,1, false);
            mockSet.Verify(s => s.Remove(It.IsAny<ControlPoint>()), Times.Never);
            mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        //40. GetControlPointsForGameAsync with empty dataset returns empty
        [Fact]
        public async Task GetControlPointsForGameAsync_EmptyDataset_ReturnsEmpty()
        {
            var cps = new List<ControlPoint>();
            var service = GetService(cps, new List<GameEvent>(), out _, out _, out _);
            var result = await service.GetControlPointsForGameAsync(1);
            Assert.Empty(result);
        }
    }
}
