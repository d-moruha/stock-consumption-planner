using System.Net;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace StockConsumptionPlanner.Tests.Localization;

public sealed class CultureSwitchTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"scp-culture-{Guid.NewGuid():N}.db");
    private readonly WebApplicationFactory<Program> _factory;

    public CultureSwitchTests(WebApplicationFactory<Program> baseFactory)
    {
        _factory = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Persistence:Provider"] = "Sqlite",
                    ["ConnectionStrings:Sqlite"] = $"Data Source={_dbPath};Pooling=False"
                });
            });
        });
    }

    [Fact]
    public async Task Pages_DefaultToUkrainian_EvenWhenBrowserPrefersEnglish()
    {
        using HttpClient client = _factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Get, "/consumption-plans");
        request.Headers.AcceptLanguage.ParseAdd("en-US,en;q=0.9");

        string html = await ReadPageAsync(client, request);

        Assert.Contains("<html lang=\"uk\">", html);
        Assert.Contains("<h1 class=\"h3 mb-1\">Плани споживання</h1>", html);
        Assert.Contains("Огляд", html);
        Assert.Contains("culture/set?culture=en-US&redirectUri=%2Fconsumption-plans", html);
        Assert.DoesNotContain(">About<", html);
    }

    [Fact]
    public async Task SetCulture_WritesCookieAndRedirectsBackToPage()
    {
        using HttpClient client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false
        });

        HttpResponseMessage response = await client.GetAsync("/culture/set?culture=en-US&redirectUri=%2Fstock-import");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/stock-import", response.Headers.Location?.OriginalString);
        string cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.StartsWith($"{CookieRequestCultureProvider.DefaultCookieName}=c%3Den-US%7Cuic%3Den-US;", cookie);
    }

    [Fact]
    public async Task CultureCookie_SwitchesPagesToEnglishAndBack()
    {
        using HttpClient client = _factory.CreateClient();

        string english = await ReadPageAsync(client, "/culture/set?culture=en-US&redirectUri=%2Fstock-import");
        Assert.Contains("<html lang=\"en\">", english);
        Assert.Contains("<title>Stock Report Import (.xlsx)</title>", english);

        string plansInEnglish = await ReadPageAsync(client, "/consumption-plans");
        Assert.Contains("<h1 class=\"h3 mb-1\">Consumption Plans</h1>", plansInEnglish);

        string ukrainian = await ReadPageAsync(client, "/culture/set?culture=uk-UA&redirectUri=%2Fconsumption-plans");
        Assert.Contains("<html lang=\"uk\">", ukrainian);
        Assert.Contains("<h1 class=\"h3 mb-1\">Плани споживання</h1>", ukrainian);
    }

    [Theory]
    [InlineData("fr-FR", "%2Fconsumption-plans", "/consumption-plans", false)]
    [InlineData("en-US", "https%3A%2F%2Fexample.com%2F", "/", true)]
    [InlineData("en-US", "%2F%2Fexample.com", "/", true)]
    public async Task SetCulture_IgnoresUnsupportedCulturesAndExternalRedirects(
        string culture,
        string redirectUri,
        string expectedLocation,
        bool expectCookie)
    {
        using HttpClient client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false
        });

        HttpResponseMessage response = await client.GetAsync($"/culture/set?culture={culture}&redirectUri={redirectUri}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(expectedLocation, response.Headers.Location?.OriginalString);
        Assert.Equal(expectCookie, response.Headers.Contains("Set-Cookie"));
    }

    public void Dispose()
    {
        _factory.Dispose();
        try
        {
            if (File.Exists(_dbPath))
            {
                File.Delete(_dbPath);
            }
        }
        catch
        {
            // Ignore cleanup lock races in test teardown.
        }
    }

    private static Task<string> ReadPageAsync(HttpClient client, string path)
    {
        return ReadPageAsync(client, new HttpRequestMessage(HttpMethod.Get, path));
    }

    private static async Task<string> ReadPageAsync(HttpClient client, HttpRequestMessage request)
    {
        HttpResponseMessage response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
    }
}
