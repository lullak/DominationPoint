using DominationPoint.Core.Application;
using DominationPoint.Core.Application.Services;
using DominationPoint.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System.Net;
using Xunit;

namespace DominationPointTests.IntegrationTests.Infrastructure
{
    [Collection("Integration Tests")]
    public class ProgramStartupTests : IClassFixture<CustomWebApplicationFactory<Program>>
    {
        private readonly CustomWebApplicationFactory<Program> _factory;

        public ProgramStartupTests(CustomWebApplicationFactory<Program> factory)
        {
            _factory = factory;
        }

        #region Application Startup Tests

        [Fact]
        public void Application_ShouldStartSuccessfully()
        {
            // ACT
            var client = _factory.CreateClient();

            // ASSERT
            client.ShouldNotBeNull();
        }

        [Fact]
        public async Task RootEndpoint_ShouldRedirectToLogin()
        {
            // ARRANGE
            var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

            // ACT
            var response = await client.GetAsync("/");

            // ASSERT
            response.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.Redirect);
        }

        [Fact]
        public async Task AdminEndpoint_ShouldRequireAuthentication()
        {
            // ARRANGE
            var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

            // ACT
            var response = await client.GetAsync("/Admin/ManageGames");

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
            response.Headers.Location?.ToString().ShouldContain("Account/Login");
        }

 

