namespace DominationPoint.PlaywrightTests.Fixtures;

public class PlaywrightFixture : IAsyncLifetime
{
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    public string BaseUrl { get; } = "https://localhost:7111";

    public IBrowserContext? Context { get; private set; }
    public IPage? Page { get; private set; }

    public async Task InitializeAsync()
    {
        Console.WriteLine("🎭 Initializing Playwright...");

        // Create Playwright
        _playwright = await Microsoft.Playwright.Playwright.CreateAsync();

        // Launch browser
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            SlowMo = 0
        });

        Console.WriteLine("✅ Playwright initialized");
        Console.WriteLine("⚠️  Make sure DominationPoint is running at https://localhost:7111");
    }

    public async Task DisposeAsync()
    {
        Console.WriteLine("🧹 Cleaning up Playwright...");

        if (_browser != null)
        {
            await _browser.CloseAsync();
        }

        _playwright?.Dispose();

        Console.WriteLine("✅ Cleanup complete");
    }

    public async Task<(IBrowserContext context, IPage page)> CreateNewContextAsync()
    {
        if (_browser == null)
        {
            throw new InvalidOperationException("Browser not initialized.");
        }

        var context = await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = BaseUrl,
            ViewportSize = new ViewportSize { Width = 1920, Height = 1080 },
            IgnoreHTTPSErrors = true,
            Locale = "en-US"
        });

        var page = await context.NewPageAsync();

        Context = context;
        Page = page;

        return (context, page);
    }

    public async Task CleanupContextAsync()
    {
        if (Page != null)
        {
            await Page.CloseAsync();
            Page = null;
        }

        if (Context != null)
        {
            await Context.CloseAsync();
            Context = null;
        }
    }
}
