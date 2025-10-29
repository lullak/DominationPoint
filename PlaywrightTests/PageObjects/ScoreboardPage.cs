namespace DominationPoint.PlaywrightTests.PageObjects;

public class ScoreboardPage
{
    private readonly IPage _page;

    public ScoreboardPage(IPage page)
    {
        _page = page;
    }

    // Locators
    private ILocator PageTitle => _page.Locator("h3");
    private ILocator GameEndTime => _page.Locator("p.text-muted");
    private ILocator ScoreTable => _page.Locator("table.table-hover");
    private ILocator TableRows => _page.Locator("table.table-hover tbody tr");

    // Queries
    public async Task<string> GetPageTitleAsync()
    {
        return await PageTitle.TextContentAsync() ?? "";
    }

    public async Task<bool> IsScoreboardVisibleAsync()
    {
        try
        {
            return await ScoreTable.IsVisibleAsync();
        }
        catch
        {
            return false;
        }
    }

    public async Task<int> GetTeamCountAsync()
    {
        return await TableRows.CountAsync();
    }

    public async Task<List<TeamScore>> GetTeamScoresAsync()
    {
        var scores = new List<TeamScore>();
        var rows = await TableRows.AllAsync();

        foreach (var row in rows)
        {
            var cells = await row.Locator("td").AllAsync();

            if (cells.Count >= 3)
            {
                var rankText = await cells[0].TextContentAsync();
                var teamText = await cells[1].TextContentAsync();
                var scoreText = await cells[2].TextContentAsync();

                // Extract rank
                var rank = int.TryParse(rankText?.Trim(), out var r) ? r : 0;

                // Extract team name (after the color circle)
                var teamName = teamText?.Trim() ?? "";

                // Extract score (remove commas if any)
                var scoreValue = scoreText?.Replace(",", "").Trim() ?? "0";
                var score = int.TryParse(scoreValue, out var s) ? s : 0;

                scores.Add(new TeamScore
                {
                    Rank = rank,
                    TeamName = teamName,
                    Score = score
                });
            }
        }

        return scores;
    }

    public async Task<int> GetTeamRankAsync(string teamName)
    {
        var scores = await GetTeamScoresAsync();
        var team = scores.FirstOrDefault(s => s.TeamName.Contains(teamName));
        return team?.Rank ?? 0;
    }

    public async Task<int> GetTeamScoreAsync(string teamName)
    {
        var scores = await GetTeamScoresAsync();
        var team = scores.FirstOrDefault(s => s.TeamName.Contains(teamName));
        return team?.Score ?? 0;
    }

    public async Task<bool> IsTeamInScoreboardAsync(string teamName)
    {
        var scores = await GetTeamScoresAsync();
        return scores.Any(s => s.TeamName.Contains(teamName));
    }

    public async Task<bool> IsScoresSortedDescendingAsync()
    {
        var scores = await GetTeamScoresAsync();

        for (int i = 0; i < scores.Count - 1; i++)
        {
            if (scores[i].Score < scores[i + 1].Score)
            {
                return false; // Not sorted descending
            }
        }

        return true;
    }

    public string CurrentUrl => _page.Url;
}

public class TeamScore
{
    public int Rank { get; set; }
    public string TeamName { get; set; } = "";
    public int Score { get; set; }
}
