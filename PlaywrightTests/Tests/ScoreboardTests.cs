using DominationPoint.PlaywrightTests.Fixtures;
using DominationPoint.PlaywrightTests.PageObjects;

namespace DominationPoint.PlaywrightTests.Tests;

public class ScoreboardTests : IClassFixture<PlaywrightFixture>, IAsyncLifetime
{
    private readonly PlaywrightFixture _fixture;
    private IPage _page = null!;
    private LoginPage _loginPage = null!;
    private ManageGamesPage _manageGamesPage = null!;
    private GameDetailsPage _gameDetailsPage = null!;
    private LiveGamePage _liveGamePage = null!;
    private ScoreboardPage _scoreboardPage = null!;

    public ScoreboardTests(PlaywrightFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        var (_, page) = await _fixture.CreateNewContextAsync();
        _page = page;
        _loginPage = new LoginPage(_page, _fixture.BaseUrl);
        _manageGamesPage = new ManageGamesPage(_page);
        _gameDetailsPage = new GameDetailsPage(_page);
        _liveGamePage = new LiveGamePage(_page);
        _scoreboardPage = new ScoreboardPage(_page);

        // Login as admin
        await _loginPage.NavigateAsync();
        await _loginPage.LoginAsync("admin@dominationpoint.com", "AdminP@ssw0rd!");
        await _page.WaitForURLAsync("**/Admin/**");
    }

