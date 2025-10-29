using Microsoft.Playwright;

namespace DominationPoint.PlaywrightTests.PageObjects;

public class AdminPage
{
    private readonly IPage _page;

    public AdminPage(IPage page)
    {
        _page = page;
    }

    // Locators
    private ILocator LogoutForm => _page.Locator("form[action='/Account/Logout']");
    private ILocator LogoutButton => _page.Locator("form[action='/Account/Logout'] button[type='submit']");
    private ILocator NavbarBrand => _page.Locator("a.navbar-brand");
    private ILocator Sidebar => _page.Locator("nav.sidebar, .sidebar, nav[class*='sidebar']");
    private ILocator MainContent => _page.Locator("main, .main-content, #main-content");

    // Actions
    public async Task ClickLogoutAsync()
    {
        await LogoutButton.ClickAsync();
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    public async Task WaitForAdminPageAsync()
    {
        await _page.WaitForURLAsync("**/Admin/**", new() { Timeout = 10000 });

        try
        {
            await MainContent.WaitForAsync(new()
            {
                State = WaitForSelectorState.Visible,
                Timeout = 5000
            });
        }
        catch
        {
            // Main content not found, but URL is correct
        }
    }

    // Queries
    public bool IsOnAdminPage()
    {
        return _page.Url.Contains("/Admin/");
    }

    public async Task<bool> IsLoggedInAsync()
    {
        try
        {
            return await LogoutButton.IsVisibleAsync(); // Remove the timeout option
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> IsSidebarVisibleAsync()
    {
        try
        {
            return await Sidebar.IsVisibleAsync(); // Remove the timeout option
        }
        catch
        {
            return false;
        }
    }

    public string CurrentUrl => _page.Url;
}
