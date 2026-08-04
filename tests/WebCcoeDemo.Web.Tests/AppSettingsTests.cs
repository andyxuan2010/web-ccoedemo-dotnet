using WebCcoeDemo.Web.Models;

namespace WebCcoeDemo.Web.Tests;

public sealed class AppSettingsTests
{
    [Fact]
    public void FromEnvironment_AppliesDefaultsAndNormalizesValues()
    {
        using var environment = new EnvironmentScope(new Dictionary<string, string?>
        {
            ["AAD_TENANT_ID"] = null,
            ["AAD_REDIRECT_PATH"] = "custom/callback",
            ["AAD_SCOPES"] = " User.Read, Mail.Read,User.Read "
        });

        var settings = AppSettings.FromEnvironment();

        Assert.Equal("common", settings.AadTenantId);
        Assert.Equal("/custom/callback", settings.AadRedirectPath);
        Assert.Equal(new[] { "User.Read", "Mail.Read", "User.Read" }, settings.AadScopes);
        Assert.Equal("/.auth/login/aad", settings.EasyAuthLoginPath);
        Assert.Equal("/.auth/logout", settings.EasyAuthLogoutPath);
    }

    [Fact]
    public void FromEnvironment_PrefersAppServiceMetadata()
    {
        using var environment = new EnvironmentScope(new Dictionary<string, string?>
        {
            ["WEBSITE_SITE_NAME"] = "platform-app",
            ["APP_SERVICE_NAME"] = "fallback-app",
            ["WEBSITE_RESOURCE_GROUP"] = "platform-rg",
            ["APP_SERVICE_RESOURCE_GROUP"] = "fallback-rg",
            ["WEBSITE_OWNER_NAME"] = "subscription-id+regionwebspace"
        });

        var settings = AppSettings.FromEnvironment();

        Assert.Equal("platform-app", settings.AppServiceName);
        Assert.Equal("platform-rg", settings.AppServiceResourceGroup);
        Assert.Equal("subscription-id", settings.AppServiceSubscriptionId);
    }

    private sealed class EnvironmentScope : IDisposable
    {
        private readonly Dictionary<string, string?> _original = new(StringComparer.Ordinal);

        public EnvironmentScope(IReadOnlyDictionary<string, string?> values)
        {
            foreach (var (name, value) in values)
            {
                _original[name] = Environment.GetEnvironmentVariable(name);
                Environment.SetEnvironmentVariable(name, value);
            }
        }

        public void Dispose()
        {
            foreach (var (name, value) in _original)
            {
                Environment.SetEnvironmentVariable(name, value);
            }
        }
    }
}
