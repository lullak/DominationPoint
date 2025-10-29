using Microsoft.Playwright;

namespace DominationPoint.PlaywrightTests.PageObjects;

public class LoginPage
{
    private readonly IPage _page;
    private readonly string _baseUrl;

    public LoginPage(IPage page, string baseUrl)
    {
        _page = page;
        _baseUrl = baseUrl;
    }

    // Correct selectors based on asp-for tag helpers
    private ILocator EmailInput => _page.Locator("#Email");
    private ILocator PasswordInput => _page.Locator("#Password");
    private ILocator RememberMeCheckbox => _page.Locator("#RememberMe");
    private ILocator LoginButton => _page.Locator("button[type='submit']:has-text('Log in')");
    private ILocator ValidationSummary => _page.Locator("div[asp-validation-summary='All']");
    private ILocator ErrorMessages => _page.Locator(".text-danger");

    // Actions
    public async Task NavigateAsync()
    {
        await _page.GotoAsync($"{_baseUrl}/Account/Login");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Wait for form to be ready
        await EmailInput.WaitForAsync(new()
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });
    }

    public async Task FillEmailAsync(string email)
    {
        await EmailInput.ClearAsync();
        await EmailInput.FillAsync(email);
    }

    public async Task FillPasswordAsync(string password)
    {
        await PasswordInput.ClearAsync();
        await PasswordInput.FillAsync(password);
    }

    public async Task SetRememberMeAsync(bool remember)
    {
        if (remember)
        {
            await RememberMeCheckbox.CheckAsync();
        }
        else
        {
            await RememberMeCheckbox.UncheckAsync();
        }
    }

    public async Task ClickLoginAsync()
    {
        await LoginButton.ClickAsync();
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    public async Task LoginAsync(string email, string password, bool rememberMe = false)
    {
        await FillEmailAsync(email);
        await FillPasswordAsync(password);
        if (rememberMe)
        {
            await SetRememberMeAsync(true);
        }
        await ClickLoginAsync();
    }

    // Queries
    public async Task<bool> IsErrorVisibleAsync()
    {
        try
        {
            var errors = await ErrorMessages.AllAsync();
            foreach (var error in errors)
            {
                if (await error.IsVisibleAsync())
                {
                    var text = await error.TextContentAsync();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        return true;
                    }
                }
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    public async Task<string> GetErrorTextAsync()
    {
        try
        {
            var errors = await ErrorMessages.AllAsync();
            foreach (var error in errors)
            {
                if (await error.IsVisibleAsync())
                {
                    var text = await error.TextContentAsync();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        return text.Trim();
                    }
                }
            }
            return "";
        }
        catch
        {
            return "";
        }
    }

    public string CurrentUrl => _page.Url;
}
