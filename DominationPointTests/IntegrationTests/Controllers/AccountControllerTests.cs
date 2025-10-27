using DominationPoint.Core.Domain;
using DominationPoint.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System.Net;
using System.Text.RegularExpressions;
using Xunit;

namespace DominationPointTests.IntegrationTests.Controllers
{
    [Collection("Integration Tests")]
    public class AccountControllerTests : IClassFixture<CustomWebApplicationFactory<Program>>
    {
        private readonly CustomWebApplicationFactory<Program> _factory;
        private readonly HttpClient _client;

        public AccountControllerTests(CustomWebApplicationFactory<Program> factory)
        {
            _factory = factory;
            _client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false // Important for testing redirects
            });
        }

        #region Helper Methods

        private async Task<(string token, string cookieValue)> GetAntiForgeryTokenAsync(string url)
        {
            var response = await _client.GetAsync(url);

            // Don't throw on redirect - just return empty if we can't get the page
            if (!response.IsSuccessStatusCode)
            {
                return (string.Empty, string.Empty);
            }

            var content = await response.Content.ReadAsStringAsync();

            // Extract antiforgery token from hidden field
            var match = Regex.Match(content, @"<input name=""__RequestVerificationToken"" type=""hidden"" value=""([^""]+)""");
            var token = match.Success ? match.Groups[1].Value : string.Empty;

            // Extract antiforgery cookie
            string? cookie = null;
            if (response.Headers.TryGetValues("Set-Cookie", out var cookies))
            {
                cookie = cookies.FirstOrDefault(c => c.StartsWith(".AspNetCore.Antiforgery"));
            }

            return (token, cookie ?? string.Empty);
        }

        private async Task<HttpResponseMessage> PostLoginWithAntiForgeryAsync(LoginViewModel model)
        {
            var (token, cookie) = await GetAntiForgeryTokenAsync("/Account/Login");

            var request = new HttpRequestMessage(HttpMethod.Post, "/Account/Login");

            if (!string.IsNullOrEmpty(cookie))
            {
                request.Headers.Add("Cookie", cookie);
            }

            var formData = new Dictionary<string, string>
            {
                ["Email"] = model.Email,
                ["Password"] = model.Password,
                ["RememberMe"] = model.RememberMe.ToString(),
                ["__RequestVerificationToken"] = token
            };

            request.Content = new FormUrlEncodedContent(formData);

            return await _client.SendAsync(request);
        }

        private async Task<(HttpClient client, string authCookie)> GetAuthenticatedUserClientAsync(string email = "team.red@test.com", string password = "Password123!")
        {
            // Login
            var model = new LoginViewModel
            {
                Email = email,
                Password = password,
                RememberMe = false
            };

            var loginResponse = await PostLoginWithAntiForgeryAsync(model);

            string? authCookie = null;
            if (loginResponse.Headers.TryGetValues("Set-Cookie", out var cookies))
            {
                authCookie = cookies.FirstOrDefault(c => c.StartsWith(".AspNetCore.Identity"));
            }

            var authenticatedClient = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

            if (!string.IsNullOrEmpty(authCookie))
            {
                authenticatedClient.DefaultRequestHeaders.Add("Cookie", authCookie.Split(';')[0]);
            }

            return (authenticatedClient, authCookie?.Split(';')[0] ?? string.Empty);
        }

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
                .Select(c => c.Contains(';') ? c.Split(';')[0].Trim() : c.Trim())
                .Where(c => !string.IsNullOrEmpty(c));

            return string.Join("; ", validCookies);
        }

        #endregion

        #region GET Login Tests

        [Fact]
        public async Task Login_Get_ShouldReturnSuccessStatusCode()
        {
            // ACT
            var response = await _client.GetAsync("/Account/Login");

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Login_Get_ShouldReturnViewWithLoginForm()
        {
            // ACT
            var response = await _client.GetAsync("/Account/Login");
            var content = await response.Content.ReadAsStringAsync();

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            content.ShouldContain("Use a local account to log in");
            content.ShouldContain("Email");
            content.ShouldContain("Password");
        }

        [Fact]
        public async Task Login_Get_ShouldIncludeAntiForgeryToken()
        {
            // ACT
            var response = await _client.GetAsync("/Account/Login");
            var content = await response.Content.ReadAsStringAsync();

            // ASSERT
            content.ShouldContain("__RequestVerificationToken");
            content.ShouldMatch(@"<input name=""__RequestVerificationToken"" type=""hidden""");
        }

        [Fact]
        public async Task Login_Get_WhenAlreadyAuthenticated_ShouldRedirectToAdmin()
        {
            // ARRANGE - First login
            var loginModel = new LoginViewModel
            {
                Email = "team.red@test.com",
                Password = "Password123!",
                RememberMe = false
            };

            var loginResponse = await PostLoginWithAntiForgeryAsync(loginModel);
            var authCookie = loginResponse.Headers.GetValues("Set-Cookie")
                .FirstOrDefault(c => c.StartsWith(".AspNetCore.Identity"));

            // Create authenticated client
            var authenticatedClient = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

            if (!string.IsNullOrEmpty(authCookie))
            {
                authenticatedClient.DefaultRequestHeaders.Add("Cookie", authCookie);
            }

            // ACT
            var response = await authenticatedClient.GetAsync("/Account/Login");

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
            response.Headers.Location?.ToString().ShouldContain("Admin/ManageGames");
        }

        #endregion

        #region POST Login - Positive Tests

        [Fact]
        public async Task Login_Post_WithValidCredentials_ShouldRedirectToAdmin()
        {
            // ARRANGE
            var model = new LoginViewModel
            {
                Email = "team.red@test.com",
                Password = "Password123!",
                RememberMe = false
            };

            // ACT
            var response = await PostLoginWithAntiForgeryAsync(model);

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
            response.Headers.Location?.ToString().ShouldContain("Admin/ManageGames");
        }

        [Fact]
        public async Task Login_Post_WithValidCredentials_ShouldSetAuthenticationCookie()
        {
            // ARRANGE
            var model = new LoginViewModel
            {
                Email = "team.red@test.com",
                Password = "Password123!",
                RememberMe = false
            };

            // ACT
            var response = await PostLoginWithAntiForgeryAsync(model);

            // ASSERT
            response.Headers.ShouldContain(h => h.Key == "Set-Cookie");
            var cookies = response.Headers.GetValues("Set-Cookie");
            cookies.ShouldContain(c => c.StartsWith(".AspNetCore.Identity"));
        }

        [Fact]
        public async Task Login_Post_WithRememberMe_ShouldSetPersistentCookie()
        {
            // ARRANGE
            var model = new LoginViewModel
            {
                Email = "team.red@test.com",
                Password = "Password123!",
                RememberMe = true
            };

            // ACT
            var response = await PostLoginWithAntiForgeryAsync(model);

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
            var cookies = response.Headers.GetValues("Set-Cookie");
            cookies.ShouldContain(c => c.StartsWith(".AspNetCore.Identity"));
        }

        [Fact]
        public async Task Login_Post_WithValidCredentials_CaseSensitiveEmail_ShouldSucceed()
        {
            // ARRANGE
            var model = new LoginViewModel
            {
                Email = "TEAM.RED@TEST.COM", // Uppercase
                Password = "Password123!",
                RememberMe = false
            };

            // ACT
            var response = await PostLoginWithAntiForgeryAsync(model);

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
            response.Headers.Location?.ToString().ShouldContain("Admin/ManageGames");
        }

        #endregion

        #region POST Login - Negative Tests

        [Fact]
        public async Task Login_Post_WithInvalidPassword_ShouldReturnViewWithError()
        {
            // ARRANGE
            var model = new LoginViewModel
            {
                Email = "team.red@test.com",
                Password = "WrongPassword!",
                RememberMe = false
            };

            // ACT
            var response = await PostLoginWithAntiForgeryAsync(model);
            var content = await response.Content.ReadAsStringAsync();

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            content.ShouldContain("Invalid login attempt");
        }

        [Fact]
        public async Task Login_Post_WithNonExistentUser_ShouldReturnViewWithError()
        {
            // ARRANGE
            var model = new LoginViewModel
            {
                Email = "nonexistent@test.com",
                Password = "Password123!",
                RememberMe = false
            };

            // ACT
            var response = await PostLoginWithAntiForgeryAsync(model);
            var content = await response.Content.ReadAsStringAsync();

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            content.ShouldContain("Invalid login attempt");
        }

        [Fact]
        public async Task Login_Post_WithEmptyEmail_ShouldReturnValidationError()
        {
            // ARRANGE
            var model = new LoginViewModel
            {
                Email = "",
                Password = "Password123!",
                RememberMe = false
            };

            // ACT
            var response = await PostLoginWithAntiForgeryAsync(model);
            var content = await response.Content.ReadAsStringAsync();

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            content.ShouldContain("validation");
        }

        [Fact]
        public async Task Login_Post_WithEmptyPassword_ShouldReturnValidationError()
        {
            // ARRANGE
            var model = new LoginViewModel
            {
                Email = "team.red@test.com",
                Password = "",
                RememberMe = false
            };

            // ACT
            var response = await PostLoginWithAntiForgeryAsync(model);
            var content = await response.Content.ReadAsStringAsync();

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            content.ShouldContain("validation");
        }

        [Fact]
        public async Task Login_Post_WithInvalidEmailFormat_ShouldReturnValidationError()
        {
            // ARRANGE
            var model = new LoginViewModel
            {
                Email = "notanemail",
                Password = "Password123!",
                RememberMe = false
            };

            // ACT
            var response = await PostLoginWithAntiForgeryAsync(model);
            var content = await response.Content.ReadAsStringAsync();

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            content.ShouldContain("validation");
        }

        [Fact]
        public async Task Login_Post_WithoutAntiForgeryToken_ShouldReturnBadRequest()
        {
            // ARRANGE
            var formData = new Dictionary<string, string>
            {
                ["Email"] = "team.red@test.com",
                ["Password"] = "Password123!",
                ["RememberMe"] = "false"
                // Missing __RequestVerificationToken
            };

            // ACT
            var response = await _client.PostAsync("/Account/Login", new FormUrlEncodedContent(formData));

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Login_Post_WithInvalidAntiForgeryToken_ShouldReturnBadRequest()
        {
            // ARRANGE
            var formData = new Dictionary<string, string>
            {
                ["Email"] = "team.red@test.com",
                ["Password"] = "Password123!",
                ["RememberMe"] = "false",
                ["__RequestVerificationToken"] = "invalid-token"
            };

            // ACT
            var response = await _client.PostAsync("/Account/Login", new FormUrlEncodedContent(formData));

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        #endregion

        #region Logout Tests

        [Fact]
        public async Task Logout_Get_ShouldNotBeAllowed()
        {
            // ACT
            var response = await _client.GetAsync("/Account/Logout");

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
        }

        [Fact]
        public async Task Logout_Post_WithoutAntiForgeryToken_ShouldReturnBadRequest()
        {
            // ARRANGE
            var formData = new Dictionary<string, string>();

            // ACT
            var response = await _client.PostAsync("/Account/Logout", new FormUrlEncodedContent(formData));

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Logout_ShouldInvalidateAuthenticationCookie()
        {
            // ARRANGE
            var (client, authCookie) = await GetAuthenticatedUserClientAsync();
            var (token, afCookie) = await GetAntiForgeryTokenFromPage(client, authCookie, "/");

            // ACT - Perform logout
            var logoutRequest = new HttpRequestMessage(HttpMethod.Post, "/Account/Logout");
            logoutRequest.Headers.Add("Cookie", CombineCookies(authCookie, afCookie));
            logoutRequest.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token
            });

            var logoutResponse = await client.SendAsync(logoutRequest);

            // ASSERT - Check that auth cookie is cleared or expired
            if (logoutResponse.Headers.TryGetValues("Set-Cookie", out var cookies))
            {
                var authCookieCleared = cookies.Any(c =>
                    c.Contains(".AspNetCore.Identity") &&
                    (c.Contains("expires=") || c.Contains("max-age=0")));

                authCookieCleared.ShouldBeTrue();
            }
        }

        [Fact]
        public async Task Logout_AfterLogout_ShouldNotAccessProtectedResources()
        {
            // ARRANGE
            var (client, authCookie) = await GetAuthenticatedUserClientAsync();

            // ACT - Logout using token from authenticated session
            var (token, afCookie) = await GetAntiForgeryTokenFromPage(client, authCookie, "/");
            var logoutRequest = new HttpRequestMessage(HttpMethod.Post, "/Account/Logout");
            logoutRequest.Headers.Add("Cookie", CombineCookies(authCookie, afCookie));
            logoutRequest.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token
            });
            await client.SendAsync(logoutRequest);

            // ASSERT - Try to access authenticated resource again
            var afterLogoutRequest = new HttpRequestMessage(HttpMethod.Get, "/Admin/ManageGames");
            afterLogoutRequest.Headers.Add("Cookie", authCookie); // Using old cookie
            var afterLogoutResponse = await client.SendAsync(afterLogoutRequest);

            afterLogoutResponse.StatusCode.ShouldBe(HttpStatusCode.Redirect);
            var location = afterLogoutResponse.Headers.Location?.ToString() ?? string.Empty;
            (location.Contains("Account/Login") || location.Contains("Account/AccessDenied")).ShouldBeTrue();
        }

        [Fact]
        public async Task Logout_ShouldClearAllAuthenticationData()
        {
            // ARRANGE
            var (client, authCookie) = await GetAuthenticatedUserClientAsync();

            // ACT - Logout using token from authenticated session
            var (token, afCookie) = await GetAntiForgeryTokenFromPage(client, authCookie, "/");
            var logoutRequest = new HttpRequestMessage(HttpMethod.Post, "/Account/Logout");
            logoutRequest.Headers.Add("Cookie", CombineCookies(authCookie, afCookie));
            logoutRequest.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token
            });
            await client.SendAsync(logoutRequest);

            // ASSERT - Try to access authenticated resource again
            var afterLogoutRequest = new HttpRequestMessage(HttpMethod.Get, "/Admin/ManageGames");
            afterLogoutRequest.Headers.Add("Cookie", authCookie);
            var afterLogoutResponse = await client.SendAsync(afterLogoutRequest);

            afterLogoutResponse.StatusCode.ShouldBe(HttpStatusCode.Redirect);
            var location = afterLogoutResponse.Headers.Location?.ToString() ?? string.Empty;
            (location.Contains("Account/Login") || location.Contains("Account/AccessDenied")).ShouldBeTrue();
        }

        #endregion
    }
}
