using DominationPoint.PlaywrightTests.Fixtures;
using Microsoft.Playwright;

namespace DominationPoint.PlaywrightTests.Tests;

public class SimplePageTests : IClassFixture<PlaywrightFixture>, IAsyncLifetime
{
    private readonly PlaywrightFixture _fixture;
    private IPage _page = null!;

    public SimplePageTests(PlaywrightFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        var (_, page) = await _fixture.CreateNewContextAsync();
        _page = page;
    }

    public async Task DisposeAsync()
    {
        await _fixture.CleanupContextAsync();
    }

    [Fact]
    public async Task WhatPage_Is_AccountLogin_ShowingActually()
    {
        // Navigate
        await _page.GotoAsync($"{_fixture.BaseUrl}/Account/Login");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Save screenshot
        await _page.ScreenshotAsync(new() { Path = "account-login.png", FullPage = true });

        // Get HTML
        var html = await _page.ContentAsync();
        await File.WriteAllTextAsync("account-login.html", html);

        // Get page info
        var url = _page.Url;
        var title = await _page.TitleAsync();
        var h1 = await _page.Locator("h1, h2").First.TextContentAsync();

        Console.WriteLine($"URL: {url}");
        Console.WriteLine($"Title: {title}");
        Console.WriteLine($"H1/H2: {h1}");

        // Check selectors
        var emailSelectors = new[] { "#Email", "#Input_Email", "input[name='Email']", "input[type='email']" };
        foreach (var selector in emailSelectors)
        {
            var count = await _page.Locator(selector).CountAsync();
            Console.WriteLine($"Selector '{selector}': Found {count} elements");
        }

        Assert.True(true);
    }
}
