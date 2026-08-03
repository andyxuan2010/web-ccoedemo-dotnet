namespace WebCcoeDemo.Web.Models;

public sealed class AppSettings
{
    public string FlaskSecretKey { get; init; } = string.Empty;
    public string AadClientId { get; init; } = string.Empty;
    public string AadClientSecret { get; init; } = string.Empty;
    public string AadTenantId { get; init; } = "common";
    public string AadRedirectPath { get; init; } = "/auth/callback";
    public string AadRedirectUri { get; init; } = string.Empty;
    public string AadPostLogoutRedirectUri { get; init; } = string.Empty;
    public string EasyAuthLoginPath { get; init; } = "/.auth/login/aad";
    public string EasyAuthLogoutPath { get; init; } = "/.auth/logout";
    public string AppServicePortalUrl { get; init; } = string.Empty;
    public string AppRegistrationPortalUrl { get; init; } = string.Empty;
    public string AppServiceName { get; init; } = string.Empty;
    public string AppServiceSubscriptionId { get; init; } = string.Empty;
    public string AppServiceResourceGroup { get; init; } = string.Empty;
    public IReadOnlyList<string> AadScopes { get; init; } = new[] { "User.Read" };

    private static string ParseSubscriptionId(string? ownerName)
    {
        var trimmedOwnerName = ownerName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmedOwnerName))
        {
            return string.Empty;
        }

        return trimmedOwnerName.Split('+', 2, StringSplitOptions.TrimEntries)[0];
    }

    public static AppSettings FromEnvironment()
    {
        var rawScopes = Environment.GetEnvironmentVariable("AAD_SCOPES") ?? "User.Read";
        var scopes = rawScopes
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        if (scopes.Count == 0)
        {
            scopes.Add("User.Read");
        }

        var redirectPath = Environment.GetEnvironmentVariable("AAD_REDIRECT_PATH")?.Trim();
        if (string.IsNullOrWhiteSpace(redirectPath))
        {
            redirectPath = "/auth/callback";
        }
        else if (!redirectPath.StartsWith('/'))
        {
            redirectPath = "/" + redirectPath;
        }

        return new AppSettings
        {
            FlaskSecretKey = Environment.GetEnvironmentVariable("FLASK_SECRET_KEY")?.Trim() ?? string.Empty,
            AadClientId = Environment.GetEnvironmentVariable("AAD_CLIENT_ID")?.Trim() ?? string.Empty,
            AadClientSecret = Environment.GetEnvironmentVariable("AAD_CLIENT_SECRET")?.Trim() ?? string.Empty,
            AadTenantId = Environment.GetEnvironmentVariable("AAD_TENANT_ID")?.Trim() is { Length: > 0 } tenant ? tenant : "common",
            AadRedirectPath = redirectPath,
            AadRedirectUri = Environment.GetEnvironmentVariable("AAD_REDIRECT_URI")?.Trim() ?? string.Empty,
            AadPostLogoutRedirectUri = Environment.GetEnvironmentVariable("AAD_POST_LOGOUT_REDIRECT_URI")?.Trim() ?? string.Empty,
            EasyAuthLoginPath = Environment.GetEnvironmentVariable("EASY_AUTH_LOGIN_PATH")?.Trim() is { Length: > 0 } easyLogin ? easyLogin : "/.auth/login/aad",
            EasyAuthLogoutPath = Environment.GetEnvironmentVariable("EASY_AUTH_LOGOUT_PATH")?.Trim() is { Length: > 0 } easyLogout ? easyLogout : "/.auth/logout",
            AppServicePortalUrl = Environment.GetEnvironmentVariable("APP_SERVICE_PORTAL_URL")?.Trim() ?? string.Empty,
            AppRegistrationPortalUrl = Environment.GetEnvironmentVariable("APP_REGISTRATION_PORTAL_URL")?.Trim() ?? string.Empty,
            AppServiceName = Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME")?.Trim() is { Length: > 0 } websiteSiteName
                ? websiteSiteName
                : Environment.GetEnvironmentVariable("APP_SERVICE_NAME")?.Trim() is { Length: > 0 } appServiceName
                ? appServiceName
                    : string.Empty,
            AppServiceSubscriptionId = ParseSubscriptionId(Environment.GetEnvironmentVariable("WEBSITE_OWNER_NAME")) is { Length: > 0 } websiteSubscriptionId
                ? websiteSubscriptionId
                : Environment.GetEnvironmentVariable("APP_SERVICE_SUBSCRIPTION_ID")?.Trim() is { Length: > 0 } appServiceSubscriptionId
                ? appServiceSubscriptionId
                : Environment.GetEnvironmentVariable("ARM_SUBSCRIPTION_ID")?.Trim() ?? string.Empty,
            AppServiceResourceGroup = Environment.GetEnvironmentVariable("WEBSITE_RESOURCE_GROUP")?.Trim() is { Length: > 0 } websiteResourceGroup
                ? websiteResourceGroup
                : Environment.GetEnvironmentVariable("APP_SERVICE_RESOURCE_GROUP")?.Trim() is { Length: > 0 } appServiceResourceGroup
                ? appServiceResourceGroup
                    : string.Empty,
            AadScopes = scopes
        };
    }
}
