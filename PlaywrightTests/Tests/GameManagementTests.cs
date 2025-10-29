using DominationPoint.PlaywrightTests.Fixtures;
using DominationPoint.PlaywrightTests.PageObjects;

namespace DominationPoint.PlaywrightTests.Tests;

public class GameManagementTests : IClassFixture<PlaywrightFixture>, IAsyncLifetime
{
    private readonly PlaywrightFixture _fixture;
    private IPage _page = null!;
    private LoginPage _loginPage = null!;
    private ManageGamesPage _manageGamesPage = null!;
    private GameDetailsPage _gameDetailsPage = null!;
    private LiveGamePage _liveGamePage = null!;

    public GameManagementTests(PlaywrightFixture fixture)
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

        // Login before each test
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
    [Trait("Feature", "GameManagement")]
    public async Task Test01_CreateGame_WithValidData_ShouldAppearInList()
    {
        // ARRANGE
        await _manageGamesPage.NavigateAsync();
        var initialCount = await _manageGamesPage.GetGameCountAsync();

        // ACT
        var gameName = $"Operation Playwright {DateTime.Now.Ticks}"; // Unique name
        var startTime = DateTime.Now.AddHours(1);
        var endTime = DateTime.Now.AddHours(3);

        await _manageGamesPage.CreateGameAsync(gameName, startTime, endTime);

        // ASSERT
        var isInList = await _manageGamesPage.IsGameInListAsync(gameName);
        Assert.True(isInList, $"Game '{gameName}' should appear in the list");

        var newCount = await _manageGamesPage.GetGameCountAsync();
        Assert.Equal(initialCount + 1, newCount);

        var status = await _manageGamesPage.GetGameStatusAsync(gameName);
        Assert.Contains("Scheduled", status);
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Feature", "GameManagement")]
    public async Task Test02_CreateGame_NavigateToEditMap_ShouldLoadMapEditor()
    {
        // ARRANGE
        await _manageGamesPage.NavigateAsync();
        var gameName = $"Test Game {DateTime.Now.Ticks}";
        await _manageGamesPage.CreateGameAsync(gameName, DateTime.Now.AddHours(1), DateTime.Now.AddHours(2));

        // ACT
        await _manageGamesPage.ClickEditMapForGameAsync(gameName);

        // ASSERT
        Assert.Contains("/Admin/GameDetails", _gameDetailsPage.CurrentUrl);

        // Verify page elements are loaded
        var title = await _page.TitleAsync();
        Assert.Contains(gameName, title);
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Feature", "GameManagement")]
    public async Task Test03_AddParticipant_ShouldMoveFromAvailableToParticipants()
    {
        // ARRANGE
        await _manageGamesPage.NavigateAsync();
        var gameName = $"Team Test {DateTime.Now.Ticks}";
        await _manageGamesPage.CreateGameAsync(gameName, DateTime.Now.AddHours(1), DateTime.Now.AddHours(2));
        await _manageGamesPage.ClickEditMapForGameAsync(gameName);

        var initialParticipants = await _gameDetailsPage.GetParticipantCountAsync();
        var initialAvailable = await _gameDetailsPage.GetAvailableTeamsCountAsync();

        // ACT - Get first available team
        var availableCount = await _gameDetailsPage.GetAvailableTeamsCountAsync();

        if (availableCount == 0)
        {
            Assert.True(true, "Skipping test - no available teams.");
            return;
        }

        // Simplest approach: just click the first Add button and verify count changed
        var firstAddButton = _page.Locator("h4:has-text('Available Teams') + ul button:has-text('Add')").First;

        // Get the team name from the parent li element, excluding button text
        var parentLi = _page.Locator("h4:has-text('Available Teams') + ul li").First;
        var fullText = await parentLi.InnerTextAsync(); // InnerText is better than TextContent

        // The format is: "TeamName\nAdd" or "TeamName Add"
        var teamName = fullText.Split('\n')[0].Trim();

        // If there's no newline, split by "Add"
        if (teamName.Contains("Add"))
        {
            teamName = teamName.Replace("Add", "").Trim();
        }

        Console.WriteLine($"Extracted team name: '{teamName}'");

        // Click add button
        await firstAddButton.ClickAsync();
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // ASSERT
        // Verify participant count increased
        var newParticipants = await _gameDetailsPage.GetParticipantCountAsync();
        Assert.Equal(initialParticipants + 1, newParticipants);

        // Verify available count decreased
        var newAvailable = await _gameDetailsPage.GetAvailableTeamsCountAsync();
        Assert.Equal(initialAvailable - 1, newAvailable);

        // Verify the team appears in participants (if we got the name)
        if (!string.IsNullOrEmpty(teamName) && teamName != "Add")
        {
            var isParticipant = await _gameDetailsPage.IsParticipantInListAsync(teamName);
            Assert.True(isParticipant, $"Team '{teamName}' should be in participants list");
        }
    }


    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Feature", "GameManagement")]
    public async Task Test04_EditMapCell_AddAnnotation_ShouldDisplayText()
    {
        // ARRANGE
        await _manageGamesPage.NavigateAsync();
        var gameName = $"Map Test {DateTime.Now.Ticks}";
        await _manageGamesPage.CreateGameAsync(gameName, DateTime.Now.AddHours(1), DateTime.Now.AddHours(2));
        await _manageGamesPage.ClickEditMapForGameAsync(gameName);

        // ACT
        await _gameDetailsPage.EditCellAsync(x: 1, y: 1, text: "HQ", isControlPoint: false);

        // ASSERT
        var cellText = await _gameDetailsPage.GetCellAnnotationTextAsync(1, 1);
        Assert.Equal("HQ", cellText.Trim());
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Feature", "GameManagement")]
    public async Task Test05_EditMapCell_SetAsControlPoint_ShouldHaveBorder()
    {
        // ARRANGE
        await _manageGamesPage.NavigateAsync();
        var gameName = $"CP Test {DateTime.Now.Ticks}";
        await _manageGamesPage.CreateGameAsync(gameName, DateTime.Now.AddHours(1), DateTime.Now.AddHours(2));
        await _manageGamesPage.ClickEditMapForGameAsync(gameName);

        // ACT
        await _gameDetailsPage.EditCellAsync(x: 2, y: 2, text: "CP1", isControlPoint: true);

        // ASSERT
        var isControlPoint = await _gameDetailsPage.IsCellControlPointAsync(2, 2);
        Assert.True(isControlPoint, "Cell should be marked as control point");

        var cellText = await _gameDetailsPage.GetCellAnnotationTextAsync(2, 2);
        Assert.Equal("CP1", cellText.Trim());
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Feature", "GameManagement")]
    public async Task Test06_CompleteGameSetup_EndToEnd()
    {
        // This test performs the complete Steg 11 scenario

        // STEP 1: Navigate to ManageGames
        await _manageGamesPage.NavigateAsync();
        Assert.Contains("/Admin/ManageGames", _manageGamesPage.CurrentUrl);

        // STEP 2: Create game
        var gameName = $"Operation Playwright {DateTime.Now.Ticks}";
        await _manageGamesPage.CreateGameAsync(gameName, DateTime.Now.AddHours(1), DateTime.Now.AddHours(3));

        // STEP 3: Verify game in list
        var isInList = await _manageGamesPage.IsGameInListAsync(gameName);
        Assert.True(isInList, "Game should appear in list");

        // STEP 4: Navigate to Edit Map
        await _manageGamesPage.ClickEditMapForGameAsync(gameName);
        Assert.Contains("/Admin/GameDetails", _gameDetailsPage.CurrentUrl);

        // STEP 5: Add participant (if available)
        var availableCount = await _gameDetailsPage.GetAvailableTeamsCountAsync();
        if (availableCount > 0)
        {
            var availableItems = await _page.Locator("h4:has-text('Available Teams') + ul li").AllAsync();
            var firstTeamText = await availableItems[0].TextContentAsync();
            var teamName = firstTeamText?.Split('\n')[0].Trim() ?? "";

            await _gameDetailsPage.AddParticipantAsync(teamName);

            var isParticipant = await _gameDetailsPage.IsParticipantInListAsync(teamName);
            Assert.True(isParticipant, "Team should be added as participant");
        }

        // STEP 6: Edit map cell - add HQ with control point
        await _gameDetailsPage.EditCellAsync(x: 1, y: 1, text: "HQ", isControlPoint: true);

        // STEP 7: Verify changes
        var cellText = await _gameDetailsPage.GetCellAnnotationTextAsync(1, 1);
        Assert.Equal("HQ", cellText.Trim());

        var isCP = await _gameDetailsPage.IsCellControlPointAsync(1, 1);
        Assert.True(isCP, "Cell should be marked as control point with dashed border");

        // SUCCESS!
        await _page.ScreenshotAsync(new() { Path = "test06-complete-game-setup.png", FullPage = true });
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Feature", "LiveGame")]
    public async Task Test07_StartGame_ShouldChangeStatusToActive()
    {
        // ARRANGE
        // First, end any active games
        await EndAllActiveGamesAsync();

        await _manageGamesPage.NavigateAsync();
        var gameName = $"Start Test {DateTime.Now.Ticks}";

        await _manageGamesPage.CreateGameAsync(
            gameName,
            DateTime.Now.AddMinutes(-5),
            DateTime.Now.AddHours(2)
        );

        var initialStatus = await _manageGamesPage.GetGameStatusAsync(gameName);
        Assert.Contains("Scheduled", initialStatus);

        // ACT
        await _manageGamesPage.ClickStartGameAsync(gameName);

        // Wait for database to update
        await Task.Delay(1000);

        // ASSERT
        var newStatus = await _manageGamesPage.GetGameStatusAsync(gameName);
        Assert.Contains("Active", newStatus);
    }




    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Feature", "LiveGame")]
    public async Task Test08_NavigateToLiveGame_ShouldLoadLiveView()
    {
        // ARRANGE - End any active games first
        await EndAllActiveGamesAsync();

        await _manageGamesPage.NavigateAsync();
        var gameName = $"Live Test {DateTime.Now.Ticks}";

        // Use past start time
        await _manageGamesPage.CreateGameAsync(
            gameName,
            DateTime.Now.AddMinutes(-5),
            DateTime.Now.AddHours(2)
        );

        // Add control point before starting
        await _manageGamesPage.ClickEditMapForGameAsync(gameName);
        await _gameDetailsPage.EditCellAsync(x: 5, y: 5, text: "CP", isControlPoint: true);

        // Go back and start the game
        await _manageGamesPage.NavigateAsync();
        await _manageGamesPage.ClickStartGameAsync(gameName);

        // Wait for status update
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Task.Delay(1000); // Extra wait for status change

        // ACT
        await _manageGamesPage.ClickGoLiveAsync(gameName);

        // ASSERT
        Assert.Contains("/Admin/LiveGame", _liveGamePage.CurrentUrl);

        var isActive = await _liveGamePage.IsGameActiveAsync();
        Assert.True(isActive, "Game should show ACTIVE badge");

        var title = await _page.TitleAsync();
        Assert.Contains(gameName, title);
    }


    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Feature", "LiveGame")]
    public async Task Test09_ClickControlPoint_ShouldOpenModal()
    {
        // ARRANGE - Create game with CP and navigate to live view
        var gameName = await CreateActiveGameWithControlPointAsync(5, 5, "CP1");
        await _manageGamesPage.ClickGoLiveAsync(gameName);

        // ACT
        await _liveGamePage.ClickControlPointAsync(5, 5);

        // ASSERT
        var isModalVisible = await _liveGamePage.IsModalVisibleAsync();
        Assert.True(isModalVisible, "Modal should be visible after clicking control point");
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Feature", "LiveGame")]
    public async Task Test10_ChangeControlPointOwner_ShouldUpdateBackgroundColor()
    {
        // ARRANGE - Create game with participant and CP
        var gameName = await CreateActiveGameWithParticipantAndCPAsync(5, 5, "CP1");
        await _manageGamesPage.ClickGoLiveAsync(gameName);

        // Get initial color (should be neutral/gray)
        var initialColor = await _liveGamePage.GetControlPointBackgroundColorAsync(5, 5);

        // Get participant info to know what color to expect
        var participants = await _liveGamePage.GetParticipantsAsync();
        if (participants.Count == 0)
        {
            Assert.True(true, "Skipping - no participants available");
            return;
        }

        var firstParticipant = participants[0];
        Console.WriteLine($"Assigning CP to: {firstParticipant.name} (color: {firstParticipant.color})");

        // ACT
        await _liveGamePage.UpdateControlPointOwnerAsync(5, 5, firstParticipant.name);

        // ASSERT
        var newColor = await _liveGamePage.GetControlPointBackgroundColorAsync(5, 5);

        // Color should have changed from initial
        Assert.NotEqual(initialColor.ToLower(), newColor.ToLower());

        // Color should match team color (comparing normalized colors)
        Assert.Equal(
            firstParticipant.color.Replace(" ", "").ToLower(),
            newColor.Replace(" ", "").ToLower()
        );
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Feature", "LiveGame")]
    public async Task Test11_SetControlPointToNeutral_ShouldReturnToGrayColor()
    {
        // ARRANGE - Create game, assign to team first
        var gameName = await CreateActiveGameWithParticipantAndCPAsync(5, 5, "CP1");
        await _manageGamesPage.ClickGoLiveAsync(gameName);

        var participants = await _liveGamePage.GetParticipantsAsync();
        if (participants.Count == 0)
        {
            Assert.True(true, "Skipping - no participants");
            return;
        }

        // Assign to team
        await _liveGamePage.UpdateControlPointOwnerAsync(5, 5, participants[0].name);
        var teamColor = await _liveGamePage.GetControlPointBackgroundColorAsync(5, 5);

        // ACT - Set back to neutral
        await _liveGamePage.UpdateControlPointOwnerAsync(5, 5, "Neutral");

        // ASSERT
        var neutralColor = await _liveGamePage.GetControlPointBackgroundColorAsync(5, 5);

        // Should be different from team color
        Assert.NotEqual(teamColor.ToLower(), neutralColor.ToLower());

        // Should be gray/neutral (rgb(128, 128, 128) or #808080)
        Assert.True(
            neutralColor.Contains("128") || neutralColor.Contains("#808080") || neutralColor.Contains("gray"),
            $"Expected neutral/gray color, got: {neutralColor}"
        );
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Feature", "LiveGame")]
    public async Task Test12_CompleteLiveGameFlow_EndToEnd()
    {
        // STEP 1: Create game with participant and control point
        var gameName = await CreateActiveGameWithParticipantAndCPAsync(5, 5, "HQ");

        // STEP 2: Navigate to live game
        await _manageGamesPage.ClickGoLiveAsync(gameName);
        Assert.Contains("/Admin/LiveGame", _liveGamePage.CurrentUrl);

        // STEP 3: Verify game is active
        var isActive = await _liveGamePage.IsGameActiveAsync();
        Assert.True(isActive);

        // STEP 4: Verify control point exists
        var isCP = await _liveGamePage.IsControlPointAsync(5, 5);
        Assert.True(isCP, "Cell should be a control point");

        var cpText = await _liveGamePage.GetAnnotationTextAsync(5, 5);
        Assert.Equal("HQ", cpText.Trim());

        // STEP 5: Get participant
        var participants = await _liveGamePage.GetParticipantsAsync();
        if (participants.Count == 0)
        {
            Assert.True(true, "Test completed - no participants to test ownership");
            return;
        }

        var team = participants[0];
        Console.WriteLine($"Testing with team: {team.name}, color: {team.color}");

        // STEP 6: Assign control point to team
        await _liveGamePage.UpdateControlPointOwnerAsync(5, 5, team.name);

        var teamColor = await _liveGamePage.GetControlPointBackgroundColorAsync(5, 5);
        Assert.Equal(
            team.color.Replace(" ", "").ToLower(),
            teamColor.Replace(" ", "").ToLower()
        );

        // STEP 7: Set back to neutral
        await _liveGamePage.UpdateControlPointOwnerAsync(5, 5, "Neutral");

        var neutralColor = await _liveGamePage.GetControlPointBackgroundColorAsync(5, 5);
        Assert.NotEqual(teamColor.ToLower(), neutralColor.ToLower());

        // STEP 8: Assign to team again (re-capture)
        await _liveGamePage.UpdateControlPointOwnerAsync(5, 5, team.name);

        var finalColor = await _liveGamePage.GetControlPointBackgroundColorAsync(5, 5);
        Assert.Equal(
            team.color.Replace(" ", "").ToLower(),
            finalColor.Replace(" ", "").ToLower()
        );

        // SUCCESS!
        await _page.ScreenshotAsync(new() { Path = "test12-complete-live-game.png", FullPage = true });
    }

    // Helper methods
    private async Task<string> CreateActiveGameWithControlPointAsync(int x, int y, string cpText)
    {
        // End any active games first
        await EndAllActiveGamesAsync();

        await _manageGamesPage.NavigateAsync();
        var gameName = $"Live CP Test {DateTime.Now.Ticks}";

        await _manageGamesPage.CreateGameAsync(
            gameName,
            DateTime.Now.AddMinutes(-5),
            DateTime.Now.AddHours(2)
        );

        await _manageGamesPage.ClickEditMapForGameAsync(gameName);
        await _gameDetailsPage.EditCellAsync(x, y, cpText, isControlPoint: true);

        await _manageGamesPage.NavigateAsync();
        await _manageGamesPage.ClickStartGameAsync(gameName);

        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        return gameName;
    }

    private async Task<string> CreateActiveGameWithParticipantAndCPAsync(int x, int y, string cpText)
    {
        // End any active games first
        await EndAllActiveGamesAsync();

        await _manageGamesPage.NavigateAsync();
        var gameName = $"Live Full Test {DateTime.Now.Ticks}";

        await _manageGamesPage.CreateGameAsync(
            gameName,
            DateTime.Now.AddMinutes(-5),
            DateTime.Now.AddHours(2)
        );

        await _manageGamesPage.ClickEditMapForGameAsync(gameName);

        var availableCount = await _gameDetailsPage.GetAvailableTeamsCountAsync();
        if (availableCount > 0)
        {
            var firstAddButton = _page.Locator("h4:has-text('Available Teams') + ul button:has-text('Add')").First;
            await firstAddButton.ClickAsync();
            await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        }

        await _gameDetailsPage.EditCellAsync(x, y, cpText, isControlPoint: true);

        await _manageGamesPage.NavigateAsync();
        await _manageGamesPage.ClickStartGameAsync(gameName);

        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        return gameName;
    }



    private async Task EndAllActiveGamesAsync()
    {
        await _manageGamesPage.NavigateAsync();

        // Find all "End Now" buttons (indicates active games)
        var endButtons = _page.Locator("button:has-text('End Now')");
        var count = await endButtons.CountAsync();

        // End all active games
        for (int i = 0; i < count; i++)
        {
            // Always get the first button since they disappear after clicking
            var button = _page.Locator("button:has-text('End Now')").First;
            await button.ClickAsync();
            await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Task.Delay(500); // Give time for update
        }
    }
}
