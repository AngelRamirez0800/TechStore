
using Microsoft.Playwright;
using Xunit;

namespace TechStore.Tests.E2ETests;

public class AuthenticationE2ETests
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
    public async Task Admin_CanLoginSuccessfully()
    {
        using var playwright = await Playwright.CreateAsync();

        await using var browser = await playwright.Chromium.LaunchAsync(
            new BrowserTypeLaunchOptions { Headless = true });

        var page = await browser.NewPageAsync();

        await LoginAsync(page, AdminEmail, AdminPassword);

        Assert.False(
            page.Url.Contains("/Account/Login",
                StringComparison.OrdinalIgnoreCase),
            "El administrador no pudo iniciar sesión.");
    }

    [Fact]
    public async Task Client_CanLoginSuccessfully()
    {
        using var playwright = await Playwright.CreateAsync();

        await using var browser = await playwright.Chromium.LaunchAsync(
            new BrowserTypeLaunchOptions { Headless = true });

        var page = await browser.NewPageAsync();

        await LoginAsync(page, ClientEmail, ClientPassword);

        Assert.False(
            page.Url.Contains("/Account/Login",
                StringComparison.OrdinalIgnoreCase),
            "El cliente no pudo iniciar sesión.");
    }

    [Fact]
    public async Task InvalidPassword_CannotLogin()
    {
        using var playwright = await Playwright.CreateAsync();

        await using var browser = await playwright.Chromium.LaunchAsync(
            new BrowserTypeLaunchOptions { Headless = true });

        var page = await browser.NewPageAsync();

        await LoginAsync(
            page,
            AdminEmail,
            "PasswordIncorrecta123!");

        Assert.True(
            page.Url.Contains("/Account/Login",
                StringComparison.OrdinalIgnoreCase),
            "El sistema permitió el acceso con una contraseña incorrecta.");
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

        await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
    }
}
