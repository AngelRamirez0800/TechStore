
using Microsoft.Playwright;
using Xunit;

namespace TechStore.Tests.E2ETests;

public class AuthorizationE2ETests
{
    private const string BaseUrl = "http://localhost:5062";

    private const string AdminEmail = "daniel@techstore.com";
    private const string ClientEmail = "angel@techstore.com";

    private static string AdminPassword =>
        Environment.GetEnvironmentVariable("TECHSTORE_ADMIN_PASSWORD")
        ?? throw new InvalidOperationException(
            "Configura TECHSTORE_ADMIN_PASSWORD.");

    private static string ClientPassword =>
        Environment.GetEnvironmentVariable("TECHSTORE_CLIENT_PASSWORD")
        ?? throw new InvalidOperationException(
            "Configura TECHSTORE_CLIENT_PASSWORD.");

    [Fact]
    public async Task Admin_CanViewProducts()
    {
        using var playwright = await Playwright.CreateAsync();

        await using var browser = await playwright.Chromium.LaunchAsync(
            new BrowserTypeLaunchOptions { Headless = true });

        var page = await browser.NewPageAsync();

        await LoginAsync(page, AdminEmail, AdminPassword);

        var response = await page.GotoAsync(
            $"{BaseUrl}/Products/Index");

        Assert.NotNull(response);
        Assert.Equal(200, response.Status);
    }

    [Fact]
    public async Task Admin_CanAccessProductCreation()
    {
        using var playwright = await Playwright.CreateAsync();

        await using var browser = await playwright.Chromium.LaunchAsync(
            new BrowserTypeLaunchOptions { Headless = true });

        var page = await browser.NewPageAsync();

        await LoginAsync(page, AdminEmail, AdminPassword);

        var response = await page.GotoAsync(
            $"{BaseUrl}/Products/Create");

        Assert.NotNull(response);
        Assert.Equal(200, response.Status);
    }

    [Fact]
    public async Task Client_CanViewProducts()
    {
        using var playwright = await Playwright.CreateAsync();

        await using var browser = await playwright.Chromium.LaunchAsync(
            new BrowserTypeLaunchOptions { Headless = true });

        var page = await browser.NewPageAsync();

        await LoginAsync(page, ClientEmail, ClientPassword);

        var response = await page.GotoAsync(
            $"{BaseUrl}/Products/Index");

        Assert.NotNull(response);
        Assert.Equal(200, response.Status);
    }

    [Fact]
    public async Task Client_CannotAccessProductCreation()
    {
        using var playwright = await Playwright.CreateAsync();

        await using var browser = await playwright.Chromium.LaunchAsync(
            new BrowserTypeLaunchOptions { Headless = true });

        var page = await browser.NewPageAsync();

        await LoginAsync(page, ClientEmail, ClientPassword);

        var response = await page.GotoAsync(
            $"{BaseUrl}/Products/Create");

        Assert.NotNull(response);

        Assert.Equal(
            403,
            response.Status);
    }

    private static async Task LoginAsync(
        IPage page,
        string email,
        string password)
    {
        await page.GotoAsync($"{BaseUrl}/Account/Login");

        await page.Locator("#Identifier").WaitForAsync();

        await page.Locator("#Identifier").FillAsync(email);

        await page.Locator("#loginPassword").FillAsync(password);

        await page.Locator("button[type='submit']").ClickAsync();

        await page.WaitForLoadStateAsync(
            LoadState.DOMContentLoaded);
    }
}
