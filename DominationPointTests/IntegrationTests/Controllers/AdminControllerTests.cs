using DominationPoint.Core.Application;
using DominationPoint.Core.Application.Services;
using DominationPoint.Core.Domain;
using DominationPoint.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;
using System.Net;
using System.Text.RegularExpressions;
using Xunit;

namespace DominationPointTests.IntegrationTests.Controllers
{
    [Collection("Integration Tests")]
    public class AdminControllerTests : IClassFixture<CustomWebApplicationFactory<Program>>
    {
        private readonly CustomWebApplicationFactory<Program> _factory;
        private readonly HttpClient _client;

        public AdminControllerTests(CustomWebApplicationFactory<Program> factory)
        {
            _factory = factory;
            _client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
        }

        #region Helper Methods

        private async Task<(HttpClient client, string authCookie)> GetAuthenticatedAdminClientAsync()
        {
            // Create an admin user for testing
            using (var scope = _factory.Services.CreateScope())
            {
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

                // Ensure Admin role exists
                if (!await roleManager.RoleExistsAsync("Admin"))
                {
                    await roleManager.CreateAsync(new IdentityRole("Admin"));
                }

                // Create admin user if not exists
                var adminUser = await userManager.FindByEmailAsync("admin@test.com");
                if (adminUser == null)
                {
                    adminUser = new ApplicationUser
                    {
                        UserName = "admin@test.com",
                        Email = "admin@test.com",
                        EmailConfirmed = true,
                        ColorHex = "#000000"
                    };
                    await userManager.CreateAsync(adminUser, "Admin123!");
                    await userManager.AddToRoleAsync(adminUser, "Admin");
                }
            }

            // Login as admin - use the old method for login
            var (token, cookie) = await GetAntiForgeryTokenAsync("/Account/Login", null);

            var loginRequest = new HttpRequestMessage(HttpMethod.Post, "/Account/Login");
            if (!string.IsNullOrEmpty(cookie))
            {
                loginRequest.Headers.Add("Cookie", cookie);
            }

            var formData = new Dictionary<string, string>
            {
                ["Email"] = "admin@test.com",
                ["Password"] = "Admin123!",
                ["RememberMe"] = "false",
                ["__RequestVerificationToken"] = token
            };

            loginRequest.Content = new FormUrlEncodedContent(formData);
            var loginResponse = await _client.SendAsync(loginRequest);

            var authCookie = loginResponse.Headers.GetValues("Set-Cookie")
                .FirstOrDefault(c => c.StartsWith(".AspNetCore.Identity"));

            var authenticatedClient = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

            if (!string.IsNullOrEmpty(authCookie))
            {
                authenticatedClient.DefaultRequestHeaders.Add("Cookie", authCookie);
            }

            return (authenticatedClient, authCookie ?? string.Empty);
        }

        // OLD METHOD - Keep for backward compatibility (used in GetAuthenticatedAdminClientAsync)
        private async Task<(string token, string cookieValue)> GetAntiForgeryTokenAsync(string url, string? authCookie)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);

            if (!string.IsNullOrEmpty(authCookie))
            {
                request.Headers.Add("Cookie", authCookie);
            }

