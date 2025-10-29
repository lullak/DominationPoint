namespace DominationPoint.PlaywrightTests.PageObjects;

public class LiveGamePage
{
    private readonly IPage _page;

    public LiveGamePage(IPage page)
    {
        _page = page;
    }

    // Locators - Map
    private ILocator MapGrid => _page.Locator("#map-grid");
    private ILocator ControlPointCells => _page.Locator(".grid-cell-container[data-iscp='true']");

    // Locators - Modal
    private ILocator Modal => _page.Locator("#editLiveTileModal");
    private ILocator ModalTitle => _page.Locator("#editLiveTileModal .modal-title");
    private ILocator OwnerSelect => _page.Locator("#ownerSelect");
    private ILocator SaveButton => _page.Locator("#editLiveTileModal button[type='submit']");
    private ILocator CancelButton => _page.Locator("#editLiveTileModal button:has-text('Cancel')");

    // Locators - Participants
    private ILocator ParticipantsList => _page.Locator("h4:has-text('Participating Teams') + ul");
    private ILocator ParticipantItems => ParticipantsList.Locator("li.list-group-item");

    // Locators - Status Badge
    private ILocator ActiveBadge => _page.Locator("span.badge:has-text('ACTIVE')");

    // Actions - Map Interaction
    public async Task ClickControlPointAsync(int x, int y)
    {
        var cell = _page.Locator($".grid-cell-container[data-x='{x}'][data-y='{y}'][data-iscp='true']");
        await cell.ClickAsync();

        // Wait for modal to appear
        await Modal.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
    }

    public async Task SelectOwnerAsync(string teamNameOrNeutral)
    {
        if (string.IsNullOrEmpty(teamNameOrNeutral) || teamNameOrNeutral.ToLower() == "neutral")
        {
            // Select the "-- Neutral --" option (empty value)
            await OwnerSelect.SelectOptionAsync("");
        }
        else
        {
            // Select by visible text (team name)
            await OwnerSelect.SelectOptionAsync(new SelectOptionValue { Label = teamNameOrNeutral });
        }
    }

    public async Task ClickSaveChangesAsync()
    {
        await SaveButton.ClickAsync();
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    public async Task UpdateControlPointOwnerAsync(int x, int y, string teamNameOrNeutral)
    {
        await ClickControlPointAsync(x, y);
        await SelectOwnerAsync(teamNameOrNeutral);
        await ClickSaveChangesAsync();
    }

    // Queries - Map State
    public async Task<string> GetControlPointBackgroundColorAsync(int x, int y)
    {
        var cell = _page.Locator($".grid-cell-container[data-x='{x}'][data-y='{y}']");
        var marker = cell.Locator(".control-point-marker");

        try
        {
            var style = await marker.GetAttributeAsync("style");
            if (style?.Contains("background-color") == true)
            {
                // Extract color from style attribute
                var match = System.Text.RegularExpressions.Regex.Match(style, @"background-color:\s*([^;]+)");
                if (match.Success)
                {
                    return match.Groups[1].Value.Trim();
                }
            }
            return "";
        }
        catch
        {
            return "";
        }
    }

    public async Task<bool> IsControlPointAsync(int x, int y)
    {
        var cell = _page.Locator($".grid-cell-container[data-x='{x}'][data-y='{y}']");
        var isCP = await cell.GetAttributeAsync("data-iscp");
        return isCP == "true";
    }

    public async Task<string> GetAnnotationTextAsync(int x, int y)
    {
        var cell = _page.Locator($".grid-cell-container[data-x='{x}'][data-y='{y}']");
        var annotation = cell.Locator(".map-annotation-text");

        try
        {
            return await annotation.TextContentAsync() ?? "";
        }
        catch
        {
            return "";
        }
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

    public async Task<string> GetSelectedOwnerAsync()
    {
        var selectedValue = await OwnerSelect.InputValueAsync();
        if (string.IsNullOrEmpty(selectedValue))
        {
            return "Neutral";
        }

        // Get the selected option text
        var selectedOption = OwnerSelect.Locator($"option[value='{selectedValue}']");
        return await selectedOption.TextContentAsync() ?? "";
    }

    // Queries - Participants
    public async Task<List<(string name, string color)>> GetParticipantsAsync()
    {
        var participants = new List<(string name, string color)>();
        var items = await ParticipantItems.AllAsync();

        foreach (var item in items)
        {
            var text = await item.TextContentAsync();
            var name = text?.Split("Code:")[0].Trim() ?? "";

            var colorSpan = item.Locator("span[style*='background-color']");
            var style = await colorSpan.GetAttributeAsync("style");
            var color = "";

            if (style?.Contains("background-color") == true)
            {
                var match = System.Text.RegularExpressions.Regex.Match(style, @"background-color:\s*([^;]+)");
                if (match.Success)
                {
                    color = match.Groups[1].Value.Trim();
                }
            }

            participants.Add((name, color));
        }

        return participants;
    }

    public async Task<int> GetParticipantCountAsync()
    {
        return await ParticipantItems.CountAsync();
    }

    // Queries - Game Status
    public async Task<bool> IsGameActiveAsync()
    {
        try
        {
            return await ActiveBadge.IsVisibleAsync();
        }
        catch
        {
            return false;
        }
    }

    public string CurrentUrl => _page.Url;
}
