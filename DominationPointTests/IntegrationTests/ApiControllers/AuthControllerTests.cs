using DominationPoint.Core.Domain;
using DominationPoint.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using Xunit;

namespace DominationPointTests.IntegrationTests.ApiControllers
{
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Json;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc.Testing;
    using Shouldly;
    using Xunit;

    public class AuthControllerTests : IClassFixture<CustomWebApplicationFactory<Program>>
    {
        private readonly HttpClient _client;

        public AuthControllerTests(CustomWebApplicationFactory<Program> factory)
        {
            _client = factory.CreateClient();
        }

        private class TokenResponse
        {
            public string token { get; set; }
            public System.DateTime expiration { get; set; }
        }

        #region Positive Scenarios
        [Fact]
        public async Task GetToken_WithValidCredentials_ShouldReturnTokenAndExpiration()
        {
            // ARRANGE
            var request = new
            {
                Email = "team.red@test.com",
                Password = "Password123!"
            };

            // ACT
            var response = await _client.PostAsJsonAsync("/api/auth/token", request);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            var json = await response.Content.ReadAsStringAsync();
            var tokenResponse = JsonConvert.DeserializeObject<TokenResponse>(json);

            // ASSERT
            tokenResponse.ShouldNotBeNull();
            tokenResponse.token.ShouldNotBeNullOrWhiteSpace();
            tokenResponse.expiration.ShouldBeGreaterThan(System.DateTime.UtcNow);
        }

        [Fact]
        public async Task GetToken_WithValidCredentials_ShouldReturnStatusOk()
        {
            // ARRANGE
            var request = new
            {
                Email = "team.red@test.com",
                Password = "Password123!"
            };

            // ACT
            var response = await _client.PostAsJsonAsync("/api/auth/token", request);

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        [Fact]
        public async Task GetToken_Response_ShouldContainTokenAndExpirationProperties()
        {
            // ARRANGE
            var request = new
            {
                Email = "team.red@test.com",
                Password = "Password123!"
            };

            // ACT
            var response = await _client.PostAsJsonAsync("/api/auth/token", request);
            var json = await response.Content.ReadAsStringAsync();
            var tokenResponse = JsonConvert.DeserializeObject<TokenResponse>(json);

            // ASSERT
            tokenResponse.ShouldNotBeNull();
            tokenResponse.token.ShouldNotBeNullOrWhiteSpace();
            tokenResponse.expiration.ShouldBeGreaterThan(System.DateTime.UtcNow);
        }

        [Fact]
        public async Task GetToken_TokenExpiration_ShouldBeInFuture()
        {
            // ARRANGE
            var request = new
            {
                Email = "team.red@test.com",
                Password = "Password123!"
            };

            // ACT
            var response = await _client.PostAsJsonAsync("/api/auth/token", request);
            var json = await response.Content.ReadAsStringAsync();
            var tokenResponse = JsonConvert.DeserializeObject<TokenResponse>(json);

            // ASSERT
            tokenResponse.expiration.ShouldBeGreaterThan(System.DateTime.UtcNow);
            tokenResponse.expiration.ShouldBeLessThan(System.DateTime.UtcNow.AddHours(4)); // Token should expire within 4 hours
        }

        [Fact]
        public async Task GetToken_TokenFormat_ShouldBeValidJwtStructure()
        {
            // ARRANGE
            var request = new
            {
                Email = "team.red@test.com",
                Password = "Password123!"
            };

            // ACT
            var response = await _client.PostAsJsonAsync("/api/auth/token", request);
            var json = await response.Content.ReadAsStringAsync();
            var tokenResponse = JsonConvert.DeserializeObject<TokenResponse>(json);

            // ASSERT
            tokenResponse.token.ShouldNotBeNullOrWhiteSpace();
            // JWT tokens have 3 parts separated by dots: header.payload.signature
            var parts = tokenResponse.token.Split('.');
            parts.Length.ShouldBe(3, "A valid JWT should have exactly 3 parts");
        }
        #endregion

        #region Negative Scenarios
        [Fact]
        public async Task GetToken_WithInvalidPassword_ShouldReturnUnauthorized()
        {
            // ARRANGE
            var request = new
            {
                Email = "team.red@test.com",
                Password = "WrongPassword!"
            };

            // ACT
            var response = await _client.PostAsJsonAsync("/api/auth/token", request);

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task GetToken_WithNonExistentUser_ShouldReturnUnauthorized()
        {
            // ARRANGE
            var request = new
            {
                Email = "ghost.user@test.com",
                Password = "Password123!"
            };

            // ACT
            var response = await _client.PostAsJsonAsync("/api/auth/token", request);

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task GetToken_WithEmptyPassword_ShouldReturnUnauthorized()
        {
            // ARRANGE
            var request = new
            {
                Email = "team.red@test.com",
                Password = ""
            };

            // ACT
            var response = await _client.PostAsJsonAsync("/api/auth/token", request);

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task GetToken_WithEmptyEmail_ShouldReturnUnauthorized()
        {
            // ARRANGE
            var request = new
            {
                Email = "",
                Password = "Password123!"
            };

            // ACT
            var response = await _client.PostAsJsonAsync("/api/auth/token", request);

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task GetToken_WithMalformedJson_ShouldReturnBadRequest()
        {
            // ARRANGE
            var content = new StringContent("{ \"email\": \"team.red@test.com\", ", System.Text.Encoding.UTF8, "application/json");

            // ACT
            var response = await _client.PostAsync("/api/auth/token", content);

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task GetToken_UsingGetMethod_ShouldReturnMethodNotAllowed()
        {
            // ARRANGE & ACT
            var response = await _client.GetAsync("/api/auth/token");

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
        }

        [Fact]
        public async Task GetToken_WithNullRequest_ShouldReturnBadRequest()
        {
            // ARRANGE
            var content = new StringContent("null", System.Text.Encoding.UTF8, "application/json");

            // ACT
            var response = await _client.PostAsync("/api/auth/token", content);

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task GetToken_WithCaseSensitiveEmail_ShouldReturnToken()
        {
            // ARRANGE - Test email with different casing
            var request = new
            {
                Email = "TEAM.RED@TEST.COM",  // Uppercase version
                Password = "Password123!"
            };

            // ACT
            var response = await _client.PostAsJsonAsync("/api/auth/token", request);

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            var json = await response.Content.ReadAsStringAsync();
            var tokenResponse = JsonConvert.DeserializeObject<TokenResponse>(json);
            tokenResponse.token.ShouldNotBeNullOrWhiteSpace();
        }

        [Fact]
        public async Task GetToken_WithWhitespaceInEmail_ShouldReturnUnauthorized()
        {
            // ARRANGE
            var request = new
            {
                Email = " team.red@test.com ",  // Email with leading/trailing whitespace
                Password = "Password123!"
            };

            // ACT
            var response = await _client.PostAsJsonAsync("/api/auth/token", request);

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task GetToken_WithSpecialCharactersInPassword_ShouldHandleCorrectly()
        {
            // ARRANGE
            var request = new
            {
                Email = "team.red@test.com",
                Password = "P@$$w0rd!#%&"  // Password with special characters
            };

            // ACT
            var response = await _client.PostAsJsonAsync("/api/auth/token", request);

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized); // Should fail because this is not the correct password
        }
        #endregion
    }
}
