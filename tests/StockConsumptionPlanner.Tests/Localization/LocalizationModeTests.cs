using System.Net;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using StockConsumptionPlanner.App.Localization;

namespace StockConsumptionPlanner.Tests.Localization;

public sealed class LocalizationModeTests : IDisposable
{
    private readonly List<string> _dbPaths = [];
    private readonly List<WebApplicationFactory<Program>> _factories = [];

    [Theory]
    [InlineData("", PlannerLocalizationOptions.Multilingual, true)]
    [InlineData(" multilingual ", PlannerLocalizationOptions.Multilingual, true)]
    [InlineData("english", PlannerLocalizationOptions.English, false)]
    [InlineData("UKRAINIAN", PlannerLocalizationOptions.Ukrainian, false)]
    public void Validate_NormalizesKnownModes(string mode, string expected, bool allowsSelection)
    {
        PlannerLocalizationOptions options = new() { Mode = mode };

        options.Validate();

        Assert.Equal(expected, options.Mode);
        Assert.Equal(allowsSelection, options.AllowsLanguageSelection);
        Assert.Equal(!allowsSelection, options.TryGetFixedCulture(out string culture));
        if (allowsSelection)
        {
            Assert.Equal(string.Empty, culture);
        }
        else if (expected == PlannerLocalizationOptions.English)
        {
            Assert.Equal(PlannerCultures.English, culture);
        }
        else
        {
            Assert.Equal(PlannerCultures.Ukrainian, culture);
        }
    }

    [Fact]
    public void Validate_RejectsUnknownMode()
    {
        PlannerLocalizationOptions options = new() { Mode = "French" };

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            options.Validate);

        Assert.Contains("Localization:Mode", error.Message);
    }

    [Theory]
    [InlineData(PlannerLocalizationOptions.English, "en", "Consumption Plans", "uk-UA")]
    [InlineData(PlannerLocalizationOptions.Ukrainian, "uk", "Плани споживання", "en-US")]
    public async Task FixedLanguage_IgnoresCookieAndHidesLanguageSwitch(
        string mode,
        string language,
        string heading,
        string ignoredCulture)
    {
        using HttpClient client = CreateClient(mode);
        using HttpRequestMessage request = new(HttpMethod.Get, "/consumption-plans");
        request.Headers.AcceptLanguage.ParseAdd("en-US,uk-UA;q=0.8");
        request.Headers.Add("Cookie", CultureCookie(ignoredCulture));

        string html = await ReadPageAsync(client, request);

        Assert.Contains($"<html lang=\"{language}\">", html);
        Assert.Contains($"<h1 class=\"h3 mb-1\">{heading}</h1>", html);
        Assert.Contains("nav-toggle", html);
        Assert.DoesNotContain("class=\"language-switch\"", html);
        Assert.DoesNotContain("culture/set?", html);
    }

    [Theory]
    [InlineData(PlannerLocalizationOptions.English, "uk-UA", "en", "Consumption Plans")]
    [InlineData(PlannerLocalizationOptions.Ukrainian, "en-US", "uk", "Плани споживання")]
    public async Task FixedLanguage_SetCultureDoesNotChangeThePage(
        string mode,
        string requestedCulture,
        string language,
        string heading)
    {
        using HttpClient client = CreateClient(
            mode,
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = false
            });

        HttpResponseMessage response = await client.GetAsync(
            $"/culture/set?culture={requestedCulture}&redirectUri=%2Fconsumption-plans");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/consumption-plans", response.Headers.Location?.OriginalString);
        Assert.False(response.Headers.Contains("Set-Cookie"));

        string html = await ReadPageAsync(client, "/consumption-plans");
        Assert.Contains($"<html lang=\"{language}\">", html);
        Assert.Contains($"<h1 class=\"h3 mb-1\">{heading}</h1>", html);
    }

    [Fact]
    public void UnknownMode_FailsStartup()
    {
        WebApplicationFactory<Program> factory = CreateFactory("French");

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => factory.CreateClient());

        Assert.Contains("Localization:Mode", error.Message);
    }

    public void Dispose()
    {
        foreach (WebApplicationFactory<Program> factory in _factories)
        {
            factory.Dispose();
        }

        foreach (string dbPath in _dbPaths)
        {
            try
            {
                if (File.Exists(dbPath))
                {
                    File.Delete(dbPath);
                }
            }
            catch
            {
                // Ignore cleanup lock races in test teardown.
            }
        }
    }

    private HttpClient CreateClient(string mode, WebApplicationFactoryClientOptions? options = null)
    {
        WebApplicationFactory<Program> factory = CreateFactory(mode);
        if (options is null)
        {
            return factory.CreateClient();
        }

        return factory.CreateClient(options);
    }

    private WebApplicationFactory<Program> CreateFactory(string? mode)
    {
        string dbPath = Path.Combine(Path.GetTempPath(), $"scp-locale-{Guid.NewGuid():N}.db");
        _dbPaths.Add(dbPath);
        WebApplicationFactory<Program> factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(
            builder =>
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    Dictionary<string, string?> settings = new()
                    {
                        ["Persistence:Provider"] = "Sqlite",
                        ["ConnectionStrings:Sqlite"] = $"Data Source={dbPath};Pooling=False"
                    };
                    if (mode is not null)
                    {
                        settings["Localization:Mode"] = mode;
                    }

                    config.AddInMemoryCollection(settings);
                });
            });
        _factories.Add(factory);
        return factory;
    }

    private static string CultureCookie(string culture)
    {
        string value = CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture));
        return $"{CookieRequestCultureProvider.DefaultCookieName}={value}";
    }

    private static async Task<string> ReadPageAsync(HttpClient client, string path)
    {
        return await ReadPageAsync(client, new HttpRequestMessage(HttpMethod.Get, path));
    }

    private static async Task<string> ReadPageAsync(HttpClient client, HttpRequestMessage request)
    {
        HttpResponseMessage response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
    }
}
