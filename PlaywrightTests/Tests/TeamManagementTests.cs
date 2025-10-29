using DominationPoint.PlaywrightTests.Fixtures;
using DominationPoint.PlaywrightTests.PageObjects;

namespace DominationPoint.PlaywrightTests.Tests;

public class TeamManagementTests : IClassFixture<PlaywrightFixture>, IAsyncLifetime
{
    private readonly PlaywrightFixture _fixture;
    private IPage _page = null!;
    private LoginPage _loginPage = null!;
    private TeamsPage _teamsPage = null!;

    public TeamManagementTests(PlaywrightFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        var (_, page) = await _fixture.CreateNewContextAsync();
        _page = page;
        _loginPage = new LoginPage(_page, _fixture.BaseUrl);
        _teamsPage = new TeamsPage(_page);

        // Login as admin before each test
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
    [Trait("Feature", "TeamManagement")]
    public async Task Test01_NavigateToTeamsList_ShouldLoadPage()
    {
        // ARRANGE & ACT
        await _teamsPage.NavigateToTeamsListAsync();

        // ASSERT
        Assert.Contains("/Admin/ManageUsers", _teamsPage.CurrentUrl);

        var title = await _page.TitleAsync();
        Assert.Contains("Manage Teams", title);
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Feature", "TeamManagement")]
    public async Task Test02_ClickCreateNewTeam_ShouldNavigateToForm()
    {
        // ARRANGE
        await _teamsPage.NavigateToTeamsListAsync();

        // ACT
        await _teamsPage.ClickCreateNewTeamAsync();

        // ASSERT
        Assert.Contains("/Admin/CreateTeam", _teamsPage.CurrentUrl);

        var title = await _page.TitleAsync();
        Assert.Contains("Create New Team", title);
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Feature", "TeamManagement")]
    public async Task Test03_CreateTeam_WithValidData_ShouldAppearInList()
    {
        // ARRANGE
        await _teamsPage.NavigateToCreateTeamAsync();
        var teamName = $"TestTeam{DateTime.Now.Ticks}"; // ✅ No spaces!
        var teamColor = "#ff5733";

        // ACT
        await _teamsPage.CreateTeamAsync(teamName, teamColor);

        // ASSERT
        Assert.Contains("/Admin/ManageUsers", _teamsPage.CurrentUrl);

        var hasSuccess = await _teamsPage.HasSuccessMessageAsync();
        Assert.True(hasSuccess, "Success message should be displayed");

        var successMsg = await _teamsPage.GetSuccessMessageAsync();
        Assert.Contains(teamName, successMsg);

        var isInList = await _teamsPage.IsTeamInListAsync(teamName);
        Assert.True(isInList, $"Team '{teamName}' should appear in list");

        var color = await _teamsPage.GetTeamColorAsync(teamName);
        Assert.Equal(teamColor.ToLower(), color.ToLower());
    }



    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Feature", "TeamManagement")]
    public async Task Test04_UpdateNumpadCode_ShouldSaveCorrectly()
    {
        // ARRANGE - Create a team first
        var teamName = await CreateTestTeamAsync("CodeTestTeam", "#00FF00");
        await _teamsPage.NavigateToTeamsListAsync();

        // ACT
        var numpadCode = "1234";
        await _teamsPage.UpdateNumpadCodeAsync(teamName, numpadCode);

        // ASSERT
        // Page should reload
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Code should be saved
        var savedCode = await _teamsPage.GetNumpadCodeAsync(teamName);
        Assert.Equal(numpadCode, savedCode);
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Feature", "TeamManagement")]
    public async Task Test05_CreateTeam_WithDuplicateName_ShouldShowError()
    {
        // ARRANGE - Create a team first
        var teamName = $"Unique Team {DateTime.Now.Ticks}";
        await CreateTestTeamAsync(teamName, "#FF0000");

        // ACT - Try to create another team with same name
        await _teamsPage.NavigateToCreateTeamAsync();
        await _teamsPage.CreateTeamAsync(teamName, "#00FF00");

        // ASSERT
        // Should stay on create page
        Assert.Contains("/Admin/CreateTeam", _teamsPage.CurrentUrl);

        // Should show validation error
        var hasError = await _teamsPage.HasValidationErrorAsync();
        Assert.True(hasError, "Validation error should be shown for duplicate name");

        var errorText = await _teamsPage.GetValidationErrorTextAsync();

        // The error might say "username" instead of "team name"
        Assert.True(
            errorText.ToLower().Contains("already taken") ||
            errorText.ToLower().Contains("is already taken") ||
            errorText.ToLower().Contains("username"),
            $"Error should indicate duplicate. Got: {errorText}"
        );
    }


    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Feature", "TeamManagement")]
    public async Task Test06_CreateTeam_WithEmptyName_ShouldShowValidationError()
    {
        // ARRANGE
        await _teamsPage.NavigateToCreateTeamAsync();

        // ACT - Try to create with empty name
        await _teamsPage.CreateTeamAsync("", "#0000FF");

        // ASSERT
        // Should stay on create page
        Assert.Contains("/Admin/CreateTeam", _teamsPage.CurrentUrl);

        // Should show validation error
        var hasError = await _teamsPage.HasValidationErrorAsync();
        Assert.True(hasError, "Validation error should be shown for empty name");
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Feature", "TeamManagement")]
    public async Task Test07_CompleteTeamManagement_EndToEnd()
    {
        // STEP 1: Navigate to teams list
        await _teamsPage.NavigateToTeamsListAsync();
        Assert.Contains("/Admin/ManageUsers", _teamsPage.CurrentUrl);

        var initialCount = await _teamsPage.GetTeamCountAsync();

        // STEP 2: Click create new team
        await _teamsPage.ClickCreateNewTeamAsync();
        Assert.Contains("/Admin/CreateTeam", _teamsPage.CurrentUrl);

        // STEP 3: Create team
        var teamName = $"E2ETeam{DateTime.Now.Ticks}";
        var teamColor = "#9B59B6";
        await _teamsPage.CreateTeamAsync(teamName, teamColor);

        // STEP 4: Verify redirect and success
        Assert.Contains("/Admin/ManageUsers", _teamsPage.CurrentUrl);
        var hasSuccess = await _teamsPage.HasSuccessMessageAsync();
        Assert.True(hasSuccess);

        // STEP 5: Verify in list
        var isInList = await _teamsPage.IsTeamInListAsync(teamName);
        Assert.True(isInList);

        var newCount = await _teamsPage.GetTeamCountAsync();
        Assert.Equal(initialCount + 1, newCount);

        // STEP 6: Update numpad code
        await _teamsPage.UpdateNumpadCodeAsync(teamName, "5678");

        // STEP 7: Verify code saved
        var savedCode = await _teamsPage.GetNumpadCodeAsync(teamName);
        Assert.Equal("5678", savedCode);

        // SUCCESS!
        await _page.ScreenshotAsync(new() { Path = "test07-complete-team-management.png", FullPage = true });
    }

    // Helper method
    private async Task<string> CreateTestTeamAsync(string teamName, string color)
    {
        await _teamsPage.NavigateToCreateTeamAsync();
        await _teamsPage.CreateTeamAsync(teamName, color);
        return teamName;
    }
}
