namespace DominationPoint.PlaywrightTests.PageObjects;

public class ManageGamesPage
{
    private readonly IPage _page;

    public ManageGamesPage(IPage page)
    {
        _page = page;
    }

    // Locators - Create Game Form
    private ILocator GameNameInput => _page.Locator("input[name='name']");
    private ILocator StartTimeInput => _page.Locator("input[name='startTime']");
    private ILocator EndTimeInput => _page.Locator("input[name='endTime']");
    private ILocator ScheduleButton => _page.Locator("button[type='submit']:has-text('Schedule')");

    // Locators - Game Table
    private ILocator GameTable => _page.Locator("table.table-striped");
    private ILocator GameRows => _page.Locator("table.table-striped tbody tr");

    // Error messages
    private ILocator ErrorAlert => _page.Locator(".alert-danger");

    // Actions
    public async Task NavigateAsync()
    {
        await _page.GotoAsync("/Admin/ManageGames");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    public async Task CreateGameAsync(string gameName, DateTime startTime, DateTime endTime)
    {
        await GameNameInput.FillAsync(gameName);

        // Format datetime for datetime-local input
        var startTimeStr = startTime.ToString("yyyy-MM-ddTHH:mm");
        var endTimeStr = endTime.ToString("yyyy-MM-ddTHH:mm");

        await StartTimeInput.FillAsync(startTimeStr);
        await EndTimeInput.FillAsync(endTimeStr);

        await ScheduleButton.ClickAsync();
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    public async Task ClickEditMapForGameAsync(string gameName)
    {
        var row = await GetGameRowAsync(gameName);
        if (row == null) throw new Exception($"Game '{gameName}' not found");

        var editButton = row.Locator("a.btn-info:has-text('Edit Map & Teams')");
        await editButton.ClickAsync();
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    public async Task ClickStartGameAsync(string gameName)
    {
        var row = await GetGameRowAsync(gameName);
        if (row == null) throw new Exception($"Game '{gameName}' not found");

        var startButton = row.Locator("button:has-text('Start Now')");

        // Verify button exists
        var count = await startButton.CountAsync();
        if (count == 0)
        {
            throw new Exception($"'Start Now' button not found for game '{gameName}'. Is the game Scheduled?");
        }

        await startButton.ClickAsync();
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Give server time to process
        await Task.Delay(1000);

        // The page should refresh automatically, but if not, reload manually
        await _page.ReloadAsync();
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }


    public async Task ClickGoLiveAsync(string gameName)
    {
        var row = await GetGameRowAsync(gameName);
        if (row == null) throw new Exception($"Game '{gameName}' not found");

        var liveButton = row.Locator("a:has-text('Go Live')");

        // Verify button exists before clicking
        var count = await liveButton.CountAsync();
        if (count == 0)
        {
            throw new Exception($"'Go Live' button not found for game '{gameName}'. Is the game Active?");
        }

        await liveButton.ClickAsync();
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    // Queries
    public async Task<bool> IsGameInListAsync(string gameName)
    {
        var row = await GetGameRowAsync(gameName);
        return row != null;
    }

    public async Task<string> GetGameStatusAsync(string gameName)
    {
        var row = await GetGameRowAsync(gameName);
        if (row == null) return "";

        var statusBadge = row.Locator("span.badge");
        return await statusBadge.TextContentAsync() ?? "";
    }

    public async Task<bool> HasErrorAsync()
    {
        try
        {
            return await ErrorAlert.IsVisibleAsync();
        }
        catch
        {
            return false;
        }
    }

    public async Task<string> GetErrorMessageAsync()
    {
        try
        {
            return await ErrorAlert.TextContentAsync() ?? "";
        }
        catch
        {
            return "";
        }
    }

    public async Task<int> GetGameCountAsync()
    {
        return await GameRows.CountAsync();
    }

    // Helper
    private async Task<ILocator?> GetGameRowAsync(string gameName)
    {
        var rows = await GameRows.AllAsync();
        foreach (var row in rows)
        {
            var text = await row.TextContentAsync();
            if (text?.Contains(gameName) == true)
            {
                return row;
            }
        }
        return null;
    }

    public string CurrentUrl => _page.Url;
}
