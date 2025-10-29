namespace DominationPoint.PlaywrightTests.PageObjects;

public class GameDetailsPage
{
    private readonly IPage _page;

    public GameDetailsPage(IPage page)
    {
        _page = page;
    }

    // Locators - Map Grid
    private ILocator MapGrid => _page.Locator("#map-grid");
    private ILocator GridCells => _page.Locator(".grid-cell-container");

    // Locators - Modal
    private ILocator Modal => _page.Locator("#editTileModal");
    private ILocator ModalTitle => _page.Locator("#editTileModal .modal-title");
    private ILocator AnnotationTextInput => _page.Locator("#annotationText");
    private ILocator IsControlPointCheckbox => _page.Locator("#isCpCheckbox");
    private ILocator SaveTileButton => _page.Locator("#saveTileButton");
    private ILocator CancelButton => _page.Locator("#editTileModal button:has-text('Cancel')");

    // Locators - Participants
    private ILocator ParticipantsList => _page.Locator("h4:has-text('Participants') + ul.list-group");
    private ILocator ParticipantItems => ParticipantsList.Locator("li.list-group-item");

    // Locators - Available Teams
    private ILocator AvailableTeamsList => _page.Locator("h4:has-text('Available Teams') + ul.list-group");
    private ILocator AvailableTeamItems => AvailableTeamsList.Locator("li.list-group-item");

    // Actions - Map Editing
    public async Task ClickGridCellAsync(int x, int y)
    {
        var cell = _page.Locator($".grid-cell-container[data-x='{x}'][data-y='{y}']");
        await cell.ClickAsync();

        // Wait for modal to appear
        await Modal.WaitForAsync(new() { State = WaitForSelectorState.Visible });
    }

    public async Task FillAnnotationTextAsync(string text)
    {
        await AnnotationTextInput.FillAsync(text);
    }

    public async Task SetIsControlPointAsync(bool isControlPoint)
    {
        var isChecked = await IsControlPointCheckbox.IsCheckedAsync();

        if (isControlPoint && !isChecked)
        {
            await IsControlPointCheckbox.CheckAsync();
        }
        else if (!isControlPoint && isChecked)
        {
            await IsControlPointCheckbox.UncheckAsync();
        }
    }

    public async Task ClickSaveTileAsync()
    {
        await SaveTileButton.ClickAsync();
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    public async Task EditCellAsync(int x, int y, string text, bool isControlPoint)
    {
        await ClickGridCellAsync(x, y);
        await FillAnnotationTextAsync(text);
        await SetIsControlPointAsync(isControlPoint);
        await ClickSaveTileAsync();
    }

    // Actions - Participants
    public async Task AddParticipantAsync(string teamName)
    {
        var teamItems = await AvailableTeamItems.AllAsync();

        foreach (var item in teamItems)
        {
            var text = await item.TextContentAsync();
            if (text?.Contains(teamName) == true)
            {
                var addButton = item.Locator("button:has-text('Add')");
                await addButton.ClickAsync();
                await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                return;
            }
        }

        throw new Exception($"Team '{teamName}' not found in available teams");
    }

    public async Task RemoveParticipantAsync(string teamName)
    {
        var participantItems = await ParticipantItems.AllAsync();

        foreach (var item in participantItems)
        {
            var text = await item.TextContentAsync();
            if (text?.Contains(teamName) == true)
            {
                var removeButton = item.Locator("button:has-text('Remove')");
                await removeButton.ClickAsync();
                await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                return;
            }
        }

        throw new Exception($"Team '{teamName}' not found in participants");
    }

    // Queries - Map
    public async Task<string> GetCellAnnotationTextAsync(int x, int y)
    {
        var cell = _page.Locator($".grid-cell-container[data-x='{x}'][data-y='{y}']");
        var annotationText = cell.Locator(".map-annotation-text");

        try
        {
            return await annotationText.TextContentAsync() ?? "";
        }
        catch
        {
            return "";
        }
    }

    public async Task<bool> IsCellControlPointAsync(int x, int y)
    {
        var cell = _page.Locator($".grid-cell-container[data-x='{x}'][data-y='{y}']");
        var classes = await cell.GetAttributeAsync("class");
        return classes?.Contains("cp-marker-border") ?? false;
    }

    // Queries - Participants
    public async Task<bool> IsParticipantInListAsync(string teamName)
    {
        var participantItems = await ParticipantItems.AllAsync();

        foreach (var item in participantItems)
        {
            var text = await item.TextContentAsync();
            if (text?.Contains(teamName) == true)
            {
                return true;
            }
        }

        return false;
    }

    public async Task<bool> IsTeamAvailableAsync(string teamName)
    {
        var teamItems = await AvailableTeamItems.AllAsync();

        foreach (var item in teamItems)
        {
            var text = await item.TextContentAsync();
            if (text?.Contains(teamName) == true)
            {
                return true;
            }
        }

        return false;
    }

    public async Task<int> GetParticipantCountAsync()
    {
        return await ParticipantItems.CountAsync();
    }

    public async Task<int> GetAvailableTeamsCountAsync()
    {
        return await AvailableTeamItems.CountAsync();
    }

    // Queries - Modal
    public async Task<bool> IsModalVisibleAsync()
    {
        try
        {
            return await Modal.IsVisibleAsync();
        }
        catch
        {
            return false;
        }
    }

    public async Task<string> GetModalTitleAsync()
    {
        return await ModalTitle.TextContentAsync() ?? "";
    }

    public string CurrentUrl => _page.Url;
}
