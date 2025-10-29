using DominationPoint.PlaywrightTests.Fixtures;
using DominationPoint.PlaywrightTests.PageObjects;
using Microsoft.Playwright;

namespace DominationPoint.PlaywrightTests.Tests;

public class AuthenticationTests : IClassFixture<PlaywrightFixture>, IAsyncLifetime
{
    private readonly PlaywrightFixture _fixture;
    private IPage _page = null!;
    private LoginPage _loginPage = null!;
    private AdminPage _adminPage = null!;

    public AuthenticationTests(PlaywrightFixture fixture)
    {
        _fixture = fixture;
    }

    // Called before EACH test
    public async Task InitializeAsync()
    {
        var (context, page) = await _fixture.CreateNewContextAsync();
        _page = page;
        _loginPage = new LoginPage(_page, _fixture.BaseUrl);
        _adminPage = new AdminPage(_page);
    }

    // Called after EACH test
    public async Task DisposeAsync()
    {
        await _fixture.CleanupContextAsync();
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Priority", "Critical")]
    public async Task Test01_DirectLoginPage_ShouldLoadSuccessfully()
    {
        // ARRANGE & ACT
        await _loginPage.NavigateAsync();

        // ASSERT
        var currentUrl = _page.Url;
        Assert.Contains("/Account/Login", currentUrl);

        var emailVisible = await _page.Locator("#Email").IsVisibleAsync();
        Assert.True(emailVisible, "Email input should be visible");
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Priority", "Critical")]
    public async Task Test02_Login_WithInvalidCredentials_ShouldShowError()
    {
        // ARRANGE
        await _loginPage.NavigateAsync();

        // ACT
        await _loginPage.LoginAsync("wrong@email.com", "WrongPassword123!");

        // Wait for response
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // ASSERT
        var currentUrl = _page.Url;

        // Your app returns the view with model errors
        // The error should be visible on the page
        var isErrorVisible = await _loginPage.IsErrorVisibleAsync();

        if (!isErrorVisible)
        {
            // Take screenshot to debug
            await _page.ScreenshotAsync(new() { Path = "test02-failure.png", FullPage = true });
            var html = await _page.ContentAsync();
            Console.WriteLine($"Current URL: {currentUrl}");
            Console.WriteLine($"Page has error elements: {await _page.Locator(".text-danger").CountAsync()}");
        }

        Assert.True(isErrorVisible,
            $"Error message should be visible after failed login. URL: {currentUrl}");

        var errorText = await _loginPage.GetErrorTextAsync();
        Assert.Contains("Invalid login attempt", errorText);
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Priority", "Critical")]
    public async Task Test03_Login_WithValidCredentials_ShouldNavigateToAdmin()
    {
        // ARRANGE
        await _loginPage.NavigateAsync();

        // ACT
        await _loginPage.LoginAsync("admin@dominationpoint.com", "AdminP@ssw0rd!");

        // Wait for navigation - simplified
        await _page.WaitForURLAsync("**/Admin/**", new() { Timeout = 10000 });

        // ASSERT
        Assert.Contains("/Admin", _page.Url);
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Priority", "Critical")]
    public async Task Test04_Logout_ShouldRedirectToHome()
    {
        // ARRANGE - Login first
        await _loginPage.NavigateAsync();
        await _loginPage.LoginAsync("admin@dominationpoint.com", "AdminP@ssw0rd!");
        await _page.WaitForURLAsync("**/Admin/**");

        Assert.Contains("/Admin", _page.Url);

        // ACT
        await _adminPage.ClickLogoutAsync();

        // ASSERT
        var currentUrl = _page.Url;
        Assert.True(
            currentUrl.EndsWith("/") || currentUrl.Contains("/Home") || currentUrl.Contains("/Account/Login"),
            $"Should redirect after logout, but got: {currentUrl}"
        );
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Priority", "Critical")]
    public async Task Test05_AccessProtectedPage_WithoutAuth_ShouldRedirectToLogin()
    {
        // ARRANGE & ACT
        await _page.GotoAsync($"{_fixture.BaseUrl}/Admin/ManageGames");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // ASSERT - Should redirect to login
        var currentUrl = _page.Url;

        // Accept either direct login redirect OR home page (for anonymous users)
        var isRedirected = currentUrl.Contains("/Account/Login") ||
                          currentUrl.EndsWith("/") ||
                          currentUrl.Contains("/Home");

        Assert.True(isRedirected,
            $"Protected page should redirect, but stayed at: {currentUrl}");
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Priority", "Critical")]
    public async Task Test06_CompleteLoginLogoutFlow_EndToEnd()
    {
        // STEP 1: Navigate to login
        await _loginPage.NavigateAsync();
        Assert.Contains("/Account/Login", _page.Url);

        // STEP 2: Failed login attempt
        await _loginPage.LoginAsync("wrong@test.com", "Wrong123!");
        var errorVisible = await _loginPage.IsErrorVisibleAsync();
        Assert.True(errorVisible, "Step 2: Error should be visible");

        // STEP 3: Successful login
        await _loginPage.LoginAsync("admin@dominationpoint.com", "AdminP@ssw0rd!");
        await _page.WaitForURLAsync("**/Admin/**", new() { Timeout = 10000 });
        Assert.Contains("/Admin", _page.Url);

        // STEP 4: Logout
        await _adminPage.ClickLogoutAsync();

        // STEP 5: Try to access protected page - should redirect
        await _page.GotoAsync($"{_fixture.BaseUrl}/Admin/ManageGames");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // After logout, accessing admin should redirect
        var finalUrl = _page.Url;
        var notOnAdminAnymore = !finalUrl.Contains("/Admin/ManageGames") ||
                                finalUrl.Contains("/Account/Login");
        Assert.True(notOnAdminAnymore, "Should not access admin after logout");

        // Success screenshot
        await _page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = "test-complete.png",
            FullPage = true
        });
    }
}