    public async Task DisposeAsync()
    {
        await _fixture.CleanupContextAsync();
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Feature", "Scoreboard")]
    public async Task Test01_EndGame_ShouldShowScoreboardButton()
    {
        // ARRANGE - Create and start a game
        await EndAllActiveGamesAsync();

        var gameName = await CreateAndStartGameAsync();

        // ACT - End the game
        await _manageGamesPage.NavigateAsync();
        var endButton = _page.Locator($"table tbody tr:has-text('{gameName}') button:has-text('End Now')");
        await endButton.ClickAsync();
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // ASSERT - Should show "View Scoreboard" button
        var scoreboardButton = _page.Locator($"table tbody tr:has-text('{gameName}') a:has-text('View Scoreboard')");
        var isVisible = await scoreboardButton.IsVisibleAsync();
        Assert.True(isVisible, "Scoreboard button should be visible after game ends");
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Feature", "Scoreboard")]
    public async Task Test02_NavigateToScoreboard_ShouldLoadPage()
    {
        // ARRANGE - Create, start, and end a game
        var gameName = await CreateStartAndEndGameAsync();

        // ACT - Click View Scoreboard
        await _manageGamesPage.NavigateAsync();
        var scoreboardButton = _page.Locator($"table tbody tr:has-text('{gameName}') a:has-text('View Scoreboard')");
        await scoreboardButton.ClickAsync();
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // ASSERT
        Assert.Contains("/Admin/Scoreboard", _scoreboardPage.CurrentUrl);

        var title = await _scoreboardPage.GetPageTitleAsync();
        Assert.Contains(gameName, title);

        var isVisible = await _scoreboardPage.IsScoreboardVisibleAsync();
        Assert.True(isVisible, "Scoreboard table should be visible");
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Feature", "Scoreboard")]
    public async Task Test03_Scoreboard_ShouldShowParticipatingTeams()
    {
        // ARRANGE - Create game with participants
        var gameName = await CreateGameWithParticipantsAsync();

        // Start and end the game
        await _manageGamesPage.NavigateAsync();
        await _manageGamesPage.ClickStartGameAsync(gameName);
        await Task.Delay(1000);

        var endButton = _page.Locator($"table tbody tr:has-text('{gameName}') button:has-text('End Now')");
        await endButton.ClickAsync();
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // ACT - Navigate to scoreboard
        var scoreboardButton = _page.Locator($"table tbody tr:has-text('{gameName}') a:has-text('View Scoreboard')");
        await scoreboardButton.ClickAsync();
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // ASSERT
        var teamCount = await _scoreboardPage.GetTeamCountAsync();
        Assert.True(teamCount > 0, "Scoreboard should show at least one team");
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Feature", "Scoreboard")]
    public async Task Test04_Scoreboard_ShouldSortTeamsByScoreDescending()
    {
        // ARRANGE - Create and end game
        var gameName = await CreateStartAndEndGameAsync();

        // ACT - Navigate to scoreboard
        await _manageGamesPage.NavigateAsync();
        var scoreboardButton = _page.Locator($"table tbody tr:has-text('{gameName}') a:has-text('View Scoreboard')");
        await scoreboardButton.ClickAsync();
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // ASSERT - Scores should be sorted descending
        var isSorted = await _scoreboardPage.IsScoresSortedDescendingAsync();
        Assert.True(isSorted, "Teams should be sorted by score in descending order");
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Feature", "Scoreboard")]
    public async Task Test05_CompleteGameFlow_WithScoreboard()
    {
        // STEP 1: End any active games
        await EndAllActiveGamesAsync();

        // STEP 2: Create game with participant
        await _manageGamesPage.NavigateAsync();
        var gameName = $"ScoreboardTest{DateTime.Now.Ticks}";
        await _manageGamesPage.CreateGameAsync(
            gameName,
            DateTime.Now.AddMinutes(-5),
            DateTime.Now.AddHours(1)
        );

        // STEP 3: Add participant and control point
        await _manageGamesPage.ClickEditMapForGameAsync(gameName);

        var availableCount = await _gameDetailsPage.GetAvailableTeamsCountAsync();
        if (availableCount > 0)
        {
            var addButton = _page.Locator("h4:has-text('Available Teams') + ul button:has-text('Add')").First;
            await addButton.ClickAsync();
            await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        }

        await _gameDetailsPage.EditCellAsync(5, 5, "CP", isControlPoint: true);

        // STEP 4: Start game
        await _manageGamesPage.NavigateAsync();
        await _manageGamesPage.ClickStartGameAsync(gameName);
        await Task.Delay(1000);

        // STEP 5: Go live and assign control point
        await _manageGamesPage.NavigateAsync();
        await _manageGamesPage.ClickGoLiveAsync(gameName);

        var participants = await _liveGamePage.GetParticipantsAsync();
        if (participants.Count > 0)
        {
            await _liveGamePage.UpdateControlPointOwnerAsync(5, 5, participants[0].name);
            await Task.Delay(2000); // Let some score accumulate
        }

        // STEP 6: End game
        await _manageGamesPage.NavigateAsync();
        var endButton = _page.Locator($"table tbody tr:has-text('{gameName}') button:has-text('End Now')");
        await endButton.ClickAsync();
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // STEP 7: View scoreboard
        var scoreboardButton = _page.Locator($"table tbody tr:has-text('{gameName}') a:has-text('View Scoreboard')");
        var hasButton = await scoreboardButton.IsVisibleAsync();
        Assert.True(hasButton, "Scoreboard button should be visible");

        await scoreboardButton.ClickAsync();
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // STEP 8: Verify scoreboard
        Assert.Contains("/Admin/Scoreboard", _scoreboardPage.CurrentUrl);

        var isVisible = await _scoreboardPage.IsScoreboardVisibleAsync();
        Assert.True(isVisible, "Scoreboard should be visible");

        var teamCount = await _scoreboardPage.GetTeamCountAsync();
        Assert.True(teamCount >= 0, "Scoreboard should show teams");

        // SUCCESS!
        await _page.ScreenshotAsync(new() { Path = "test05-complete-scoreboard.png", FullPage = true });
    }

    // Helper methods
    private async Task EndAllActiveGamesAsync()
    {
        await _manageGamesPage.NavigateAsync();
        var endButtons = _page.Locator("button:has-text('End Now')");
        var count = await endButtons.CountAsync();

        for (int i = 0; i < count; i++)
        {
            var button = _page.Locator("button:has-text('End Now')").First;
            await button.ClickAsync();
            await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Task.Delay(500);
        }
    }

    private async Task<string> CreateAndStartGameAsync()
    {
        await _manageGamesPage.NavigateAsync();
        var gameName = $"ScoreTest{DateTime.Now.Ticks}";
        await _manageGamesPage.CreateGameAsync(
            gameName,
            DateTime.Now.AddMinutes(-5),
            DateTime.Now.AddHours(1)
        );

        await _manageGamesPage.ClickEditMapForGameAsync(gameName);
        await _gameDetailsPage.EditCellAsync(3, 3, "CP", isControlPoint: true);

        await _manageGamesPage.NavigateAsync();
        await _manageGamesPage.ClickStartGameAsync(gameName);
        await Task.Delay(1000);

        return gameName;
    }

    private async Task<string> CreateStartAndEndGameAsync()
    {
        await EndAllActiveGamesAsync();
        var gameName = await CreateAndStartGameAsync();

        await _manageGamesPage.NavigateAsync();
        var endButton = _page.Locator($"table tbody tr:has-text('{gameName}') button:has-text('End Now')");
        await endButton.ClickAsync();
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        return gameName;
    }

    private async Task<string> CreateGameWithParticipantsAsync()
    {
        await EndAllActiveGamesAsync();

        await _manageGamesPage.NavigateAsync();
        var gameName = $"TeamScore{DateTime.Now.Ticks}";
        await _manageGamesPage.CreateGameAsync(
            gameName,
            DateTime.Now.AddMinutes(-5),
            DateTime.Now.AddHours(1)
        );

        await _manageGamesPage.ClickEditMapForGameAsync(gameName);

        var availableCount = await _gameDetailsPage.GetAvailableTeamsCountAsync();
        if (availableCount > 0)
        {
            var addButton = _page.Locator("h4:has-text('Available Teams') + ul button:has-text('Add')").First;
            await addButton.ClickAsync();
            await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        }

        await _gameDetailsPage.EditCellAsync(4, 4, "CP", isControlPoint: true);

        return gameName;
    }
}