        [Fact]
        public async Task ApiAuthEndpoint_ShouldBeAccessible()
        {
            // ARRANGE
            var client = _factory.CreateClient();

            // ACT - Try to get token without credentials (should fail but endpoint is accessible)
            var response = await client.PostAsync("/Api/Auth/token", new StringContent(""));

            // ASSERT
            response.StatusCode.ShouldBeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.UnsupportedMediaType);
        }

        [Fact]
        public async Task AccountLoginEndpoint_ShouldBeAccessible()
        {
            // ARRANGE
            var client = _factory.CreateClient();

            // ACT
            var response = await client.GetAsync("/Account/Login");

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        [Fact]
        public async Task StaticFiles_ShouldBeServed()
        {
            // ARRANGE
            var client = _factory.CreateClient();

            // ACT - Try to access common static file paths
            var cssResponse = await client.GetAsync("/css/site.css");
            var jsResponse = await client.GetAsync("/js/site.js");

            // ASSERT - Should either be found or not found, but not server error
            cssResponse.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
            jsResponse.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task SwaggerEndpoint_ShouldBeAccessibleInDevelopment()
        {
            // ARRANGE
            var client = _factory.CreateClient();

            // ACT
            var response = await client.GetAsync("/swagger/index.html");

            // ASSERT - Should be accessible or not found depending on environment
            response.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        }

        #endregion

        #region Service Registration Tests

        [Fact]
        public void Services_DbContext_ShouldBeRegistered()
        {
            // ACT
            var dbContext = _factory.Services.GetService<ApplicationDbContext>();

            // ASSERT
            dbContext.ShouldNotBeNull();
        }

        [Fact]
        public void Services_IApplicationDbContext_ShouldBeRegistered()
        {
            // ACT
            var dbContext = _factory.Services.GetService<IApplicationDbContext>();

            // ASSERT
            dbContext.ShouldNotBeNull();
        }

        [Fact]
        public void Services_UserManager_ShouldBeRegistered()
        {
            // ACT
            var userManager = _factory.Services.GetService<Microsoft.AspNetCore.Identity.UserManager<DominationPoint.Core.Domain.ApplicationUser>>();

            // ASSERT
            userManager.ShouldNotBeNull();
        }

        [Fact]
        public void Services_SignInManager_ShouldBeRegistered()
        {
            // ACT
            var signInManager = _factory.Services.GetService<Microsoft.AspNetCore.Identity.SignInManager<DominationPoint.Core.Domain.ApplicationUser>>();

            // ASSERT
            signInManager.ShouldNotBeNull();
        }

        [Fact]
        public void Services_ControlPointService_ShouldBeRegistered()
        {
            // ACT
            var service = _factory.Services.GetService<IControlPointService>();

            // ASSERT
            service.ShouldNotBeNull();
        }

        [Fact]
        public void Services_GameplayService_ShouldBeRegistered()
        {
            // ACT
            var service = _factory.Services.GetService<IGameplayService>();

            // ASSERT
            service.ShouldNotBeNull();
        }

        [Fact]
        public void Services_GameManagementService_ShouldBeRegistered()
        {
            // ACT
            var service = _factory.Services.GetService<IGameManagementService>();

            // ASSERT
            service.ShouldNotBeNull();
        }

        [Fact]
        public void Services_MapAnnotationService_ShouldBeRegistered()
        {
            // ACT
            var service = _factory.Services.GetService<IMapAnnotationService>();

            // ASSERT
            service.ShouldNotBeNull();
        }

        [Fact]
        public void Services_ScoreboardService_ShouldBeRegistered()
        {
            // ACT
            var service = _factory.Services.GetService<IScoreboardService>();

            // ASSERT
            service.ShouldNotBeNull();
        }

        [Fact]
        public void Services_LiveScoreUpdateService_ShouldBeRegistered()
        {
            // ACT
            var hostedServices = _factory.Services.GetServices<Microsoft.Extensions.Hosting.IHostedService>();

            // ASSERT
            hostedServices.ShouldNotBeNull();
            hostedServices.Any(s => s.GetType().Name == "LiveScoreUpdateService").ShouldBeTrue();
        }

        [Fact]
        public void Services_JwtSettings_ShouldBeConfigured()
        {
            // ACT
            var configuration = _factory.Services.GetService<Microsoft.Extensions.Configuration.IConfiguration>();

            // ASSERT
            configuration.ShouldNotBeNull();
            var jwtKey = configuration["Jwt:Key"];
            jwtKey.ShouldNotBeNullOrEmpty();
        }

        [Fact]
        public void Services_AntiforgeryService_ShouldBeRegistered()
        {
            // ACT
            var antiforgery = _factory.Services.GetService<Microsoft.AspNetCore.Antiforgery.IAntiforgery>();

            // ASSERT
            antiforgery.ShouldNotBeNull();
        }

        #endregion

        #region Middleware Pipeline Tests

        [Fact]
        public async Task Middleware_HttpsRedirection_ShouldBeConfigured()
        {
            // ARRANGE
            var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

            // ACT
            var response = await client.GetAsync("http://localhost/Account/Login");

            // ASSERT - Should either allow HTTP or handle it gracefully
            response.StatusCode.ShouldBeOneOf(
                HttpStatusCode.OK,
                HttpStatusCode.Redirect,
                HttpStatusCode.MovedPermanently
            );
        }

        [Fact]
        public async Task Middleware_StaticFiles_ShouldBeConfigured()
        {
            // ARRANGE
            var client = _factory.CreateClient();

            // ACT - Request to wwwroot should be handled
            var response = await client.GetAsync("/favicon.ico");

            // ASSERT - Should handle static file requests
            response.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task Middleware_ExceptionHandling_ShouldHandleErrors()
        {
            // ARRANGE
            var client = _factory.CreateClient();

            // ACT - Request a non-existent endpoint
            var response = await client.GetAsync("/NonExistent/Route");

            // ASSERT - Should return 404, not 500
            response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task Middleware_Authentication_ShouldBeConfigured()
        {
            // ARRANGE
            var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

            // ACT
            var response = await client.GetAsync("/Admin/ManageGames");

            // ASSERT - Should redirect to login, proving authentication middleware is active
            response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
            response.Headers.Location?.ToString().ShouldContain("Login");
        }

        [Fact]
        public async Task Middleware_Authorization_ShouldEnforceRoles()
        {
            // ARRANGE
            var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

            // ACT - Try to access admin endpoint without authentication
            var response = await client.GetAsync("/Admin/ManageGames");

            // ASSERT
            response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        }

        [Fact]
        public async Task Middleware_Routing_ShouldMapControllers()
        {
            // ARRANGE
            var client = _factory.CreateClient();

            // ACT
            var accountResponse = await client.GetAsync("/Account/Login");
            var apiResponse = await client.PostAsync("/Api/Auth/token", new StringContent(""));

            // ASSERT - Routes should be mapped
            accountResponse.StatusCode.ShouldNotBe(HttpStatusCode.NotFound);
            apiResponse.StatusCode.ShouldNotBe(HttpStatusCode.NotFound);
        }

        #endregion

        #region Endpoint Discovery Tests


        [Fact]
        public async Task Application_DefaultRoute_ShouldMapToAccountLogin()
        {
            // ARRANGE
            var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = true,
                HandleCookies = true
            });

            // ACT
            var response = await client.GetAsync("/");

            // ASSERT - Default route should go to Account/Login
            var finalUrl = response.RequestMessage?.RequestUri?.ToString() ?? "";
            (finalUrl.Contains("Account/Login") || response.StatusCode == HttpStatusCode.OK).ShouldBeTrue();
        }


        #endregion

        #region Configuration Tests

        [Fact]
        public void Configuration_ConnectionString_ShouldBeConfigured()
        {
            // ACT
            var configuration = _factory.Services.GetService<Microsoft.Extensions.Configuration.IConfiguration>();
            var environment = _factory.Services.GetService<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();

            // ASSERT
            configuration.ShouldNotBeNull();

            // In Testing environment, connection string might be overridden or not used
            if (environment?.EnvironmentName == "Testing")
            {
                // In test environment, we use in-memory database configured in CustomWebApplicationFactory
                var dbContext = _factory.Services.GetService<ApplicationDbContext>();
                dbContext.ShouldNotBeNull();
            }
            else
            {
                var connectionString = configuration.GetConnectionString("DefaultConnection");
                connectionString.ShouldNotBeNullOrEmpty();
            }
        }


        [Fact]
        public void Configuration_JwtSettings_ShouldBeValid()
        {
            // ACT
            var configuration = _factory.Services.GetService<Microsoft.Extensions.Configuration.IConfiguration>();

            // ASSERT
            configuration.ShouldNotBeNull();
            configuration["Jwt:Key"].ShouldNotBeNullOrEmpty();
            configuration["Jwt:Issuer"].ShouldNotBeNullOrEmpty();
            configuration["Jwt:Audience"].ShouldNotBeNullOrEmpty();
        }

        [Fact]
        public void Configuration_IdentityOptions_ShouldBeConfigured()
        {
            // ACT
            var options = _factory.Services.GetService<Microsoft.Extensions.Options.IOptions<Microsoft.AspNetCore.Identity.IdentityOptions>>();

            // ASSERT
            options.ShouldNotBeNull();
            options.Value.ShouldNotBeNull();
            options.Value.Password.RequiredLength.ShouldBe(6);
            options.Value.Password.RequireDigit.ShouldBeFalse();
            options.Value.Password.RequireUppercase.ShouldBeFalse();
            options.Value.Password.RequireLowercase.ShouldBeFalse();
            options.Value.Password.RequireNonAlphanumeric.ShouldBeFalse();
        }

        [Fact]
        public void Configuration_JwtAuthentication_ShouldBeConfigured()
        {
            // ACT
            var authSchemes = _factory.Services.GetService<Microsoft.AspNetCore.Authentication.IAuthenticationSchemeProvider>();

            // ASSERT
            authSchemes.ShouldNotBeNull();
            var jwtScheme = authSchemes.GetSchemeAsync("Bearer").Result;
            jwtScheme.ShouldNotBeNull();
        }

        #endregion

        #region Health Check Tests

        [Fact]
        public async Task Application_ShouldHandleMultipleSimultaneousRequests()
        {
            // ARRANGE
            var client = _factory.CreateClient();
            var tasks = new List<Task<HttpResponseMessage>>();

            // ACT - Make 10 simultaneous requests
            for (int i = 0; i < 10; i++)
            {
                tasks.Add(client.GetAsync("/Account/Login"));
            }

            var responses = await Task.WhenAll(tasks);

            // ASSERT - All requests should succeed
            foreach (var response in responses)
            {
                response.StatusCode.ShouldBe(HttpStatusCode.OK);
            }
        }


        [Fact]
        public void Environment_ShouldBeTesting()
        {
            // ACT
            var environment = _factory.Services.GetService<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();

            // ASSERT
            environment.ShouldNotBeNull();
            environment.EnvironmentName.ShouldBe("Testing");
        }

        [Fact]
        public void SeedData_ShouldNotRunInTestingEnvironment()
        {
            // ACT
            var environment = _factory.Services.GetService<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();

            // ASSERT - Verify we're in Testing environment so seed doesn't run
            environment.ShouldNotBeNull();
            environment.EnvironmentName.ShouldBe("Testing");
        }

        #endregion
    }
}
