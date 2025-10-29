namespace DominationPoint.PlaywrightTests.PageObjects;

public class TeamsPage
{
    private readonly IPage _page;

    public TeamsPage(IPage page)
    {
        _page = page;
    }

    // Locators - Teams List
    private ILocator TeamsTable => _page.Locator("table.table-striped");
    private ILocator TeamRows => _page.Locator("table.table-striped tbody tr");
    private ILocator CreateTeamButton => _page.Locator("a.btn-success:has-text('Create New Team')");
    private ILocator SuccessAlert => _page.Locator(".alert-success");

    // Locators - Create Team Form
    private ILocator TeamNameInput => _page.Locator("#TeamName");
    private ILocator ColorInput => _page.Locator("#ColorHex");
    private ILocator SubmitButton => _page.Locator("button[type='submit']:has-text('Create Team')");
    private ILocator ValidationErrors => _page.Locator(".text-danger");

    // Actions - Navigation
    public async Task NavigateToTeamsListAsync()
    {
        await _page.GotoAsync("/Admin/ManageUsers");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    public async Task NavigateToCreateTeamAsync()
    {
        await _page.GotoAsync("/Admin/CreateTeam");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    public async Task ClickCreateNewTeamAsync()
    {
        await CreateTeamButton.ClickAsync();
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    // Actions - Create Team
    public async Task FillTeamNameAsync(string teamName)
    {
        await TeamNameInput.FillAsync(teamName);
    }

    public async Task SelectColorAsync(string hexColor)
    {
        // Color inputs need to be set using evaluate, not fill
        await ColorInput.EvaluateAsync($"input => input.value = '{hexColor}'");

        // Trigger change event to ensure validation
        await ColorInput.DispatchEventAsync("input");
        await ColorInput.DispatchEventAsync("change");
    }

    public async Task ClickSubmitAsync()
    {
        await SubmitButton.ClickAsync();
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    public async Task CreateTeamAsync(string teamName, string hexColor)
    {
        // Wait for form to be fully loaded
        await TeamNameInput.WaitForAsync(new() { State = WaitForSelectorState.Visible });

        await FillTeamNameAsync(teamName);
        await SelectColorAsync(hexColor);
        await ClickSubmitAsync();
    }

    // Actions - Update Numpad Code
    public async Task UpdateNumpadCodeAsync(string teamName, string numpadCode)
    {
        var row = await GetTeamRowAsync(teamName);
        if (row == null) throw new Exception($"Team '{teamName}' not found");

        var numpadInput = row.Locator("input[name='numpadCode']");
        await numpadInput.FillAsync(numpadCode);

        var saveButton = row.Locator("button:has-text('Save')");
        await saveButton.ClickAsync();
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    // Queries - Teams List
    public async Task<bool> IsTeamInListAsync(string teamName)
    {
        var row = await GetTeamRowAsync(teamName);
        return row != null;
    }

    public async Task<string> GetTeamColorAsync(string teamName)
    {
        var row = await GetTeamRowAsync(teamName);
        if (row == null) return "";

        var colorCell = row.Locator("td").Nth(1);
        var text = await colorCell.TextContentAsync();

        // Extract hex color from text (format: "      #FF0000")
        var match = System.Text.RegularExpressions.Regex.Match(text ?? "", @"#[0-9A-Fa-f]{6}");
        return match.Success ? match.Value : "";
    }

    public async Task<string> GetNumpadCodeAsync(string teamName)
    {
        var row = await GetTeamRowAsync(teamName);
        if (row == null) return "";

        var numpadInput = row.Locator("input[name='numpadCode']");
        return await numpadInput.InputValueAsync();
    }

    public async Task<int> GetTeamCountAsync()
    {
        return await TeamRows.CountAsync();
    }

    // Queries - Success/Error Messages
    public async Task<bool> HasSuccessMessageAsync()
    {
        try
        {
            return await SuccessAlert.IsVisibleAsync(new() { Timeout = 2000 });
        }
        catch
        {
            return false;
        }
    }

    public async Task<string> GetSuccessMessageAsync()
    {
        try
        {
            return await SuccessAlert.TextContentAsync() ?? "";
        }
        catch
        {
            return "";
        }
    }

    public async Task<bool> HasValidationErrorAsync()
    {
        try
        {
            var errors = await ValidationErrors.AllAsync();
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

    public async Task<string> GetValidationErrorTextAsync()
    {
        try
        {
            var errors = await ValidationErrors.AllAsync();
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

    // Helper
    private async Task<ILocator?> GetTeamRowAsync(string teamName)
    {
        var rows = await TeamRows.AllAsync();
        foreach (var row in rows)
        {
            var text = await row.TextContentAsync();
            if (text?.Contains(teamName) == true)
            {
                return row;
            }
        }
        return null;
    }

    public string CurrentUrl => _page.Url;
}