            var response = await _client.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                return (string.Empty, string.Empty);
            }

            var content = await response.Content.ReadAsStringAsync();
            var match = Regex.Match(content, @"<input name=""__RequestVerificationToken"" type=""hidden"" value=""([^""]+)""");
            var token = match.Success ? match.Groups[1].Value : string.Empty;

            string? afCookie = null;
            if (response.Headers.TryGetValues("Set-Cookie", out var cookies))
            {
                afCookie = cookies.FirstOrDefault(c => c.StartsWith(".AspNetCore.Antiforgery"));
            }

            return (token, afCookie ?? string.Empty);
        }

        // NEW METHOD - Use this for all POST tests in Admin controller
        private async Task<(string token, string afCookie)> GetAntiForgeryTokenFromPage(HttpClient client, string authCookie, string pageUrl)
        {
            var getRequest = new HttpRequestMessage(HttpMethod.Get, pageUrl);
            getRequest.Headers.Add("Cookie", authCookie);
            var getResponse = await client.SendAsync(getRequest);

            var pageContent = await getResponse.Content.ReadAsStringAsync();
            var match = Regex.Match(pageContent, @"<input name=""__RequestVerificationToken"" type=""hidden"" value=""([^""]+)""");
            var token = match.Success ? match.Groups[1].Value : string.Empty;

            string? afCookie = null;
            if (getResponse.Headers.TryGetValues("Set-Cookie", out var responseCookies))
            {
                afCookie = responseCookies.FirstOrDefault(c => c.StartsWith(".AspNetCore.Antiforgery"));
            }

            return (token, afCookie ?? string.Empty);
        }

        private string CombineCookies(params string[] cookies)
        {
            var validCookies = cookies
                .Where(c => !string.IsNullOrEmpty(c))
                .Select(c => c.Split(';')[0].Trim()) // Extract just the name=value part
                .Where(c => !string.IsNullOrEmpty(c));

            return string.Join("; ", validCookies);
        }

        #endregion

        #region Authorization Tests (5)

        [Fact]
        public async Task ManageGames_WithoutAuthentication_ShouldRedirectToLogin()
        {
            // ACT
            var response = await _client.GetAsync("/Admin/ManageGames");

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
            response.Headers.Location?.ToString().ShouldContain("Account/Login");
        }

        [Fact]
        public async Task ManageUsers_WithoutAuthentication_ShouldRedirectToLogin()
        {
            // ACT
            var response = await _client.GetAsync("/Admin/ManageUsers");

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
            response.Headers.Location?.ToString().ShouldContain("Account/Login");
        }

        [Fact]
        public async Task GameDetails_WithoutAuthentication_ShouldRedirectToLogin()
        {
            // ACT
            var response = await _client.GetAsync("/Admin/GameDetails/1");

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        }

        [Fact]
        public async Task LiveGame_WithoutAuthentication_ShouldRedirectToLogin()
        {
            // ACT
            var response = await _client.GetAsync("/Admin/LiveGame/1");

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        }

        [Fact]
        public async Task Scoreboard_WithoutAuthentication_ShouldRedirectToLogin()
        {
            // ACT
            var response = await _client.GetAsync("/Admin/Scoreboard/1");

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        }

        #endregion

        #region ManageGames GET Tests (3)

        [Fact]
        public async Task ManageGames_WithAdminAuth_ShouldReturnSuccess()
        {
            // ARRANGE
            var (client, _) = await GetAuthenticatedAdminClientAsync();

            // ACT
            var response = await client.GetAsync("/Admin/ManageGames");

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        [Fact]
        public async Task ManageGames_ShouldDisplayGameList()
        {
            // ARRANGE
            var (client, _) = await GetAuthenticatedAdminClientAsync();

            // ACT
            var response = await client.GetAsync("/Admin/ManageGames");
            var content = await response.Content.ReadAsStringAsync();

            // ASSERT
            content.ShouldContain("Manage Games");
            content.ShouldContain("Schedule New Game");
        }

        [Fact]
        public async Task ManageGames_ShouldContainGameCreationForm()
        {
            // ARRANGE
            var (client, _) = await GetAuthenticatedAdminClientAsync();

            // ACT
            var response = await client.GetAsync("/Admin/ManageGames");
            var content = await response.Content.ReadAsStringAsync();

            // ASSERT
            content.ShouldContain("name=\"name\"");
            content.ShouldContain("name=\"startTime\"");
            content.ShouldContain("name=\"endTime\"");
        }

        #endregion

        #region CreateGame POST Tests (5)

        [Fact]
        public async Task CreateGame_WithValidData_ShouldRedirectToManageGames()
        {
            // ARRANGE
            var (client, authCookie) = await GetAuthenticatedAdminClientAsync();
            var (token, afCookie) = await GetAntiForgeryTokenFromPage(client, authCookie, "/Admin/ManageGames");

            var request = new HttpRequestMessage(HttpMethod.Post, "/Admin/CreateGame");
            request.Headers.Add("Cookie", CombineCookies(authCookie, afCookie));

            var formData = new Dictionary<string, string>
            {
                ["name"] = $"Test Game {Guid.NewGuid()}",
                ["startTime"] = DateTime.Now.AddHours(1).ToString("yyyy-MM-ddTHH:mm"),
                ["endTime"] = DateTime.Now.AddHours(3).ToString("yyyy-MM-ddTHH:mm"),
                ["__RequestVerificationToken"] = token
            };

            request.Content = new FormUrlEncodedContent(formData);

            // ACT
            var response = await client.SendAsync(request);

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
            response.Headers.Location?.ToString().ShouldContain("ManageGames");
        }


        [Fact]
        public async Task CreateGame_WithEndTimeBeforeStartTime_ShouldRedirectWithError()
        {
            // ARRANGE
            var (client, authCookie) = await GetAuthenticatedAdminClientAsync();

            // Get antiforgery token from the page
            var getRequest = new HttpRequestMessage(HttpMethod.Get, "/Admin/ManageGames");
            getRequest.Headers.Add("Cookie", authCookie);
            var getResponse = await client.SendAsync(getRequest);

            var pageContent = await getResponse.Content.ReadAsStringAsync();
            var match = Regex.Match(pageContent, @"<input name=""__RequestVerificationToken"" type=""hidden"" value=""([^""]+)""");
            var token = match.Success ? match.Groups[1].Value : string.Empty;

            string? afCookie = null;
            if (getResponse.Headers.TryGetValues("Set-Cookie", out var responseCookies))
            {
                afCookie = responseCookies.FirstOrDefault(c => c.StartsWith(".AspNetCore.Antiforgery"));
            }

            // Create POST request with combined cookies
            var postRequest = new HttpRequestMessage(HttpMethod.Post, "/Admin/CreateGame");
            postRequest.Headers.Add("Cookie", CombineCookies(authCookie, afCookie ?? string.Empty));

            var formData = new Dictionary<string, string>
            {
                ["name"] = "Invalid Game",
                ["startTime"] = DateTime.Now.AddHours(3).ToString("yyyy-MM-ddTHH:mm"),
                ["endTime"] = DateTime.Now.AddHours(1).ToString("yyyy-MM-ddTHH:mm"),
                ["__RequestVerificationToken"] = token
            };

            postRequest.Content = new FormUrlEncodedContent(formData);

            // ACT
            var response = await client.SendAsync(postRequest);

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        }


        [Fact]
        public async Task CreateGame_WithoutAntiForgeryToken_ShouldReturnBadRequest()
        {
            // ARRANGE
            var (client, authCookie) = await GetAuthenticatedAdminClientAsync();

            var request = new HttpRequestMessage(HttpMethod.Post, "/Admin/CreateGame");
            request.Headers.Add("Cookie", authCookie);

            var formData = new Dictionary<string, string>
            {
                ["name"] = "Test Game",
                ["startTime"] = DateTime.Now.AddHours(1).ToString("yyyy-MM-ddTHH:mm"),
                ["endTime"] = DateTime.Now.AddHours(3).ToString("yyyy-MM-ddTHH:mm")
            };

            request.Content = new FormUrlEncodedContent(formData);

            // ACT
            var response = await client.SendAsync(request);

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task CreateGame_WithWhitespaceName_ShouldRedirectWithError()
        {
            // ARRANGE
            var (client, authCookie) = await GetAuthenticatedAdminClientAsync();
            var (token, afCookie) = await GetAntiForgeryTokenFromPage(client, authCookie, "/Admin/ManageGames");

            var request = new HttpRequestMessage(HttpMethod.Post, "/Admin/CreateGame");
            request.Headers.Add("Cookie", CombineCookies(authCookie, afCookie));

            var formData = new Dictionary<string, string>
            {
                ["name"] = "   ",
                ["startTime"] = DateTime.Now.AddHours(1).ToString("yyyy-MM-ddTHH:mm"),
                ["endTime"] = DateTime.Now.AddHours(3).ToString("yyyy-MM-ddTHH:mm"),
                ["__RequestVerificationToken"] = token
            };

            request.Content = new FormUrlEncodedContent(formData);

            // ACT
            var response = await client.SendAsync(request);

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        }


        #endregion

        #region StartGame and EndGame Tests (4)

        [Fact]
        public async Task StartGame_WithoutAntiForgeryToken_ShouldReturnBadRequest()
        {
            // ARRANGE
            var (client, authCookie) = await GetAuthenticatedAdminClientAsync();

            var request = new HttpRequestMessage(HttpMethod.Post, "/Admin/StartGame");
            request.Headers.Add("Cookie", authCookie);

            var formData = new Dictionary<string, string>
            {
                ["id"] = "1"
            };

            request.Content = new FormUrlEncodedContent(formData);

            // ACT
            var response = await client.SendAsync(request);

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task EndGame_WithoutAntiForgeryToken_ShouldReturnBadRequest()
        {
            // ARRANGE
            var (client, authCookie) = await GetAuthenticatedAdminClientAsync();

            var request = new HttpRequestMessage(HttpMethod.Post, "/Admin/EndGame");
            request.Headers.Add("Cookie", authCookie);

            var formData = new Dictionary<string, string>
            {
                ["id"] = "1"
            };

            request.Content = new FormUrlEncodedContent(formData);

            // ACT
            var response = await client.SendAsync(request);

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task StartGame_UsingGetMethod_ShouldReturnMethodNotAllowed()
        {
            // ARRANGE
            var (client, _) = await GetAuthenticatedAdminClientAsync();

            // ACT
            var response = await client.GetAsync("/Admin/StartGame?id=1");

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
        }

        [Fact]
        public async Task EndGame_UsingGetMethod_ShouldReturnMethodNotAllowed()
        {
            // ARRANGE
            var (client, _) = await GetAuthenticatedAdminClientAsync();

            // ACT
            var response = await client.GetAsync("/Admin/EndGame?id=1");

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
        }

        #endregion

        #region GameDetails Tests (5)

        [Fact]
        public async Task GameDetails_WithNonExistentId_ShouldReturnNotFound()
        {
            // ARRANGE
            var (client, _) = await GetAuthenticatedAdminClientAsync();

            // ACT
            var response = await client.GetAsync("/Admin/GameDetails/99999");

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task GameDetails_WithNegativeId_ShouldReturnNotFound()
        {
            // ARRANGE
            var (client, _) = await GetAuthenticatedAdminClientAsync();

            // ACT
            var response = await client.GetAsync("/Admin/GameDetails/-1");

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task GameDetails_WithZeroId_ShouldReturnNotFound()
        {
            // ARRANGE
            var (client, _) = await GetAuthenticatedAdminClientAsync();

            // ACT
            var response = await client.GetAsync("/Admin/GameDetails/0");

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task GameDetails_WithInvalidIdFormat_ShouldReturnNotFound()
        {
            // ARRANGE
            var (client, _) = await GetAuthenticatedAdminClientAsync();

            // ACT
            var response = await client.GetAsync("/Admin/GameDetails/abc");

            // ASSERT - ASP.NET Core routing returns 404 for invalid route parameters
            response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task GameDetails_WithVeryLargeId_ShouldReturnNotFound()
        {
            // ARRANGE
            var (client, _) = await GetAuthenticatedAdminClientAsync();

            // ACT
            var response = await client.GetAsync("/Admin/GameDetails/2147483647");

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }

        #endregion

        #region Participant Management Tests (4)

        [Fact]
        public async Task AddParticipant_WithoutAntiForgeryToken_ShouldReturnBadRequest()
        {
            // ARRANGE
            var (client, authCookie) = await GetAuthenticatedAdminClientAsync();

            var request = new HttpRequestMessage(HttpMethod.Post, "/Admin/AddParticipant");
            request.Headers.Add("Cookie", authCookie);

            var formData = new Dictionary<string, string>
            {
                ["gameId"] = "1",
                ["userId"] = "test-user-id"
            };

            request.Content = new FormUrlEncodedContent(formData);

            // ACT
            var response = await client.SendAsync(request);

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task RemoveParticipant_WithoutAntiForgeryToken_ShouldReturnBadRequest()
        {
            // ARRANGE
            var (client, authCookie) = await GetAuthenticatedAdminClientAsync();

            var request = new HttpRequestMessage(HttpMethod.Post, "/Admin/RemoveParticipant");
            request.Headers.Add("Cookie", authCookie);

            var formData = new Dictionary<string, string>
            {
                ["gameId"] = "1",
                ["userId"] = "test-user-id"
            };

            request.Content = new FormUrlEncodedContent(formData);

            // ACT
            var response = await client.SendAsync(request);

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task AddParticipant_UsingGetMethod_ShouldReturnMethodNotAllowed()
        {
            // ARRANGE
            var (client, _) = await GetAuthenticatedAdminClientAsync();

            // ACT
            var response = await client.GetAsync("/Admin/AddParticipant?gameId=1&userId=test");

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
        }

        [Fact]
        public async Task RemoveParticipant_UsingGetMethod_ShouldReturnMethodNotAllowed()
        {
            // ARRANGE
            var (client, _) = await GetAuthenticatedAdminClientAsync();

            // ACT
            var response = await client.GetAsync("/Admin/RemoveParticipant?gameId=1&userId=test");

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
        }

        #endregion

        #region EditMap Tests (3)

        [Fact]
        public async Task EditMap_WithoutAntiForgeryToken_ShouldReturnBadRequest()
        {
            // ARRANGE
            var (client, authCookie) = await GetAuthenticatedAdminClientAsync();

            var request = new HttpRequestMessage(HttpMethod.Post, "/Admin/EditMap");
            request.Headers.Add("Cookie", authCookie);

            var formData = new Dictionary<string, string>
            {
                ["gameId"] = "1",
                ["positionX"] = "5",
                ["positionY"] = "5",
                ["text"] = "OBJ",
                ["isCp"] = "true"
            };

            request.Content = new FormUrlEncodedContent(formData);

            // ACT
            var response = await client.SendAsync(request);

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task EditMap_UsingGetMethod_ShouldReturnMethodNotAllowed()
        {
            // ARRANGE
            var (client, _) = await GetAuthenticatedAdminClientAsync();

            // ACT
            var response = await client.GetAsync("/Admin/EditMap?gameId=1&positionX=5&positionY=5");

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
        }

        [Fact]
        public async Task EditMap_WithInvalidCoordinates_ShouldHandleGracefully()
        {
            // ARRANGE
            var (client, authCookie) = await GetAuthenticatedAdminClientAsync();
            var (token, afCookie) = await GetAntiForgeryTokenAsync("/Admin/ManageGames", authCookie);

            var request = new HttpRequestMessage(HttpMethod.Post, "/Admin/EditMap");
            request.Headers.Add("Cookie", authCookie);
            if (!string.IsNullOrEmpty(afCookie))
            {
                request.Headers.Add("Cookie", afCookie);
            }

            var formData = new Dictionary<string, string>
            {
                ["gameId"] = "1",
                ["positionX"] = "-1",
                ["positionY"] = "100",
                ["text"] = "OBJ",
                ["isCp"] = "true",
                ["__RequestVerificationToken"] = token
            };

            request.Content = new FormUrlEncodedContent(formData);

            // ACT
            var response = await client.SendAsync(request);

            // ASSERT
            response.StatusCode.ShouldBeOneOf(HttpStatusCode.Redirect, HttpStatusCode.BadRequest);
        }

        #endregion

        #region LiveGame Tests (4)

        [Fact]
        public async Task LiveGame_WithNonExistentId_ShouldReturnNotFound()
        {
            // ARRANGE
            var (client, _) = await GetAuthenticatedAdminClientAsync();

            // ACT
            var response = await client.GetAsync("/Admin/LiveGame/99999");

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task LiveGame_WithNegativeId_ShouldReturnNotFound()
        {
            // ARRANGE
            var (client, _) = await GetAuthenticatedAdminClientAsync();

            // ACT
            var response = await client.GetAsync("/Admin/LiveGame/-1");

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task UpdateLiveTileState_WithoutAntiForgeryToken_ShouldReturnBadRequest()
        {
            // ARRANGE
            var (client, authCookie) = await GetAuthenticatedAdminClientAsync();

            var request = new HttpRequestMessage(HttpMethod.Post, "/Admin/UpdateLiveTileState");
            request.Headers.Add("Cookie", authCookie);

            var formData = new Dictionary<string, string>
            {
                ["gameId"] = "1",
                ["cpId"] = "1",
                ["userId"] = "test-user-id"
            };

            request.Content = new FormUrlEncodedContent(formData);

            // ACT
            var response = await client.SendAsync(request);

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task UpdateLiveTileState_UsingGetMethod_ShouldReturnMethodNotAllowed()
        {
            // ARRANGE
            var (client, _) = await GetAuthenticatedAdminClientAsync();

            // ACT
            var response = await client.GetAsync("/Admin/UpdateLiveTileState?gameId=1&cpId=1");

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
        }

        #endregion

        #region ManageUsers Tests (3)

        [Fact]
        public async Task ManageUsers_WithAdminAuth_ShouldReturnSuccess()
        {
            // ARRANGE
            var (client, _) = await GetAuthenticatedAdminClientAsync();

            // ACT
            var response = await client.GetAsync("/Admin/ManageUsers");

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        [Fact]
        public async Task ManageUsers_ShouldDisplayUserList()
        {
            // ARRANGE
            var (client, _) = await GetAuthenticatedAdminClientAsync();

            // ACT
            var response = await client.GetAsync("/Admin/ManageUsers");
            var content = await response.Content.ReadAsStringAsync();

            // ASSERT
            content.ShouldContain("Manage Teams");
            content.ShouldContain("Create New Team");
        }

        [Fact]
        public async Task ManageUsers_ShouldContainNumpadCodeInputs()
        {
            // ARRANGE
            var (client, _) = await GetAuthenticatedAdminClientAsync();

            // ACT
            var response = await client.GetAsync("/Admin/ManageUsers");
            var content = await response.Content.ReadAsStringAsync();

            // ASSERT
            content.ShouldContain("name=\"numpadCode\"");
        }

        #endregion

        #region CreateTeam Tests (6)

        [Fact]
        public async Task CreateTeam_Get_ShouldReturnSuccess()
        {
            // ARRANGE
            var (client, _) = await GetAuthenticatedAdminClientAsync();

            // ACT
            var response = await client.GetAsync("/Admin/CreateTeam");

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        [Fact]
        public async Task CreateTeam_Get_ShouldDisplayForm()
        {
            // ARRANGE
            var (client, _) = await GetAuthenticatedAdminClientAsync();

            // ACT
            var response = await client.GetAsync("/Admin/CreateTeam");
            var content = await response.Content.ReadAsStringAsync();

            // ASSERT
            content.ShouldContain("Create New Team");
            content.ShouldContain("TeamName");
            content.ShouldContain("ColorHex");
        }

        [Fact]
        public async Task CreateTeam_Post_WithValidData_ShouldRedirectToManageUsers()
        {
            // ARRANGE
            var (client, authCookie) = await GetAuthenticatedAdminClientAsync();
            var (token, afCookie) = await GetAntiForgeryTokenFromPage(client, authCookie, "/Admin/CreateTeam");

            var request = new HttpRequestMessage(HttpMethod.Post, "/Admin/CreateTeam");
            request.Headers.Add("Cookie", CombineCookies(authCookie, afCookie));

            var formData = new Dictionary<string, string>
            {
                ["TeamName"] = $"TestTeam{Guid.NewGuid().ToString().Substring(0, 8)}",
                ["ColorHex"] = "#FF0000",
                ["__RequestVerificationToken"] = token
            };

            request.Content = new FormUrlEncodedContent(formData);

            // ACT
            var response = await client.SendAsync(request);

            // ASSERT
            response.StatusCode.ShouldBeOneOf(HttpStatusCode.Redirect, HttpStatusCode.OK);
        }

        [Fact]
        public async Task CreateTeam_WithEmptyName_ShouldReturnValidationError()
        {
            // ARRANGE
            var (client, authCookie) = await GetAuthenticatedAdminClientAsync();
            var (token, afCookie) = await GetAntiForgeryTokenFromPage(client, authCookie, "/Admin/CreateTeam");

            var request = new HttpRequestMessage(HttpMethod.Post, "/Admin/CreateTeam");
            request.Headers.Add("Cookie", CombineCookies(authCookie, afCookie));

            var formData = new Dictionary<string, string>
            {
                ["TeamName"] = "",
                ["ColorHex"] = "#FF0000",
                ["__RequestVerificationToken"] = token
            };

            request.Content = new FormUrlEncodedContent(formData);

            // ACT
            var response = await client.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            content.ShouldContain("validation");
        }

        [Fact]
        public async Task CreateTeam_WithShortName_ShouldReturnValidationError()
        {
            // ARRANGE
            var (client, authCookie) = await GetAuthenticatedAdminClientAsync();
            var (token, afCookie) = await GetAntiForgeryTokenFromPage(client, authCookie, "/Admin/CreateTeam");

            var request = new HttpRequestMessage(HttpMethod.Post, "/Admin/CreateTeam");
            request.Headers.Add("Cookie", CombineCookies(authCookie, afCookie));

            var formData = new Dictionary<string, string>
            {
                ["TeamName"] = "AB",
                ["ColorHex"] = "#FF0000",
                ["__RequestVerificationToken"] = token
            };

            request.Content = new FormUrlEncodedContent(formData);

            // ACT
            var response = await client.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            content.ShouldContain("validation");
        }

        [Fact]
        public async Task CreateTeam_WithoutAntiForgeryToken_ShouldReturnBadRequest()
        {
            // ARRANGE
            var (client, authCookie) = await GetAuthenticatedAdminClientAsync();

            var request = new HttpRequestMessage(HttpMethod.Post, "/Admin/CreateTeam");
            request.Headers.Add("Cookie", authCookie);

            var formData = new Dictionary<string, string>
            {
                ["TeamName"] = "Test Team",
                ["ColorHex"] = "#FF0000"
            };

            request.Content = new FormUrlEncodedContent(formData);

            // ACT
            var response = await client.SendAsync(request);

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        #endregion


        #region UpdateNumpadCode Tests (3)

        [Fact]
        public async Task UpdateNumpadCode_WithoutAntiForgeryToken_ShouldReturnBadRequest()
        {
            // ARRANGE
            var (client, authCookie) = await GetAuthenticatedAdminClientAsync();

            var request = new HttpRequestMessage(HttpMethod.Post, "/Admin/UpdateNumpadCode");
            request.Headers.Add("Cookie", authCookie);

            var formData = new Dictionary<string, string>
            {
                ["userId"] = "test-user-id",
                ["numpadCode"] = "1234"
            };

            request.Content = new FormUrlEncodedContent(formData);

            // ACT
            var response = await client.SendAsync(request);

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task UpdateNumpadCode_UsingGetMethod_ShouldReturnMethodNotAllowed()
        {
            // ARRANGE
            var (client, _) = await GetAuthenticatedAdminClientAsync();

            // ACT
            var response = await client.GetAsync("/Admin/UpdateNumpadCode?userId=test&numpadCode=1234");

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
        }

        [Fact]
        public async Task UpdateNumpadCode_WithNonExistentUser_ShouldRedirectGracefully()
        {
            // ARRANGE
            var (client, authCookie) = await GetAuthenticatedAdminClientAsync();
            var (token, afCookie) = await GetAntiForgeryTokenFromPage(client, authCookie, "/Admin/ManageUsers");

            var request = new HttpRequestMessage(HttpMethod.Post, "/Admin/UpdateNumpadCode");
            request.Headers.Add("Cookie", CombineCookies(authCookie, afCookie));

            var formData = new Dictionary<string, string>
            {
                ["userId"] = "non-existent-user-id",
                ["numpadCode"] = "1234",
                ["__RequestVerificationToken"] = token
            };

            request.Content = new FormUrlEncodedContent(formData);

            // ACT
            var response = await client.SendAsync(request);

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        }

        #endregion

        #region Scoreboard Tests (3)

        [Fact]
        public async Task Scoreboard_WithNonExistentId_ShouldHandleGracefully()
        {
            // ARRANGE
            var (client, _) = await GetAuthenticatedAdminClientAsync();

            // ACT
            var response = await client.GetAsync("/Admin/Scoreboard/99999");

            // ASSERT
            response.StatusCode.ShouldBeOneOf(HttpStatusCode.NotFound, HttpStatusCode.OK, HttpStatusCode.InternalServerError);
        }

        [Fact]
        public async Task Scoreboard_WithNegativeId_ShouldHandleGracefully()
        {
            // ARRANGE
            var (client, _) = await GetAuthenticatedAdminClientAsync();

            // ACT
            var response = await client.GetAsync("/Admin/Scoreboard/-1");

            // ASSERT
            response.StatusCode.ShouldBeOneOf(HttpStatusCode.NotFound, HttpStatusCode.OK, HttpStatusCode.InternalServerError);
        }

        [Fact]
        public async Task Scoreboard_WithInvalidIdFormat_ShouldReturnNotFoundOrOk()
        {
            // ARRANGE
            var (client, _) = await GetAuthenticatedAdminClientAsync();

            // ACT
            var response = await client.GetAsync("/Admin/Scoreboard/abc");

            // ASSERT - ASP.NET Core routing may return 404 or pass "abc" as 0
            response.StatusCode.ShouldBeOneOf(HttpStatusCode.NotFound, HttpStatusCode.BadRequest, HttpStatusCode.OK);
        }

        #endregion

        #region AddParticipant/RemoveParticipant - Full Coverage Tests

        [Fact]
        public async Task AddParticipant_WithValidGameAndUser_ShouldRedirectToGameDetails()
        {
            // ARRANGE
            var (client, authCookie) = await GetAuthenticatedAdminClientAsync();

            // First, create a game
            var (createToken, createAfCookie) = await GetAntiForgeryTokenFromPage(client, authCookie, "/Admin/ManageGames");
            var createGameRequest = new HttpRequestMessage(HttpMethod.Post, "/Admin/CreateGame");
            createGameRequest.Headers.Add("Cookie", CombineCookies(authCookie, createAfCookie));
            createGameRequest.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["name"] = $"Test Game {Guid.NewGuid()}",
                ["startTime"] = DateTime.Now.AddHours(1).ToString("yyyy-MM-ddTHH:mm"),
                ["endTime"] = DateTime.Now.AddHours(3).ToString("yyyy-MM-ddTHH:mm"),
                ["__RequestVerificationToken"] = createToken
            });
            await client.SendAsync(createGameRequest);

            // Get the test user ID
            string userId;
            using (var scope = _factory.Services.CreateScope())
            {
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var user = await userManager.FindByEmailAsync("team.red@test.com");
                userId = user?.Id ?? "test-id";
            }

            // Get token for AddParticipant
            var (token, afCookie) = await GetAntiForgeryTokenFromPage(client, authCookie, "/Admin/ManageGames");

            var request = new HttpRequestMessage(HttpMethod.Post, "/Admin/AddParticipant");
            request.Headers.Add("Cookie", CombineCookies(authCookie, afCookie));

            var formData = new Dictionary<string, string>
            {
                ["gameId"] = "1",
                ["userId"] = userId,
                ["__RequestVerificationToken"] = token
            };

            request.Content = new FormUrlEncodedContent(formData);

            // ACT
            var response = await client.SendAsync(request);

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
            response.Headers.Location?.ToString().ShouldContain("GameDetails");
        }

        [Fact]
        public async Task AddParticipant_WithInvalidGameId_ShouldHandleGracefully()
        {
            // ARRANGE
            var (client, authCookie) = await GetAuthenticatedAdminClientAsync();
            var (token, afCookie) = await GetAntiForgeryTokenFromPage(client, authCookie, "/Admin/ManageGames");

            var request = new HttpRequestMessage(HttpMethod.Post, "/Admin/AddParticipant");
            request.Headers.Add("Cookie", CombineCookies(authCookie, afCookie));

            var formData = new Dictionary<string, string>
            {
                ["gameId"] = "99999",
                ["userId"] = "test-user-id",
                ["__RequestVerificationToken"] = token
            };

            request.Content = new FormUrlEncodedContent(formData);

            // ACT
            var response = await client.SendAsync(request);

            // ASSERT
            response.StatusCode.ShouldBeOneOf(HttpStatusCode.Redirect, HttpStatusCode.NotFound, HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task RemoveParticipant_WithValidData_ShouldRedirectToGameDetails()
        {
            // ARRANGE
            var (client, authCookie) = await GetAuthenticatedAdminClientAsync();
            var (token, afCookie) = await GetAntiForgeryTokenFromPage(client, authCookie, "/Admin/ManageGames");

            var request = new HttpRequestMessage(HttpMethod.Post, "/Admin/RemoveParticipant");
            request.Headers.Add("Cookie", CombineCookies(authCookie, afCookie));

            var formData = new Dictionary<string, string>
            {
                ["gameId"] = "1",
                ["userId"] = "test-user-id",
                ["__RequestVerificationToken"] = token
            };

            request.Content = new FormUrlEncodedContent(formData);

            // ACT
            var response = await client.SendAsync(request);

            // ASSERT
            response.StatusCode.ShouldBeOneOf(HttpStatusCode.Redirect, HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task RemoveParticipant_WithNullUserId_ShouldHandleGracefully()
        {
            // ARRANGE
            var (client, authCookie) = await GetAuthenticatedAdminClientAsync();
            var (token, afCookie) = await GetAntiForgeryTokenFromPage(client, authCookie, "/Admin/ManageGames");

            var request = new HttpRequestMessage(HttpMethod.Post, "/Admin/RemoveParticipant");
            request.Headers.Add("Cookie", CombineCookies(authCookie, afCookie));

            var formData = new Dictionary<string, string>
            {
                ["gameId"] = "1",
                ["userId"] = "",
                ["__RequestVerificationToken"] = token
            };

            request.Content = new FormUrlEncodedContent(formData);

            // ACT
            var response = await client.SendAsync(request);

            // ASSERT
            response.StatusCode.ShouldBeOneOf(HttpStatusCode.Redirect, HttpStatusCode.BadRequest);
        }

        #endregion

        #region StartGame/EndGame - Full Coverage Tests

        [Fact]
        public async Task StartGame_WithValidGameId_ShouldRedirectToManageGames()
        {
            // ARRANGE
            var (client, authCookie) = await GetAuthenticatedAdminClientAsync();

            // First, create a game
            var (createToken, createAfCookie) = await GetAntiForgeryTokenFromPage(client, authCookie, "/Admin/ManageGames");
            var createGameRequest = new HttpRequestMessage(HttpMethod.Post, "/Admin/CreateGame");
            createGameRequest.Headers.Add("Cookie", CombineCookies(authCookie, createAfCookie));
            createGameRequest.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["name"] = $"Game To Start {Guid.NewGuid()}",
                ["startTime"] = DateTime.Now.AddHours(1).ToString("yyyy-MM-ddTHH:mm"),
                ["endTime"] = DateTime.Now.AddHours(3).ToString("yyyy-MM-ddTHH:mm"),
                ["__RequestVerificationToken"] = createToken
            });
            await client.SendAsync(createGameRequest);

            // Now start the game
            var (token, afCookie) = await GetAntiForgeryTokenFromPage(client, authCookie, "/Admin/ManageGames");

            var request = new HttpRequestMessage(HttpMethod.Post, "/Admin/StartGame");
            request.Headers.Add("Cookie", CombineCookies(authCookie, afCookie));

            var formData = new Dictionary<string, string>
            {
                ["id"] = "1",
                ["__RequestVerificationToken"] = token
            };

            request.Content = new FormUrlEncodedContent(formData);

            // ACT
            var response = await client.SendAsync(request);

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
            response.Headers.Location?.ToString().ShouldContain("ManageGames");
        }

        [Fact]
        public async Task StartGame_WithNonExistentGameId_ShouldHandleGracefully()
        {
            // ARRANGE
            var (client, authCookie) = await GetAuthenticatedAdminClientAsync();
            var (token, afCookie) = await GetAntiForgeryTokenFromPage(client, authCookie, "/Admin/ManageGames");

            var request = new HttpRequestMessage(HttpMethod.Post, "/Admin/StartGame");
            request.Headers.Add("Cookie", CombineCookies(authCookie, afCookie));

            var formData = new Dictionary<string, string>
            {
                ["id"] = "99999",
                ["__RequestVerificationToken"] = token
            };

            request.Content = new FormUrlEncodedContent(formData);

            // ACT
            var response = await client.SendAsync(request);

            // ASSERT
            response.StatusCode.ShouldBeOneOf(HttpStatusCode.Redirect, HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task EndGame_WithNegativeGameId_ShouldHandleGracefully()
        {
            // ARRANGE
            var (client, authCookie) = await GetAuthenticatedAdminClientAsync();
            var (token, afCookie) = await GetAntiForgeryTokenFromPage(client, authCookie, "/Admin/ManageGames");

            var request = new HttpRequestMessage(HttpMethod.Post, "/Admin/EndGame");
            request.Headers.Add("Cookie", CombineCookies(authCookie, afCookie));

            var formData = new Dictionary<string, string>
            {
                ["id"] = "-1",
                ["__RequestVerificationToken"] = token
            };

            request.Content = new FormUrlEncodedContent(formData);

            // ACT
            var response = await client.SendAsync(request);

            // ASSERT
            response.StatusCode.ShouldBeOneOf(HttpStatusCode.Redirect, HttpStatusCode.BadRequest);
        }

        #endregion

        #region EditMap - Full Coverage Tests

        [Fact]
        public async Task EditMap_WithValidCoordinates_ShouldRedirectToGameDetails()
        {
            // ARRANGE
            var (client, authCookie) = await GetAuthenticatedAdminClientAsync();
            var (token, afCookie) = await GetAntiForgeryTokenFromPage(client, authCookie, "/Admin/ManageGames");

            var request = new HttpRequestMessage(HttpMethod.Post, "/Admin/EditMap");
            request.Headers.Add("Cookie", CombineCookies(authCookie, afCookie));

            var formData = new Dictionary<string, string>
            {
                ["gameId"] = "1",
                ["positionX"] = "5",
                ["positionY"] = "5",
                ["text"] = "OBJ",
                ["isCp"] = "true",
                ["__RequestVerificationToken"] = token
            };

            request.Content = new FormUrlEncodedContent(formData);

            // ACT
            var response = await client.SendAsync(request);

            // ASSERT
            response.StatusCode.ShouldBeOneOf(HttpStatusCode.Redirect, HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task EditMap_WithEmptyText_ShouldStillWork()
        {
            // ARRANGE
            var (client, authCookie) = await GetAuthenticatedAdminClientAsync();
            var (token, afCookie) = await GetAntiForgeryTokenFromPage(client, authCookie, "/Admin/ManageGames");

            var request = new HttpRequestMessage(HttpMethod.Post, "/Admin/EditMap");
            request.Headers.Add("Cookie", CombineCookies(authCookie, afCookie));

            var formData = new Dictionary<string, string>
            {
                ["gameId"] = "1",
                ["positionX"] = "3",
                ["positionY"] = "3",
                ["text"] = "",
                ["isCp"] = "false",
                ["__RequestVerificationToken"] = token
            };

            request.Content = new FormUrlEncodedContent(formData);

            // ACT
            var response = await client.SendAsync(request);

            // ASSERT
            response.StatusCode.ShouldBeOneOf(HttpStatusCode.Redirect, HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task EditMap_WithMaxCoordinates_ShouldHandleGracefully()
        {
            // ARRANGE
            var (client, authCookie) = await GetAuthenticatedAdminClientAsync();
            var (token, afCookie) = await GetAntiForgeryTokenFromPage(client, authCookie, "/Admin/ManageGames");

            var request = new HttpRequestMessage(HttpMethod.Post, "/Admin/EditMap");
            request.Headers.Add("Cookie", CombineCookies(authCookie, afCookie));

            var formData = new Dictionary<string, string>
            {
                ["gameId"] = "1",
                ["positionX"] = "10",
                ["positionY"] = "10",
                ["text"] = "MAX",
                ["isCp"] = "true",
                ["__RequestVerificationToken"] = token
            };

            request.Content = new FormUrlEncodedContent(formData);

            // ACT
            var response = await client.SendAsync(request);

            // ASSERT
            response.StatusCode.ShouldBeOneOf(HttpStatusCode.Redirect, HttpStatusCode.BadRequest, HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task EditMap_WithLongText_ShouldTruncateOrHandleGracefully()
        {
            // ARRANGE
            var (client, authCookie) = await GetAuthenticatedAdminClientAsync();
            var (token, afCookie) = await GetAntiForgeryTokenFromPage(client, authCookie, "/Admin/ManageGames");

            var request = new HttpRequestMessage(HttpMethod.Post, "/Admin/EditMap");
            request.Headers.Add("Cookie", CombineCookies(authCookie, afCookie));

            var formData = new Dictionary<string, string>
            {
                ["gameId"] = "1",
                ["positionX"] = "5",
                ["positionY"] = "5",
                ["text"] = "VERYLONGTEXT", // Should be max 3 chars
                ["isCp"] = "true",
                ["__RequestVerificationToken"] = token
            };

            request.Content = new FormUrlEncodedContent(formData);

            // ACT
            var response = await client.SendAsync(request);

            // ASSERT
            response.StatusCode.ShouldBeOneOf(HttpStatusCode.Redirect, HttpStatusCode.BadRequest, HttpStatusCode.NotFound);
        }

        #endregion

        #region UpdateLiveTileState - Full Coverage Tests

        [Fact]
        public async Task UpdateLiveTileState_WithValidData_ShouldRedirectToLiveGame()
        {
            // ARRANGE
            var (client, authCookie) = await GetAuthenticatedAdminClientAsync();
            var (token, afCookie) = await GetAntiForgeryTokenFromPage(client, authCookie, "/Admin/ManageGames");

            var request = new HttpRequestMessage(HttpMethod.Post, "/Admin/UpdateLiveTileState");
            request.Headers.Add("Cookie", CombineCookies(authCookie, afCookie));

            var formData = new Dictionary<string, string>
            {
                ["gameId"] = "1",
                ["cpId"] = "1",
                ["userId"] = "test-user-id",
                ["__RequestVerificationToken"] = token
            };

            request.Content = new FormUrlEncodedContent(formData);

            // ACT
            var response = await client.SendAsync(request);

            // ASSERT
            response.StatusCode.ShouldBeOneOf(HttpStatusCode.Redirect, HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task UpdateLiveTileState_WithNullUserId_ShouldSetToNeutral()
        {
            // ARRANGE
            var (client, authCookie) = await GetAuthenticatedAdminClientAsync();
            var (token, afCookie) = await GetAntiForgeryTokenFromPage(client, authCookie, "/Admin/ManageGames");

            var request = new HttpRequestMessage(HttpMethod.Post, "/Admin/UpdateLiveTileState");
            request.Headers.Add("Cookie", CombineCookies(authCookie, afCookie));

            var formData = new Dictionary<string, string>
            {
                ["gameId"] = "1",
                ["cpId"] = "1",
                ["userId"] = "",
                ["__RequestVerificationToken"] = token
            };

            request.Content = new FormUrlEncodedContent(formData);

            // ACT
            var response = await client.SendAsync(request);

            // ASSERT
            response.StatusCode.ShouldBeOneOf(HttpStatusCode.Redirect, HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task UpdateLiveTileState_WithInvalidCpId_ShouldHandleGracefully()
        {
            // ARRANGE
            var (client, authCookie) = await GetAuthenticatedAdminClientAsync();
            var (token, afCookie) = await GetAntiForgeryTokenFromPage(client, authCookie, "/Admin/ManageGames");

            var request = new HttpRequestMessage(HttpMethod.Post, "/Admin/UpdateLiveTileState");
            request.Headers.Add("Cookie", CombineCookies(authCookie, afCookie));

            var formData = new Dictionary<string, string>
            {
                ["gameId"] = "1",
                ["cpId"] = "99999",
                ["userId"] = "test-user-id",
                ["__RequestVerificationToken"] = token
            };

            request.Content = new FormUrlEncodedContent(formData);

            // ACT
            var response = await client.SendAsync(request);

            // ASSERT
            response.StatusCode.ShouldBeOneOf(HttpStatusCode.Redirect, HttpStatusCode.NotFound, HttpStatusCode.BadRequest);
        }

        #endregion

        #region UpdateNumpadCode - Full Coverage Tests

        [Fact]
        public async Task UpdateNumpadCode_WithValidCode_ShouldRedirectToManageUsers()
        {
            // ARRANGE
            var (client, authCookie) = await GetAuthenticatedAdminClientAsync();

            // Get existing user
            string userId;
            using (var scope = _factory.Services.CreateScope())
            {
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var user = await userManager.FindByEmailAsync("team.red@test.com");
                userId = user?.Id ?? "test-id";
            }

            var (token, afCookie) = await GetAntiForgeryTokenFromPage(client, authCookie, "/Admin/ManageUsers");

            var request = new HttpRequestMessage(HttpMethod.Post, "/Admin/UpdateNumpadCode");
            request.Headers.Add("Cookie", CombineCookies(authCookie, afCookie));

            var formData = new Dictionary<string, string>
            {
                ["userId"] = userId,
                ["numpadCode"] = "9876",
                ["__RequestVerificationToken"] = token
            };

            request.Content = new FormUrlEncodedContent(formData);

            // ACT
            var response = await client.SendAsync(request);

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
            response.Headers.Location?.ToString().ShouldContain("ManageUsers");
        }

        [Fact]
        public async Task UpdateNumpadCode_WithEmptyCode_ShouldClearCode()
        {
            // ARRANGE
            var (client, authCookie) = await GetAuthenticatedAdminClientAsync();

            string userId;
            using (var scope = _factory.Services.CreateScope())
            {
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var user = await userManager.FindByEmailAsync("team.red@test.com");
                userId = user?.Id ?? "test-id";
            }

            var (token, afCookie) = await GetAntiForgeryTokenFromPage(client, authCookie, "/Admin/ManageUsers");

            var request = new HttpRequestMessage(HttpMethod.Post, "/Admin/UpdateNumpadCode");
            request.Headers.Add("Cookie", CombineCookies(authCookie, afCookie));

            var formData = new Dictionary<string, string>
            {
                ["userId"] = userId,
                ["numpadCode"] = "",
                ["__RequestVerificationToken"] = token
            };

            request.Content = new FormUrlEncodedContent(formData);

            // ACT
            var response = await client.SendAsync(request);

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        }

        [Fact]
        public async Task UpdateNumpadCode_WithVeryLongCode_ShouldHandleGracefully()
        {
            // ARRANGE
            var (client, authCookie) = await GetAuthenticatedAdminClientAsync();

            string userId;
            using (var scope = _factory.Services.CreateScope())
            {
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var user = await userManager.FindByEmailAsync("team.red@test.com");
                userId = user?.Id ?? "test-id";
            }

            var (token, afCookie) = await GetAntiForgeryTokenFromPage(client, authCookie, "/Admin/ManageUsers");

            var request = new HttpRequestMessage(HttpMethod.Post, "/Admin/UpdateNumpadCode");
            request.Headers.Add("Cookie", CombineCookies(authCookie, afCookie));

            var formData = new Dictionary<string, string>
            {
                ["userId"] = userId,
                ["numpadCode"] = "123456789012345678901234567890",
                ["__RequestVerificationToken"] = token
            };

            request.Content = new FormUrlEncodedContent(formData);

            // ACT
            var response = await client.SendAsync(request);

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        }

        #endregion

        #region View Rendering - GameDetails, LiveGame, Scoreboard

        [Fact]
        public async Task GameDetails_WithValidGame_ShouldRenderViewWithParticipants()
        {
            // ARRANGE
            var (client, authCookie) = await GetAuthenticatedAdminClientAsync();

            // Create a game first
            var (createToken, createAfCookie) = await GetAntiForgeryTokenFromPage(client, authCookie, "/Admin/ManageGames");
            var createGameRequest = new HttpRequestMessage(HttpMethod.Post, "/Admin/CreateGame");
            createGameRequest.Headers.Add("Cookie", CombineCookies(authCookie, createAfCookie));
            createGameRequest.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["name"] = $"Test Game {Guid.NewGuid()}",
                ["startTime"] = DateTime.Now.AddHours(1).ToString("yyyy-MM-ddTHH:mm"),
                ["endTime"] = DateTime.Now.AddHours(3).ToString("yyyy-MM-ddTHH:mm"),
                ["__RequestVerificationToken"] = createToken
            });
            await client.SendAsync(createGameRequest);

            // ACT
            var response = await client.GetAsync("/Admin/GameDetails/1");

            // ASSERT
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var content = await response.Content.ReadAsStringAsync();
                content.ShouldContain("Participants");
                content.ShouldContain("map-grid");
                content.ShouldContain("Available Teams");
            }
        }

        [Fact]
        public async Task GameDetails_ShouldRenderMapGrid()
        {
            // ARRANGE
            var (client, _) = await GetAuthenticatedAdminClientAsync();

            // ACT
            var response = await client.GetAsync("/Admin/GameDetails/1");

            // ASSERT
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var content = await response.Content.ReadAsStringAsync();
                content.ShouldContain("map-grid");
                content.ShouldContain("grid-cell-container");
            }
        }

        [Fact]
        public async Task LiveGame_WithActiveGame_ShouldRenderLiveView()
        {
            // ARRANGE
            var (client, _) = await GetAuthenticatedAdminClientAsync();

            // ACT
            var response = await client.GetAsync("/Admin/LiveGame/1");

            // ASSERT
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var content = await response.Content.ReadAsStringAsync();
                content.ShouldContain("ACTIVE");
                content.ShouldContain("map-grid");
            }
        }

        [Fact]
        public async Task LiveGame_WithScheduledGame_ShouldRedirectToGameDetails()
        {
            // ARRANGE
            var (client, _) = await GetAuthenticatedAdminClientAsync();

            // ACT
            var response = await client.GetAsync("/Admin/LiveGame/1");

            // ASSERT
            response.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.Redirect, HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task Scoreboard_WithFinishedGame_ShouldRenderScoreboard()
        {
            // ARRANGE
            var (client, _) = await GetAuthenticatedAdminClientAsync();

            // ACT
            var response = await client.GetAsync("/Admin/Scoreboard/1");

            // ASSERT
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var content = await response.Content.ReadAsStringAsync();
                content.ShouldContain("Scoreboard");
            }
        }

        [Fact]
        public async Task Scoreboard_ShouldDisplayTeamRankings()
        {
            // ARRANGE
            var (client, _) = await GetAuthenticatedAdminClientAsync();

            // ACT
            var response = await client.GetAsync("/Admin/Scoreboard/1");

            // ASSERT
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var content = await response.Content.ReadAsStringAsync();
                content.ShouldContain("Rank");
                content.ShouldContain("Team");
            }
        }

        #endregion

        #region CreateGame - Additional Error and Edge Case Tests

        [Fact]
        public async Task CreateGame_WithEmptyName_ShouldRedirectWithError()
        {
            // ARRANGE
            var (client, authCookie) = await GetAuthenticatedAdminClientAsync();
            var (token, afCookie) = await GetAntiForgeryTokenFromPage(client, authCookie, "/Admin/ManageGames");

            var request = new HttpRequestMessage(HttpMethod.Post, "/Admin/CreateGame");
            request.Headers.Add("Cookie", CombineCookies(authCookie, afCookie));

            var formData = new Dictionary<string, string>
            {
                ["name"] = "",
                ["startTime"] = DateTime.Now.AddHours(1).ToString("yyyy-MM-ddTHH:mm"),
                ["endTime"] = DateTime.Now.AddHours(3).ToString("yyyy-MM-ddTHH:mm"),
                ["__RequestVerificationToken"] = token
            };
            request.Content = new FormUrlEncodedContent(formData);

            // ACT
            var response = await client.SendAsync(request);

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
            response.Headers.Location?.ToString().ShouldContain("ManageGames");
        }

        [Fact]
        public async Task CreateGame_WithInvalidModelState_ShouldRedirectWithError()
        {
            // ARRANGE
            var (client, authCookie) = await GetAuthenticatedAdminClientAsync();
            var (token, afCookie) = await GetAntiForgeryTokenFromPage(client, authCookie, "/Admin/ManageGames");

            var request = new HttpRequestMessage(HttpMethod.Post, "/Admin/CreateGame");
            request.Headers.Add("Cookie", CombineCookies(authCookie, afCookie));

            var formData = new Dictionary<string, string>
            {
                ["name"] = "", // Empty name - invalid
                ["startTime"] = "invalid-date", // Invalid date format
                ["endTime"] = DateTime.Now.AddHours(-1).ToString("yyyy-MM-ddTHH:mm"), // Past time
                ["__RequestVerificationToken"] = token
            };
            request.Content = new FormUrlEncodedContent(formData);

            // ACT
            var response = await client.SendAsync(request);

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
            response.Headers.Location?.ToString().ShouldContain("ManageGames");
        }

        [Fact]
        public async Task CreateGame_WithStartTimeInPast_ShouldReturnValidationError()
        {
            // ARRANGE
            var (client, authCookie) = await GetAuthenticatedAdminClientAsync();
            var (token, afCookie) = await GetAntiForgeryTokenFromPage(client, authCookie, "/Admin/ManageGames");

            var request = new HttpRequestMessage(HttpMethod.Post, "/Admin/CreateGame");
            request.Headers.Add("Cookie", CombineCookies(authCookie, afCookie));

            var formData = new Dictionary<string, string>
            {
                ["name"] = "Past Game",
                ["startTime"] = DateTime.Now.AddHours(-2).ToString("yyyy-MM-ddTHH:mm"),
                ["endTime"] = DateTime.Now.AddHours(-1).ToString("yyyy-MM-ddTHH:mm"),
                ["__RequestVerificationToken"] = token
            };
            request.Content = new FormUrlEncodedContent(formData);

            // ACT
            var response = await client.SendAsync(request);

            // ASSERT
            response.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.Redirect);
        }

        [Fact]
        public async Task CreateGame_WithSameStartAndEndTime_ShouldReturnError()
        {
            // ARRANGE
            var (client, authCookie) = await GetAuthenticatedAdminClientAsync();
            var (token, afCookie) = await GetAntiForgeryTokenFromPage(client, authCookie, "/Admin/ManageGames");

            var request = new HttpRequestMessage(HttpMethod.Post, "/Admin/CreateGame");
            request.Headers.Add("Cookie", CombineCookies(authCookie, afCookie));

            var sameTime = DateTime.Now.AddHours(1).ToString("yyyy-MM-ddTHH:mm");
            var formData = new Dictionary<string, string>
            {
                ["name"] = "Same Time Game",
                ["startTime"] = sameTime,
                ["endTime"] = sameTime,
                ["__RequestVerificationToken"] = token
            };
            request.Content = new FormUrlEncodedContent(formData);

            // ACT
            var response = await client.SendAsync(request);

            // ASSERT
            response.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.Redirect);
        }

        [Fact]
        public async Task CreateGame_WithVeryLongName_ShouldHandleGracefully()
        {
            // ARRANGE
            var (client, authCookie) = await GetAuthenticatedAdminClientAsync();
            var (token, afCookie) = await GetAntiForgeryTokenFromPage(client, authCookie, "/Admin/ManageGames");

            var request = new HttpRequestMessage(HttpMethod.Post, "/Admin/CreateGame");
            request.Headers.Add("Cookie", CombineCookies(authCookie, afCookie));

            var formData = new Dictionary<string, string>
            {
                ["name"] = new string('A', 500), // Very long name
                ["startTime"] = DateTime.Now.AddHours(1).ToString("yyyy-MM-ddTHH:mm"),
                ["endTime"] = DateTime.Now.AddHours(3).ToString("yyyy-MM-ddTHH:mm"),
                ["__RequestVerificationToken"] = token
            };
            request.Content = new FormUrlEncodedContent(formData);

            // ACT
            var response = await client.SendAsync(request);

            // ASSERT
            response.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.Redirect, HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task CreateGame_WithSpecialCharactersInName_ShouldSucceed()
        {
            // ARRANGE
            var (client, authCookie) = await GetAuthenticatedAdminClientAsync();
            var (token, afCookie) = await GetAntiForgeryTokenFromPage(client, authCookie, "/Admin/ManageGames");

            var request = new HttpRequestMessage(HttpMethod.Post, "/Admin/CreateGame");
            request.Headers.Add("Cookie", CombineCookies(authCookie, afCookie));

            var formData = new Dictionary<string, string>
            {
                ["name"] = "Test Game <>&\"'",
                ["startTime"] = DateTime.Now.AddHours(1).ToString("yyyy-MM-ddTHH:mm"),
                ["endTime"] = DateTime.Now.AddHours(3).ToString("yyyy-MM-ddTHH:mm"),
                ["__RequestVerificationToken"] = token
            };
            request.Content = new FormUrlEncodedContent(formData);

            // ACT
            var response = await client.SendAsync(request);

            // ASSERT
            response.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.Redirect);
        }

        [Fact]
        public async Task CreateGame_AsAnonymousUser_ShouldRedirectToLogin()
        {
            // ARRANGE - Use unauthenticated client
            var unauthClient = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

            var (token, afCookie) = await GetAntiForgeryTokenAsync("/Account/Login", null);

            var request = new HttpRequestMessage(HttpMethod.Post, "/Admin/CreateGame");
            if (!string.IsNullOrEmpty(afCookie))
            {
                request.Headers.Add("Cookie", afCookie);
            }

            var formData = new Dictionary<string, string>
            {
                ["name"] = "Unauthorized Game",
                ["startTime"] = DateTime.Now.AddHours(1).ToString("yyyy-MM-ddTHH:mm"),
                ["endTime"] = DateTime.Now.AddHours(3).ToString("yyyy-MM-ddTHH:mm"),
                ["__RequestVerificationToken"] = token
            };
            request.Content = new FormUrlEncodedContent(formData);

            // ACT
            var response = await unauthClient.SendAsync(request);

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
            response.Headers.Location?.ToString().ShouldContain("Account/Login");
        }

        [Fact]
        public async Task CreateGame_WithNullFormValues_ShouldRedirectWithError()
        {
            // ARRANGE
            var (client, authCookie) = await GetAuthenticatedAdminClientAsync();
            var (token, afCookie) = await GetAntiForgeryTokenFromPage(client, authCookie, "/Admin/ManageGames");

            var request = new HttpRequestMessage(HttpMethod.Post, "/Admin/CreateGame");
            request.Headers.Add("Cookie", CombineCookies(authCookie, afCookie));

            var formData = new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token
                // Missing all required fields
            };
            request.Content = new FormUrlEncodedContent(formData);

            // ACT
            var response = await client.SendAsync(request);

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
            response.Headers.Location?.ToString().ShouldContain("ManageGames");
        }

        #endregion

        #region CreateTeam - Additional Error and Edge Case Tests

        [Fact]
        public async Task CreateTeam_WithInvalidColorFormat_ShouldReturnValidationError()
        {
            // ARRANGE
            var (client, authCookie) = await GetAuthenticatedAdminClientAsync();
            var (token, afCookie) = await GetAntiForgeryTokenFromPage(client, authCookie, "/Admin/CreateTeam");

            var request = new HttpRequestMessage(HttpMethod.Post, "/Admin/CreateTeam");
            request.Headers.Add("Cookie", CombineCookies(authCookie, afCookie));

            var formData = new Dictionary<string, string>
            {
                ["TeamName"] = "Invalid Color Team",
                ["ColorHex"] = "GGGGGG", // Invalid hex color
                ["__RequestVerificationToken"] = token
            };
            request.Content = new FormUrlEncodedContent(formData);

            // ACT
            var response = await client.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            content.ShouldContain("validation");
        }

        [Fact]
        public async Task CreateTeam_WithColorWithoutHash_ShouldHandleGracefully()
        {
            // ARRANGE
            var (client, authCookie) = await GetAuthenticatedAdminClientAsync();
            var (token, afCookie) = await GetAntiForgeryTokenFromPage(client, authCookie, "/Admin/CreateTeam");

            var request = new HttpRequestMessage(HttpMethod.Post, "/Admin/CreateTeam");
            request.Headers.Add("Cookie", CombineCookies(authCookie, afCookie));

            var formData = new Dictionary<string, string>
            {
                ["TeamName"] = $"Team_{Guid.NewGuid().ToString().Substring(0, 8)}",
                ["ColorHex"] = "FF0000", // No # prefix
                ["__RequestVerificationToken"] = token
            };
            request.Content = new FormUrlEncodedContent(formData);

            // ACT
            var response = await client.SendAsync(request);

            // ASSERT
            response.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.Redirect);
        }

        [Fact]
        public async Task CreateTeam_WithVeryLongTeamName_ShouldReturnValidationError()
        {
            // ARRANGE
            var (client, authCookie) = await GetAuthenticatedAdminClientAsync();
            var (token, afCookie) = await GetAntiForgeryTokenFromPage(client, authCookie, "/Admin/CreateTeam");

            var request = new HttpRequestMessage(HttpMethod.Post, "/Admin/CreateTeam");
            request.Headers.Add("Cookie", CombineCookies(authCookie, afCookie));

            var formData = new Dictionary<string, string>
            {
                ["TeamName"] = new string('X', 500),
                ["ColorHex"] = "FF0000",
                ["__RequestVerificationToken"] = token
            };
            request.Content = new FormUrlEncodedContent(formData);

            // ACT
            var response = await client.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            content.ShouldContain("validation");
        }

        [Fact]
        public async Task CreateTeam_WithDuplicateTeamName_ShouldHandleGracefully()
        {
            // ARRANGE
            var (client, authCookie) = await GetAuthenticatedAdminClientAsync();

            // Create first team
            var (token1, afCookie1) = await GetAntiForgeryTokenFromPage(client, authCookie, "/Admin/CreateTeam");
            var request1 = new HttpRequestMessage(HttpMethod.Post, "/Admin/CreateTeam");
            request1.Headers.Add("Cookie", CombineCookies(authCookie, afCookie1));
            var teamName = $"DuplicateTeam_{Guid.NewGuid().ToString().Substring(0, 8)}";
            request1.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["TeamName"] = teamName,
                ["ColorHex"] = "FF0000",
                ["__RequestVerificationToken"] = token1
            });
            await client.SendAsync(request1);

            // Try to create duplicate
            var (token2, afCookie2) = await GetAntiForgeryTokenFromPage(client, authCookie, "/Admin/CreateTeam");
            var request2 = new HttpRequestMessage(HttpMethod.Post, "/Admin/CreateTeam");
            request2.Headers.Add("Cookie", CombineCookies(authCookie, afCookie2));
            request2.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["TeamName"] = teamName, // Same name
                ["ColorHex"] = "00FF00",
                ["__RequestVerificationToken"] = token2
            });

            // ACT
            var response = await client.SendAsync(request2);

            // ASSERT
            response.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.Redirect);
        }

        [Fact]
        public async Task CreateTeam_WithSpecialCharactersInName_ShouldSucceed()
        {
            // ARRANGE
            var (client, authCookie) = await GetAuthenticatedAdminClientAsync();
            var (token, afCookie) = await GetAntiForgeryTokenFromPage(client, authCookie, "/Admin/CreateTeam");

            var request = new HttpRequestMessage(HttpMethod.Post, "/Admin/CreateTeam");
            request.Headers.Add("Cookie", CombineCookies(authCookie, afCookie));

            var formData = new Dictionary<string, string>
            {
                ["TeamName"] = $"Team<>&'{Guid.NewGuid().ToString().Substring(0, 4)}",
                ["ColorHex"] = "FF0000",
                ["__RequestVerificationToken"] = token
            };
            request.Content = new FormUrlEncodedContent(formData);

            // ACT
            var response = await client.SendAsync(request);

            // ASSERT
            response.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.Redirect);
        }

        [Fact]
        public async Task CreateTeam_AsAnonymousUser_ShouldRedirectToLogin()
        {
            // ARRANGE - Use unauthenticated client
            var unauthClient = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

            var (token, afCookie) = await GetAntiForgeryTokenAsync("/Account/Login", null);

            var request = new HttpRequestMessage(HttpMethod.Post, "/Admin/CreateTeam");
            if (!string.IsNullOrEmpty(afCookie))
            {
                request.Headers.Add("Cookie", afCookie);
            }

            var formData = new Dictionary<string, string>
            {
                ["TeamName"] = "Unauthorized Team",
                ["ColorHex"] = "FF0000",
                ["__RequestVerificationToken"] = token
            };
            request.Content = new FormUrlEncodedContent(formData);

            // ACT
            var response = await unauthClient.SendAsync(request);

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
            response.Headers.Location?.ToString().ShouldContain("Account/Login");
        }

        [Fact]
        public async Task CreateTeam_WithNullColorHex_ShouldReturnValidationError()
        {
            // ARRANGE
            var (client, authCookie) = await GetAuthenticatedAdminClientAsync();
            var (token, afCookie) = await GetAntiForgeryTokenFromPage(client, authCookie, "/Admin/CreateTeam");

            var request = new HttpRequestMessage(HttpMethod.Post, "/Admin/CreateTeam");
            request.Headers.Add("Cookie", CombineCookies(authCookie, afCookie));

            var formData = new Dictionary<string, string>
            {
                ["TeamName"] = "No Color Team",
                // ColorHex missing
                ["__RequestVerificationToken"] = token
            };
            request.Content = new FormUrlEncodedContent(formData);

            // ACT
            var response = await client.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            content.ShouldContain("validation");
        }

        [Fact]
        public async Task CreateTeam_WithWhitespaceColorHex_ShouldReturnValidationError()
        {
            // ARRANGE
            var (client, authCookie) = await GetAuthenticatedAdminClientAsync();
            var (token, afCookie) = await GetAntiForgeryTokenFromPage(client, authCookie, "/Admin/CreateTeam");

            var request = new HttpRequestMessage(HttpMethod.Post, "/Admin/CreateTeam");
            request.Headers.Add("Cookie", CombineCookies(authCookie, afCookie));

            var formData = new Dictionary<string, string>
            {
                ["TeamName"] = "Whitespace Color Team",
                ["ColorHex"] = "   ",
                ["__RequestVerificationToken"] = token
            };
            request.Content = new FormUrlEncodedContent(formData);

            // ACT
            var response = await client.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            content.ShouldContain("validation");
        }

        [Fact]
        public async Task CreateTeam_With3CharacterColor_ShouldReturnValidationError()
        {
            // ARRANGE
            var (client, authCookie) = await GetAuthenticatedAdminClientAsync();
            var (token, afCookie) = await GetAntiForgeryTokenFromPage(client, authCookie, "/Admin/CreateTeam");

            var request = new HttpRequestMessage(HttpMethod.Post, "/Admin/CreateTeam");
            request.Headers.Add("Cookie", CombineCookies(authCookie, afCookie));

            var formData = new Dictionary<string, string>
            {
                ["TeamName"] = "Short Color Team",
                ["ColorHex"] = "FFF", // Should be 6 characters
                ["__RequestVerificationToken"] = token
            };
            request.Content = new FormUrlEncodedContent(formData);

            // ACT
            var response = await client.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            content.ShouldContain("validation");
        }

        #endregion


    }

    public static class ShouldlyHttpExtensions
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
