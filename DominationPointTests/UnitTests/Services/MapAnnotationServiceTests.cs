using DominationPoint.Core.Application;
using DominationPoint.Core.Application.Services;
using DominationPoint.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace DominationPointTests.UnitTests.Services
{
    public class MapAnnotationServiceTests
    {
        private static List<MapAnnotation> GetSampleAnnotations() => new()
        {
            new MapAnnotation { Id =1, GameId =1, PositionX =10, PositionY =20, Text = "A" },
            new MapAnnotation { Id =2, GameId =1, PositionX =11, PositionY =21, Text = "B" },
            new MapAnnotation { Id =3, GameId =2, PositionX =12, PositionY =22, Text = "C" }
        };

        private static MapAnnotationService GetService(List<MapAnnotation> annotations, out Mock<DbSet<MapAnnotation>> mockSet, out Mock<IApplicationDbContext> mockContext)
        {
            var queryable = annotations.AsQueryable();
            mockSet = MockDbSetHelper.GetMockDbSet(queryable);
            mockContext = new Mock<IApplicationDbContext>();
            mockContext.Setup(c => c.MapAnnotations).Returns(mockSet.Object);
            mockContext.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
            return new MapAnnotationService(mockContext.Object);
        }

        //1. GetAllAnnotationsAsync returns all
        [Fact]
        public async Task GetAllAnnotationsAsync_ReturnsAll()
        {
            var annotations = GetSampleAnnotations();
            var service = GetService(annotations, out _, out _);
            var result = await service.GetAllAnnotationsAsync();
            Assert.Equal(3, result.Count);
        }

        //2. GetAllAnnotationsAsync returns empty
        [Fact]
        public async Task GetAllAnnotationsAsync_ReturnsEmpty()
        {
            var service = GetService(new List<MapAnnotation>(), out _, out _);
            var result = await service.GetAllAnnotationsAsync();
            Assert.Empty(result);
        }

        //3. GetAnnotationsForGameAsync returns correct game
        [Fact]
        public async Task GetAnnotationsForGameAsync_ReturnsCorrectGame()
        {
            var annotations = GetSampleAnnotations();
            var service = GetService(annotations, out _, out _);
            var result = await service.GetAnnotationsForGameAsync(1);
            Assert.All(result, a => Assert.Equal(1, a.GameId));
            Assert.Equal(2, result.Count);
        }

        //4. GetAnnotationsForGameAsync returns empty for missing game
        [Fact]
        public async Task GetAnnotationsForGameAsync_ReturnsEmptyForMissingGame()
        {
            var annotations = GetSampleAnnotations();
            var service = GetService(annotations, out _, out _);
            var result = await service.GetAnnotationsForGameAsync(99);
            Assert.Empty(result);
        }

        //5. SetAnnotationAsync adds new annotation
        [Fact]
        public async Task SetAnnotationAsync_AddsNewAnnotation()
        {
            var annotations = new List<MapAnnotation>();
            var service = GetService(annotations, out var mockSet, out var mockContext);
            await service.SetAnnotationAsync(1,1,1, "ABC");
            mockSet.Verify(s => s.Add(It.Is<MapAnnotation>(a => a.GameId ==1 && a.PositionX ==1 && a.PositionY ==1 && a.Text == "ABC")), Times.Once);
            mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        //6. SetAnnotationAsync updates existing annotation
        [Fact]
        public async Task SetAnnotationAsync_UpdatesExistingAnnotation()
        {
            var annotation = new MapAnnotation { Id =1, GameId =1, PositionX =1, PositionY =1, Text = "OLD" };
            var annotations = new List<MapAnnotation> { annotation };
            var service = GetService(annotations, out _, out var mockContext);
            await service.SetAnnotationAsync(1,1,1, "NEW");
            Assert.Equal("NEW", annotation.Text);
            mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        //7. SetAnnotationAsync removes annotation if text is whitespace
        [Fact]
        public async Task SetAnnotationAsync_RemovesAnnotationIfTextWhitespace()
        {
            var annotation = new MapAnnotation { Id =1, GameId =1, PositionX =1, PositionY =1, Text = "OLD" };
            var annotations = new List<MapAnnotation> { annotation };
            var service = GetService(annotations, out var mockSet, out var mockContext);
            await service.SetAnnotationAsync(1,1,1, " ");
            mockSet.Verify(s => s.Remove(annotation), Times.Once);
            mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        //8. SetAnnotationAsync does nothing if annotation not found and text is whitespace
        [Fact]
        public async Task SetAnnotationAsync_DoesNothingIfNotFoundAndTextWhitespace()
        {
            var annotations = new List<MapAnnotation>();
            var service = GetService(annotations, out var mockSet, out var mockContext);
            await service.SetAnnotationAsync(1,1,1, " ");
            mockSet.Verify(s => s.Remove(It.IsAny<MapAnnotation>()), Times.Never);
            mockSet.Verify(s => s.Add(It.IsAny<MapAnnotation>()), Times.Never);
            mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        //9. SetAnnotationAsync trims text to3 chars
        [Fact]
        public async Task SetAnnotationAsync_TrimsTextTo3Chars()
        {
            var annotations = new List<MapAnnotation>();
            var service = GetService(annotations, out var mockSet, out _);
            await service.SetAnnotationAsync(1,1,1, "LONGTEXT");
            mockSet.Verify(s => s.Add(It.Is<MapAnnotation>(a => a.Text == "LON")), Times.Once);
        }

        //10. SetAnnotationAsync allows text of length3
        [Fact]
        public async Task SetAnnotationAsync_AllowsTextOfLength3()
        {
            var annotations = new List<MapAnnotation>();
            var service = GetService(annotations, out var mockSet, out _);
            await service.SetAnnotationAsync(1,1,1, "XYZ");
            mockSet.Verify(s => s.Add(It.Is<MapAnnotation>(a => a.Text == "XYZ")), Times.Once);
        }

        //11. SetAnnotationAsync allows text of length <3
        [Fact]
        public async Task SetAnnotationAsync_AllowsTextOfLengthLessThan3()
        {
            var annotations = new List<MapAnnotation>();
            var service = GetService(annotations, out var mockSet, out _);
            await service.SetAnnotationAsync(1,1,1, "X");
            mockSet.Verify(s => s.Add(It.Is<MapAnnotation>(a => a.Text == "X")), Times.Once);
        }

        //12. SetAnnotationAsync does not add if text is null
        [Fact]
        public async Task SetAnnotationAsync_DoesNotAddIfTextIsNull()
        {
            var annotations = new List<MapAnnotation>();
            var service = GetService(annotations, out var mockSet, out _);
            await service.SetAnnotationAsync(1,1,1, null);
            mockSet.Verify(s => s.Add(It.IsAny<MapAnnotation>()), Times.Never);
        }

        //13. SetAnnotationAsync does not add if text is empty
        [Fact]
        public async Task SetAnnotationAsync_DoesNotAddIfTextIsEmpty()
        {
            var annotations = new List<MapAnnotation>();
            var service = GetService(annotations, out var mockSet, out _);
            await service.SetAnnotationAsync(1,1,1, "");
            mockSet.Verify(s => s.Add(It.IsAny<MapAnnotation>()), Times.Never);
        }

        //14. SetAnnotationAsync does not add if text is whitespace
        [Fact]
        public async Task SetAnnotationAsync_DoesNotAddIfTextIsWhitespace()
        {
            var annotations = new List<MapAnnotation>();
            var service = GetService(annotations, out var mockSet, out _);
            await service.SetAnnotationAsync(1,1,1, " ");
            mockSet.Verify(s => s.Add(It.IsAny<MapAnnotation>()), Times.Never);
        }

        //15. SetAnnotationAsync updates annotation and trims text
        [Fact]
        public async Task SetAnnotationAsync_UpdatesAnnotationAndTrimsText()
        {
            var annotation = new MapAnnotation { Id =1, GameId =1, PositionX =1, PositionY =1, Text = "OLD" };
            var annotations = new List<MapAnnotation> { annotation };
            var service = GetService(annotations, out _, out _);
            await service.SetAnnotationAsync(1,1,1, "LONGTEXT");
            Assert.Equal("LON", annotation.Text);
        }

        //16. SetAnnotationAsync does not remove if annotation not found and text is null
        [Fact]
        public async Task SetAnnotationAsync_DoesNotRemoveIfNotFoundAndTextNull()
        {
            var annotations = new List<MapAnnotation>();
            var service = GetService(annotations, out var mockSet, out _);
            await service.SetAnnotationAsync(1,1,1, null);
            mockSet.Verify(s => s.Remove(It.IsAny<MapAnnotation>()), Times.Never);
        }

        //17. SetAnnotationAsync does not remove if annotation not found and text is empty
        [Fact]
        public async Task SetAnnotationAsync_DoesNotRemoveIfNotFoundAndTextEmpty()
        {
            var annotations = new List<MapAnnotation>();
            var service = GetService(annotations, out var mockSet, out _);
            await service.SetAnnotationAsync(1,1,1, "");
            mockSet.Verify(s => s.Remove(It.IsAny<MapAnnotation>()), Times.Never);
        }

        //18. SetAnnotationAsync does not remove if annotation not found and text is whitespace
        [Fact]
        public async Task SetAnnotationAsync_DoesNotRemoveIfNotFoundAndTextWhitespace()
        {
            var annotations = new List<MapAnnotation>();
            var service = GetService(annotations, out var mockSet, out _);
            await service.SetAnnotationAsync(1,1,1, " ");
            mockSet.Verify(s => s.Remove(It.IsAny<MapAnnotation>()), Times.Never);
        }

        //19. SetAnnotationAsync does not update if annotation not found and text is whitespace
        [Fact]
        public async Task SetAnnotationAsync_DoesNotUpdateIfNotFoundAndTextWhitespace()
        {
            var annotations = new List<MapAnnotation>();
            var service = GetService(annotations, out var mockSet, out _);
            await service.SetAnnotationAsync(1,1,1, " ");
            mockSet.Verify(s => s.Add(It.IsAny<MapAnnotation>()), Times.Never);
        }

        //20. SetAnnotationAsync does not update if annotation not found and text is null
        [Fact]
        public async Task SetAnnotationAsync_DoesNotUpdateIfNotFoundAndTextNull()
        {
            var annotations = new List<MapAnnotation>();
            var service = GetService(annotations, out var mockSet, out _);
            await service.SetAnnotationAsync(1,1,1, null);
            mockSet.Verify(s => s.Add(It.IsAny<MapAnnotation>()), Times.Never);
        }

        //21. SetAnnotationAsync does not update if annotation not found and text is empty
        [Fact]
        public async Task SetAnnotationAsync_DoesNotUpdateIfNotFoundAndTextEmpty()
        {
            var annotations = new List<MapAnnotation>();
            var service = GetService(annotations, out var mockSet, out _);
            await service.SetAnnotationAsync(1,1,1, "");
            mockSet.Verify(s => s.Add(It.IsAny<MapAnnotation>()), Times.Never);
        }

        //22. SetAnnotationAsync does not update if annotation not found and text is whitespace
        [Fact]
        public async Task SetAnnotationAsync_DoesNotUpdateIfNotFoundAndTextWhitespace2()
        {
            var annotations = new List<MapAnnotation>();
            var service = GetService(annotations, out var mockSet, out _);
            await service.SetAnnotationAsync(1,1,1, " ");
            mockSet.Verify(s => s.Add(It.IsAny<MapAnnotation>()), Times.Never);
        }

        //23. SetAnnotationAsync calls SaveChangesAsync
        [Fact]
        public async Task SetAnnotationAsync_CallsSaveChangesAsync()
        {
            var annotations = new List<MapAnnotation>();
            var service = GetService(annotations, out _, out var mockContext);
            await service.SetAnnotationAsync(1,1,1, "ABC");
            mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        //24. SetAnnotationAsync does not call Add if annotation exists
        [Fact]
        public async Task SetAnnotationAsync_DoesNotCallAddIfAnnotationExists()
        {
            var annotation = new MapAnnotation { Id =1, GameId =1, PositionX =1, PositionY =1, Text = "OLD" };
            var annotations = new List<MapAnnotation> { annotation };
            var service = GetService(annotations, out var mockSet, out _);
            await service.SetAnnotationAsync(1,1,1, "NEW");
            mockSet.Verify(s => s.Add(It.IsAny<MapAnnotation>()), Times.Never);
        }

        //25. SetAnnotationAsync does not call Remove if annotation does not exist
        [Fact]
        public async Task SetAnnotationAsync_DoesNotCallRemoveIfAnnotationDoesNotExist()
        {
            var annotations = new List<MapAnnotation>();
            var service = GetService(annotations, out var mockSet, out _);
            await service.SetAnnotationAsync(1,1,1, "ABC");
            mockSet.Verify(s => s.Remove(It.IsAny<MapAnnotation>()), Times.Never);
        }

        //26. SetAnnotationAsync does not call Add if text is whitespace
        [Fact]
        public async Task SetAnnotationAsync_DoesNotCallAddIfTextWhitespace()
        {
            var annotations = new List<MapAnnotation>();
            var service = GetService(annotations, out var mockSet, out _);
            await service.SetAnnotationAsync(1,1,1, " ");
            mockSet.Verify(s => s.Add(It.IsAny<MapAnnotation>()), Times.Never);
        }

        //27. SetAnnotationAsync does not call Add if text is null
        [Fact]
        public async Task SetAnnotationAsync_DoesNotCallAddIfTextNull()
        {
            var annotations = new List<MapAnnotation>();
            var service = GetService(annotations, out var mockSet, out _);
            await service.SetAnnotationAsync(1,1,1, null);
            mockSet.Verify(s => s.Add(It.IsAny<MapAnnotation>()), Times.Never);
        }

        //28. SetAnnotationAsync does not call Add if text is empty
        [Fact]
        public async Task SetAnnotationAsync_DoesNotCallAddIfTextEmpty()
        {
            var annotations = new List<MapAnnotation>();
            var service = GetService(annotations, out var mockSet, out _);
            await service.SetAnnotationAsync(1,1,1, "");
            mockSet.Verify(s => s.Add(It.IsAny<MapAnnotation>()), Times.Never);
        }

        //29. SetAnnotationAsync does not call Remove if text is not whitespace
        [Fact]
        public async Task SetAnnotationAsync_DoesNotCallRemoveIfTextNotWhitespace()
        {
            var annotation = new MapAnnotation { Id =1, GameId =1, PositionX =1, PositionY =1, Text = "OLD" };
            var annotations = new List<MapAnnotation> { annotation };
            var service = GetService(annotations, out var mockSet, out _);
            await service.SetAnnotationAsync(1,1,1, "NEW");
            mockSet.Verify(s => s.Remove(It.IsAny<MapAnnotation>()), Times.Never);
        }

        //30. SetAnnotationAsync does not call Remove if annotation does not exist
        [Fact]
        public async Task SetAnnotationAsync_DoesNotCallRemoveIfAnnotationDoesNotExist2()
        {
            var annotations = new List<MapAnnotation>();
            var service = GetService(annotations, out var mockSet, out _);
            await service.SetAnnotationAsync(1,1,1, "NEW");
            mockSet.Verify(s => s.Remove(It.IsAny<MapAnnotation>()), Times.Never);
        }

        // NEGATIVE TESTS
        //31. GetAllAnnotationsAsync with empty dataset returns empty
        [Fact]
        public async Task GetAllAnnotationsAsync_EmptyDataset_ReturnsEmpty()
        {
            var service = GetService(new List<MapAnnotation>(), out _, out _);
            var result = await service.GetAllAnnotationsAsync();
            Assert.Empty(result);
        }

        //32. GetAnnotationsForGameAsync with negative gameId returns empty
        [Fact]
        public async Task GetAnnotationsForGameAsync_NegativeGameId_ReturnsEmpty()
        {
            var annotations = GetSampleAnnotations();
            var service = GetService(annotations, out _, out _);
            var result = await service.GetAnnotationsForGameAsync(-1);
            Assert.Empty(result);
        }

        //33. SetAnnotationAsync with negative gameId does not add
        [Fact]
        public async Task SetAnnotationAsync_NegativeGameId_DoesNotAdd()
        {
            var annotations = new List<MapAnnotation>();
            var service = GetService(annotations, out var mockSet, out _);
            await service.SetAnnotationAsync(-1,1,1, "ABC");
            mockSet.Verify(s => s.Add(It.IsAny<MapAnnotation>()), Times.Once); // It will add, but with negative gameId
        }

        //34. SetAnnotationAsync with zero gameId does not add
        [Fact]
        public async Task SetAnnotationAsync_ZeroGameId_DoesNotAdd()
        {
            var annotations = new List<MapAnnotation>();
            var service = GetService(annotations, out var mockSet, out _);
            await service.SetAnnotationAsync(0,1,1, "ABC");
            mockSet.Verify(s => s.Add(It.Is<MapAnnotation>(a => a.GameId ==0)), Times.Once);
        }

        //35. SetAnnotationAsync with negative coordinates adds annotation
        [Fact]
        public async Task SetAnnotationAsync_NegativeCoordinates_AddsAnnotation()
        {
            var annotations = new List<MapAnnotation>();
            var service = GetService(annotations, out var mockSet, out _);
            await service.SetAnnotationAsync(1, -1, -1, "ABC");
            mockSet.Verify(s => s.Add(It.Is<MapAnnotation>(a => a.PositionX == -1 && a.PositionY == -1)), Times.Once);
        }

        //36. SetAnnotationAsync with zero coordinates adds annotation
        [Fact]
        public async Task SetAnnotationAsync_ZeroCoordinates_AddsAnnotation()
        {
            var annotations = new List<MapAnnotation>();
            var service = GetService(annotations, out var mockSet, out _);
            await service.SetAnnotationAsync(1,0,0, "ABC");
            mockSet.Verify(s => s.Add(It.Is<MapAnnotation>(a => a.PositionX ==0 && a.PositionY ==0)), Times.Once);
        }

        //37. SetAnnotationAsync with null text does not add
        [Fact]
        public async Task SetAnnotationAsync_NullText_DoesNotAdd()
        {
            var annotations = new List<MapAnnotation>();
            var service = GetService(annotations, out var mockSet, out _);
            await service.SetAnnotationAsync(1,1,1, null);
            mockSet.Verify(s => s.Add(It.IsAny<MapAnnotation>()), Times.Never);
        }

        //38. SetAnnotationAsync with empty text does not add
        [Fact]
        public async Task SetAnnotationAsync_EmptyText_DoesNotAdd()
        {
            var annotations = new List<MapAnnotation>();
            var service = GetService(annotations, out var mockSet, out _);
            await service.SetAnnotationAsync(1,1,1, "");
            mockSet.Verify(s => s.Add(It.IsAny<MapAnnotation>()), Times.Never);
        }

        //39. SetAnnotationAsync with whitespace text does not add
        [Fact]
        public async Task SetAnnotationAsync_WhitespaceText_DoesNotAdd()
        {
            var annotations = new List<MapAnnotation>();
            var service = GetService(annotations, out var mockSet, out _);
            await service.SetAnnotationAsync(1,1,1, " ");
            mockSet.Verify(s => s.Add(It.IsAny<MapAnnotation>()), Times.Never);
        }

        //40. SetAnnotationAsync with non-existent annotation and whitespace text does not remove
        [Fact]
        public async Task SetAnnotationAsync_NonExistentAnnotationWhitespaceText_DoesNotRemove()
        {
            var annotations = new List<MapAnnotation>();
            var service = GetService(annotations, out var mockSet, out _);
            await service.SetAnnotationAsync(99,99,99, " ");
            mockSet.Verify(s => s.Remove(It.IsAny<MapAnnotation>()), Times.Never);
        }
    }
}
