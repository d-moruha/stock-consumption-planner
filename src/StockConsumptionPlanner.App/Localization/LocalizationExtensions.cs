using Microsoft.AspNetCore.Localization;

namespace StockConsumptionPlanner.App.Localization;

public static class LocalizationExtensions
{
    public const string SetCulturePath = "/culture/set";

    private const string ApiPathPrefix = "/api";

    public static IServiceCollection AddPlannerLocalization(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddLocalization();
        services.Configure<RequestLocalizationOptions>(options =>
        {
            string[] cultures = [.. PlannerCultures.Supported];
            options.SetDefaultCulture(PlannerCultures.Default)
                .AddSupportedCultures(cultures)
                .AddSupportedUICultures(cultures);
            options.RequestCultureProviders = [new CookieRequestCultureProvider()];
        });

        return services;
    }

    /// <summary>
    /// Applies the culture cookie to UI requests (pages and the Blazor circuit) but not to <c>/api</c>,
    /// whose contracts stay culture-invariant.
    /// </summary>
    public static IApplicationBuilder UsePlannerRequestLocalization(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.UseWhen(
            context => !context.Request.Path.StartsWithSegments(ApiPathPrefix),
            branch => branch.UseRequestLocalization());
    }

    public static IEndpointRouteBuilder MapCultureEndpoint(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet(SetCulturePath, SetCulture);
        return endpoints;
    }

    public static string BuildSetCultureUrl(string culture, string redirectUri)
    {
        return $"{SetCulturePath.TrimStart('/')}?culture={Uri.EscapeDataString(culture)}" +
               $"&redirectUri={Uri.EscapeDataString(redirectUri)}";
    }

    private static IResult SetCulture(HttpContext context, string? culture, string? redirectUri)
    {
        if (PlannerCultures.TryNormalize(culture, out string selected))
        {
            context.Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(selected)),
                new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddYears(1),
                    IsEssential = true,
                    HttpOnly = true,
                    SameSite = SameSiteMode.Lax,
                    Path = "/"
                });
        }

        return Results.LocalRedirect(IsLocalPath(redirectUri) ? redirectUri! : "/");
    }

    private static bool IsLocalPath(string? uri)
    {
        return !string.IsNullOrEmpty(uri) &&
               uri[0] == '/' &&
               (uri.Length == 1 || (uri[1] != '/' && uri[1] != '\\')) &&
               !uri.Any(char.IsControl);
    }
}
