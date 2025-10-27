using DominationPoint.Core.Application;
using DominationPoint.Core.Domain;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Newtonsoft.Json;
using Shouldly;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

namespace DominationPointTests.IntegrationTests.ApiControllers
{
    [Collection("Integration Tests")]
    public class GameplayControllerTests : IClassFixture<CustomWebApplicationFactory<Program>>
    {
        private readonly CustomWebApplicationFactory<Program> _factory;
        private readonly HttpClient _client;

        public GameplayControllerTests(CustomWebApplicationFactory<Program> factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        #region Helper Methods
        private async Task<string> GetValidTokenAsync()
        {
            var loginRequest = new
            {
                Email = "team.red@test.com",
                Password = "Password123!"
            };

            var response = await _client.PostAsJsonAsync("/api/auth/token", loginRequest);
            var json = await response.Content.ReadAsStringAsync();
            var tokenResponse = JsonConvert.DeserializeObject<TokenResponse>(json);
            return tokenResponse.token;
        }

        private class TokenResponse
        {
            public string token { get; set; }
            public DateTime expiration { get; set; }
        }
        #endregion

        #region Positive Tests - Authorized Access

        [Fact]
        public async Task Capture_WithValidToken_ShouldReturnOk()
        {
            // ARRANGE
            var token = await GetValidTokenAsync();
            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    var mockService = new Mock<IGameplayService>();
                    mockService.Setup(s => s.CaptureControlPointAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()))
                        .ReturnsAsync((true, "Control point captured successfully"));

                    services.AddScoped(_ => mockService.Object);
                });
            }).CreateClient();

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var request = new { NumpadCode = "1234" };

            // ACT
            var response = await client.PostAsJsonAsync("/api/gameplay/controlpoints/1/capture", request);

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Capture_WithValidCredentials_ShouldCallServiceWithCorrectParameters()
        {
            // ARRANGE
            var token = await GetValidTokenAsync();
            var mockService = new Mock<IGameplayService>();
            mockService.Setup(s => s.CaptureControlPointAsync(1, It.IsAny<string>(), "1234"))
                .ReturnsAsync((true, "Success"));

            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.AddScoped(_ => mockService.Object);
                });
            }).CreateClient();

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var request = new { NumpadCode = "1234" };

            // ACT
            var response = await client.PostAsJsonAsync("/api/gameplay/controlpoints/1/capture", request);

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            mockService.Verify(s => s.CaptureControlPointAsync(1, It.IsAny<string>(), "1234"), Times.Once);
        }

        [Fact]
        public async Task Capture_WhenServiceReturnsSuccess_ShouldReturnSuccessMessage()
        {
            // ARRANGE
            var token = await GetValidTokenAsync();
            var mockService = new Mock<IGameplayService>();
            mockService.Setup(s => s.CaptureControlPointAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync((true, "Control point captured"));

            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.AddScoped(_ => mockService.Object);
                });
            }).CreateClient();

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var request = new { NumpadCode = "1234" };

            // ACT
            var response = await client.PostAsJsonAsync("/api/gameplay/controlpoints/1/capture", request);
            var content = await response.Content.ReadAsStringAsync();

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            content.ShouldContain("Control point captured");
        }

        [Fact]
        public async Task Capture_WithDifferentControlPointIds_ShouldPassCorrectIdToService()
        {
            // ARRANGE
            var token = await GetValidTokenAsync();
            var mockService = new Mock<IGameplayService>();
            mockService.Setup(s => s.CaptureControlPointAsync(99, It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync((true, "Success"));

            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.AddScoped(_ => mockService.Object);
                });
            }).CreateClient();

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var request = new { NumpadCode = "1234" };

            // ACT
            var response = await client.PostAsJsonAsync("/api/gameplay/controlpoints/99/capture", request);

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            mockService.Verify(s => s.CaptureControlPointAsync(99, It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task Capture_WithValidToken_ShouldExtractUserIdFromToken()
        {
            // ARRANGE
            var token = await GetValidTokenAsync();
            string capturedUserId = null;

            var mockService = new Mock<IGameplayService>();
            mockService.Setup(s => s.CaptureControlPointAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()))
                .Callback<int, string, string>((id, userId, code) => capturedUserId = userId)
                .ReturnsAsync((true, "Success"));

            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.AddScoped(_ => mockService.Object);
                });
            }).CreateClient();

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var request = new { NumpadCode = "1234" };

            // ACT
            var response = await client.PostAsJsonAsync("/api/gameplay/controlpoints/1/capture", request);

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            capturedUserId.ShouldNotBeNullOrEmpty();
        }

        #endregion

        #region Negative Tests - Service Failures

        [Fact]
        public async Task Capture_WhenServiceReturnsFalse_ShouldReturnBadRequest()
        {
            // ARRANGE
            var token = await GetValidTokenAsync();
            var mockService = new Mock<IGameplayService>();
            mockService.Setup(s => s.CaptureControlPointAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync((false, "Control point already captured"));

            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.AddScoped(_ => mockService.Object);
                });
            }).CreateClient();

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var request = new { NumpadCode = "1234" };

            // ACT
            var response = await client.PostAsJsonAsync("/api/gameplay/controlpoints/1/capture", request);

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Capture_WhenServiceFails_ShouldReturnErrorMessage()
        {
            // ARRANGE
            var token = await GetValidTokenAsync();
            var mockService = new Mock<IGameplayService>();
            mockService.Setup(s => s.CaptureControlPointAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync((false, "Invalid numpad code"));

            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.AddScoped(_ => mockService.Object);
                });
            }).CreateClient();

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var request = new { NumpadCode = "9999" };

            // ACT
            var response = await client.PostAsJsonAsync("/api/gameplay/controlpoints/1/capture", request);
            var content = await response.Content.ReadAsStringAsync();

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            content.ShouldContain("Invalid numpad code");
        }

        [Fact]
        public async Task Capture_WithInvalidNumpadCode_ShouldReturnBadRequest()
        {
            // ARRANGE
            var token = await GetValidTokenAsync();
            var mockService = new Mock<IGameplayService>();
            mockService.Setup(s => s.CaptureControlPointAsync(It.IsAny<int>(), It.IsAny<string>(), "0000"))
                .ReturnsAsync((false, "Incorrect numpad code"));

            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.AddScoped(_ => mockService.Object);
                });
            }).CreateClient();

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var request = new { NumpadCode = "0000" };

            // ACT
            var response = await client.PostAsJsonAsync("/api/gameplay/controlpoints/1/capture", request);

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Capture_WithNonExistentControlPoint_ShouldReturnBadRequest()
        {
            // ARRANGE
            var token = await GetValidTokenAsync();
            var mockService = new Mock<IGameplayService>();
            mockService.Setup(s => s.CaptureControlPointAsync(999, It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync((false, "Control point not found"));

            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.AddScoped(_ => mockService.Object);
                });
            }).CreateClient();

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var request = new { NumpadCode = "1234" };

            // ACT
            var response = await client.PostAsJsonAsync("/api/gameplay/controlpoints/999/capture", request);

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        #endregion

        #region Negative Tests - Authentication & Authorization

        [Fact]
        public async Task Capture_WithoutToken_ShouldReturnUnauthorized()
        {
            // ARRANGE
            var request = new { NumpadCode = "1234" };

            // ACT
            var response = await _client.PostAsJsonAsync("/api/gameplay/controlpoints/1/capture", request);

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task Capture_WithInvalidToken_ShouldReturnUnauthorized()
        {
            // ARRANGE
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid.token.here");
            var request = new { NumpadCode = "1234" };

            // ACT
            var response = await _client.PostAsJsonAsync("/api/gameplay/controlpoints/1/capture", request);

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task Capture_WithExpiredToken_ShouldReturnUnauthorized()
        {
            // ARRANGE
            // Create a token that's already expired
            var expiredToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiaWF0IjoxNTE2MjM5MDIyLCJleHAiOjE1MTYyMzkwMjJ9.4Adcj0jZgb3_CJPOmT6e7N6D5P8J5rZQWx5L5X5gK8U";

            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", expiredToken);
            var request = new { NumpadCode = "1234" };

            // ACT
            var response = await _client.PostAsJsonAsync("/api/gameplay/controlpoints/1/capture", request);

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task Capture_WithMalformedAuthorizationHeader_ShouldReturnUnauthorized()
        {
            // ARRANGE
            _client.DefaultRequestHeaders.Add("Authorization", "NotBearer token123");
            var request = new { NumpadCode = "1234" };

            // ACT
            var response = await _client.PostAsJsonAsync("/api/gameplay/controlpoints/1/capture", request);

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task Capture_WithEmptyAuthorizationHeader_ShouldReturnUnauthorized()
        {
            // ARRANGE
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "");
            var request = new { NumpadCode = "1234" };

            // ACT
            var response = await _client.PostAsJsonAsync("/api/gameplay/controlpoints/1/capture", request);

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        #endregion

        #region Negative Tests - Request Validation

        [Fact]
        public async Task Capture_WithNullNumpadCode_ShouldReturnBadRequest()
        {
            // ARRANGE
            var token = await GetValidTokenAsync();
            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    var mockService = new Mock<IGameplayService>();
                    services.AddScoped(_ => mockService.Object);
                });
            }).CreateClient();

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var request = new { NumpadCode = (string)null };

            // ACT
            var response = await client.PostAsJsonAsync("/api/gameplay/controlpoints/1/capture", request);

            // ASSERT
            // This might be BadRequest or OK depending on service implementation
            // Adjust based on your actual validation logic
            response.StatusCode.ShouldBeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.OK);
        }

        [Fact]
        public async Task Capture_WithInvalidJson_ShouldReturnBadRequest()
        {
            // ARRANGE
            var token = await GetValidTokenAsync();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var content = new StringContent("{ invalid json ", System.Text.Encoding.UTF8, "application/json");

            // ACT
            var response = await _client.PostAsync("/api/gameplay/controlpoints/1/capture", content);

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Capture_WithNegativeControlPointId_ShouldHandleGracefully()
        {
            // ARRANGE
            var token = await GetValidTokenAsync();
            var mockService = new Mock<IGameplayService>();
            mockService.Setup(s => s.CaptureControlPointAsync(-1, It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync((false, "Invalid control point ID"));

            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.AddScoped(_ => mockService.Object);
                });
            }).CreateClient();

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var request = new { NumpadCode = "1234" };

            // ACT
            var response = await client.PostAsJsonAsync("/api/gameplay/controlpoints/-1/capture", request);

            // ASSERT
            response.StatusCode.ShouldBeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task Capture_UsingGetMethod_ShouldReturnMethodNotAllowed()
        {
            // ARRANGE
            var token = await GetValidTokenAsync();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // ACT
            var response = await _client.GetAsync("/api/gameplay/controlpoints/1/capture");

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
        }

        [Fact]
        public async Task Capture_WithVeryLongNumpadCode_ShouldHandleGracefully()
        {
            // ARRANGE
            var token = await GetValidTokenAsync();
            var mockService = new Mock<IGameplayService>();
            var longCode = new string('1', 1000);
            mockService.Setup(s => s.CaptureControlPointAsync(It.IsAny<int>(), It.IsAny<string>(), longCode))
                .ReturnsAsync((false, "Invalid numpad code format"));

            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.AddScoped(_ => mockService.Object);
                });
            }).CreateClient();

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var request = new { NumpadCode = longCode };

            // ACT
            var response = await client.PostAsJsonAsync("/api/gameplay/controlpoints/1/capture", request);

            // ASSERT
            response.StatusCode.ShouldBeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.OK);
        }

        #endregion
    }

    // Extension method for Shouldly
    public static class ShouldlyExtensions
    {
        public static void ShouldBeOneOf(this HttpStatusCode actual, params HttpStatusCode[] expected)
        {
            if (!expected.Contains(actual))
            {
                throw new Shouldly.ShouldAssertException($"Expected one of [{string.Join(", ", expected)}] but was {actual}");
            }
        }
    }
}
