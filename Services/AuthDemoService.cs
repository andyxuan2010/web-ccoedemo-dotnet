using System.IdentityModel.Tokens.Jwt;
using System.Globalization;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Identity.Client;
using Microsoft.AspNetCore.WebUtilities;
using WebCcoeDemo.Web.Models;

namespace WebCcoeDemo.Web.Services;

public sealed class AuthDemoService
{
    private const string MsalUserSessionKey = "msal_user";
    private const string MsalStateSessionKey = "msal_state";
    private const string SessionTimelineKey = "session_timeline";
    private readonly AppSettings _settings;

    public AuthDemoService(AppSettings settings)
    {
        _settings = settings;
    }

    public IConfidentialClientApplication BuildMsalApp(string? redirectUri = null)
    {
        var authority = $"https://login.microsoftonline.com/{_settings.AadTenantId}";
        var builder = ConfidentialClientApplicationBuilder
            .Create(_settings.AadClientId)
            .WithClientSecret(_settings.AadClientSecret)
            .WithAuthority(authority);

        if (!string.IsNullOrWhiteSpace(redirectUri))
        {
            builder = builder.WithRedirectUri(redirectUri);
        }

        return builder.Build();
    }

    public async Task<string> BuildMsalLoginUrlAsync(HttpContext context)
    {
        var redirectUri = BuildRedirectUri(context);
        var state = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        context.Session.SetString(MsalStateSessionKey, state);
        var result = await BuildMsalApp(redirectUri)
            .GetAuthorizationRequestUrl(_settings.AadScopes)
            .WithRedirectUri(redirectUri)
            .WithExtraQueryParameters(new Dictionary<string, (string value, bool includeInCacheKey)>
            {
                ["state"] = (state, false)
            })
            .ExecuteAsync();
        return result.ToString();
    }

    public bool ValidateAndConsumeMsalState(ISession session, string state)
    {
        var expectedState = session.GetString(MsalStateSessionKey);
        session.Remove(MsalStateSessionKey);

        if (string.IsNullOrWhiteSpace(expectedState) || string.IsNullOrWhiteSpace(state))
        {
            return false;
        }

        var expectedBytes = Encoding.UTF8.GetBytes(expectedState);
        var actualBytes = Encoding.UTF8.GetBytes(state);
        return expectedBytes.Length == actualBytes.Length
            && CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }

    public async Task<Dictionary<string, string>> ExchangeCodeForUserAsync(HttpContext context, string code)
    {
        var redirectUri = BuildRedirectUri(context);
        var result = await BuildMsalApp(redirectUri)
            .AcquireTokenByAuthorizationCode(_settings.AadScopes, code)
            .ExecuteAsync();

        var claims = new JwtSecurityTokenHandler().ReadJwtToken(result.IdToken).Claims;
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var claim in claims)
        {
            if (string.IsNullOrWhiteSpace(claim.Type) || string.IsNullOrWhiteSpace(claim.Value))
            {
                continue;
            }

            map[claim.Type] = claim.Value;
        }

        map["name"] = FirstValue(map, "name", "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name");
        map["preferred_username"] = FirstValue(
            map,
            "preferred_username",
            "upn",
            "email",
            "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/upn",
            "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress");
        map["tid"] = FirstValue(map, "tid", "tenantid", "http://schemas.microsoft.com/identity/claims/tenantid");
        map["oid"] = FirstValue(
            map,
            "oid",
            "objectidentifier",
            "http://schemas.microsoft.com/identity/claims/objectidentifier",
            "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");

        return map;
    }

    public Dictionary<string, object> GetEasyAuthUser(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue("X-MS-CLIENT-PRINCIPAL", out var rawHeader))
        {
            return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        }

        var principalHeader = rawHeader.ToString();
        if (string.IsNullOrWhiteSpace(principalHeader))
        {
            return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var payload = Encoding.UTF8.GetString(Convert.FromBase64String(principalHeader));
            var principal = JsonSerializer.Deserialize<EasyAuthPrincipal>(payload);
            if (principal is null)
            {
                return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            }

            var claimMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var claim in principal.Claims ?? [])
            {
                if (!string.IsNullOrWhiteSpace(claim.Type) && !string.IsNullOrWhiteSpace(claim.Value))
                {
                    claimMap[claim.Type] = claim.Value;
                }
            }

            var user = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["name"] = FirstClaim(claimMap, "name", "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name")
                    ?? principal.UserDetails
                    ?? string.Empty,
                ["preferred_username"] = FirstClaim(
                    claimMap,
                    "preferred_username",
                    "upn",
                    "email",
                    "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/upn",
                    "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress")
                    ?? principal.UserDetails
                    ?? string.Empty,
                ["tid"] = FirstClaim(claimMap, "tid", "tenantid", "http://schemas.microsoft.com/identity/claims/tenantid") ?? string.Empty,
                ["oid"] = FirstClaim(
                    claimMap,
                    "oid",
                    "objectidentifier",
                    "http://schemas.microsoft.com/identity/claims/objectidentifier",
                    "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")
                    ?? principal.UserId
                    ?? string.Empty,
                ["identity_provider"] = principal.IdentityProvider ?? string.Empty,
                ["authentication_type"] = principal.AuthType ?? string.Empty,
                ["user_id"] = principal.UserId ?? string.Empty,
                ["user_details"] = principal.UserDetails ?? string.Empty,
                ["claims"] = claimMap
            };
            string[] identityFields = ["user_id", "user_details", "oid", "preferred_username", "name"];
            return identityFields.Any(key =>
                user.TryGetValue(key, out var value) &&
                !string.IsNullOrWhiteSpace(value?.ToString()))
                ? user
                : new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public void SetMsalUser(ISession session, Dictionary<string, string> user)
    {
        session.SetJson(MsalUserSessionKey, user);
    }

    public Dictionary<string, string> GetMsalUser(ISession session)
    {
        return session.GetJson<Dictionary<string, string>>(MsalUserSessionKey)
            ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public void ClearMsalSession(ISession session)
    {
        session.Remove(MsalUserSessionKey);
        session.Remove(MsalStateSessionKey);
    }

    public void AddTimelineEvent(ISession session, string evt, string mode = "", string detail = "")
    {
        var timeline = session.GetJson<List<TimelineEvent>>(SessionTimelineKey) ?? new List<TimelineEvent>();
        timeline.Add(new TimelineEvent
        {
            Event = evt,
            Mode = mode,
            Detail = detail,
            At = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture)
        });
        session.SetJson(SessionTimelineKey, timeline.TakeLast(12).ToList());
    }

    public List<TimelineEvent> GetSessionTimeline(ISession session)
    {
        var timeline = session.GetJson<List<TimelineEvent>>(SessionTimelineKey) ?? new List<TimelineEvent>();
        timeline.Reverse();
        return timeline;
    }

    public string BuildRedirectUri(HttpContext context)
    {
        if (!string.IsNullOrWhiteSpace(_settings.AadRedirectUri))
        {
            return _settings.AadRedirectUri;
        }

        var hostUrl = $"{context.Request.Scheme}://{context.Request.Host}";
        return new Uri(new Uri($"{hostUrl}/"), _settings.AadRedirectPath.TrimStart('/')).ToString();
    }

    public string BuildAppServicePortalUrl(HttpContext context)
    {
        if (!string.IsNullOrWhiteSpace(_settings.AppServicePortalUrl))
        {
            return _settings.AppServicePortalUrl;
        }

        var subscriptionId = _settings.AppServiceSubscriptionId.Trim();
        var resourceGroup = _settings.AppServiceResourceGroup.Trim();
        var appServiceName = string.IsNullOrWhiteSpace(_settings.AppServiceName)
            ? GetSiteNameFromHost(context)
            : _settings.AppServiceName.Trim();
        if (!string.IsNullOrWhiteSpace(subscriptionId)
            && !string.IsNullOrWhiteSpace(resourceGroup)
            && !string.IsNullOrWhiteSpace(appServiceName))
        {
            return "https://portal.azure.com/#resource/subscriptions/"
                + UrlEncoder.Default.Encode(subscriptionId)
                + "/resourceGroups/"
                + UrlEncoder.Default.Encode(resourceGroup)
                + "/providers/Microsoft.Web/sites/"
                + UrlEncoder.Default.Encode(appServiceName)
                + "/overview";
        }

        var siteName = GetSiteNameFromHost(context);
        return "https://portal.azure.com/#view/HubsExtension/BrowseResource/resourceType/Microsoft.Web%2Fsites/search/"
            + UrlEncoder.Default.Encode(siteName);
    }

    public string BuildAppRegistrationPortalUrl(HttpContext context)
    {
        if (!string.IsNullOrWhiteSpace(_settings.AppRegistrationPortalUrl))
        {
            return _settings.AppRegistrationPortalUrl;
        }

        if (!string.IsNullOrWhiteSpace(_settings.AadClientId))
        {
            return "https://portal.azure.com/#view/Microsoft_AAD_RegisteredApps/ApplicationMenuBlade/~/Overview/appId/"
                + UrlEncoder.Default.Encode(_settings.AadClientId);
        }

        var siteName = GetSiteNameFromHost(context);
        return "https://portal.azure.com/#view/Microsoft_AAD_RegisteredApps/ApplicationsListBlade/searchText/"
            + UrlEncoder.Default.Encode(siteName);
    }

    public string BuildEasyAuthLoginUrl(string postLoginRedirectUri)
    {
        return $"{_settings.EasyAuthLoginPath}?post_login_redirect_uri={UrlEncoder.Default.Encode(postLoginRedirectUri)}";
    }

    public string BuildEasyAuthLogoutUrl(string postLogoutRedirectUri)
    {
        return $"{_settings.EasyAuthLogoutPath}?post_logout_redirect_uri={UrlEncoder.Default.Encode(postLogoutRedirectUri)}";
    }

    public List<KeyValuePair<string, string>> BuildRuntimeInfo()
    {
        var osType = (Environment.GetEnvironmentVariable("WEBSITE_OS") ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(osType))
        {
            osType = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Windows" : "Linux";
        }

        var stack = (Environment.GetEnvironmentVariable("WEBSITE_STACK") ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(stack))
        {
            stack = ".NET";
        }

        var version = RuntimeInformation.FrameworkDescription;
        var stackValue = BuildStackDisplayValue(stack, version);

        return new List<KeyValuePair<string, string>>
        {
            new("OS Type", osType),
            new("Stack", stackValue)
        };
    }

    private static string BuildStackDisplayValue(string stack, string version)
    {
        var normalizedStack = stack.Trim();
        var normalizedVersion = version.Trim();
        var stackKey = new string(normalizedStack.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
        var versionKey = new string(normalizedVersion.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

        if (string.IsNullOrWhiteSpace(normalizedStack))
        {
            return normalizedVersion;
        }

        if (string.IsNullOrWhiteSpace(normalizedVersion) || versionKey.Contains(stackKey, StringComparison.Ordinal))
        {
            return string.IsNullOrWhiteSpace(normalizedVersion) ? normalizedStack : normalizedVersion;
        }

        return $"{normalizedStack} {normalizedVersion}";
    }

    public List<AuthHealthItem> BuildAuthHealth()
    {
        var configuredRedirectUri = !string.IsNullOrWhiteSpace(_settings.AadRedirectUri) ? _settings.AadRedirectUri : "auto from host + path";
        var secretIsExplicit = !string.IsNullOrWhiteSpace(_settings.FlaskSecretKey);
        return new List<AuthHealthItem>
        {
            new() { Name = "AAD_CLIENT_ID", Ok = !string.IsNullOrWhiteSpace(_settings.AadClientId), Value = MaskValue(_settings.AadClientId) },
            new() { Name = "AAD_CLIENT_SECRET", Ok = !string.IsNullOrWhiteSpace(_settings.AadClientSecret), Value = !string.IsNullOrWhiteSpace(_settings.AadClientSecret) ? "configured" : "missing" },
            new() { Name = "AAD_TENANT_ID", Ok = !string.IsNullOrWhiteSpace(_settings.AadTenantId), Value = string.IsNullOrWhiteSpace(_settings.AadTenantId) ? "missing" : _settings.AadTenantId },
            new() { Name = "AAD_REDIRECT_PATH", Ok = _settings.AadRedirectPath.StartsWith('/'), Value = _settings.AadRedirectPath },
            new() { Name = "AAD_REDIRECT_URI", Ok = true, Value = configuredRedirectUri },
            new() { Name = "FLASK_SECRET_KEY", Ok = secretIsExplicit, Value = secretIsExplicit ? "explicit" : "demo fallback" }
        };
    }

    public static List<KeyValuePair<string, string>> BuildClaimItems(IDictionary<string, string> claims)
    {
        return claims
            .OrderBy(kvp => kvp.Key, StringComparer.Ordinal)
            .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Value))
            .ToList();
    }

    public static (List<string> tenant, List<string> roles) BuildIdentityBadges(Dictionary<string, string> user, Dictionary<string, string> claims)
    {
        var tenant = new List<string>();
        var roles = new List<string>();

        if (user.TryGetValue("tid", out var tid) && !string.IsNullOrWhiteSpace(tid))
        {
            tenant.Add(tid.Trim());
        }

        var roleValues = NormalizeClaimValues(
            claims.GetValueOrDefault("roles")
            ?? claims.GetValueOrDefault("role")
            ?? claims.GetValueOrDefault("http://schemas.microsoft.com/ws/2008/06/identity/claims/role"));
        var groupValues = NormalizeClaimValues(claims.GetValueOrDefault("groups"));

        roles.AddRange(roleValues.Take(8));
        if (groupValues.Count > 0)
        {
            roles.AddRange(groupValues.Take(4).Select(g => $"group:{g}"));
        }

        return (Dedupe(tenant), Dedupe(roles));
    }

    public static List<string> NormalizeClaimValues(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new List<string>();
        }

        var text = value.Trim();
        if (text.StartsWith('[') && text.EndsWith(']'))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<List<string>>(text);
                if (parsed is { Count: > 0 })
                {
                    return parsed.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToList();
                }
            }
            catch
            {
                // Ignore parse error and continue.
            }
        }

        if (text.Contains(','))
        {
            return text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
        }

        return new List<string> { text };
    }

    public string MaskValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "missing";
        }

        if (value.Length <= 8)
        {
            return value;
        }

        return $"{value[..4]}...{value[^4..]}";
    }

    private static string FirstValue(Dictionary<string, string> map, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (map.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }
        return string.Empty;
    }

    private static string? FirstClaim(Dictionary<string, string> claimMap, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (claimMap.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }
        return null;
    }

    private static string GetSiteNameFromHost(HttpContext context)
    {
        var host = context.Request.Host.Host;
        var firstDot = host.IndexOf('.');
        return firstDot > 0 ? host[..firstDot] : host;
    }

    private static List<string> Dedupe(IEnumerable<string> values)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var unique = new List<string>();
        foreach (var value in values)
        {
            if (seen.Add(value))
            {
                unique.Add(value);
            }
        }

        return unique;
    }

    private sealed class EasyAuthPrincipal
    {
        [JsonPropertyName("identityProvider")]
        public string? IdentityProvider { get; set; }
        [JsonPropertyName("userId")]
        public string? UserId { get; set; }
        [JsonPropertyName("userDetails")]
        public string? UserDetails { get; set; }
        [JsonPropertyName("auth_typ")]
        public string? AuthType { get; set; }
        [JsonPropertyName("claims")]
        public List<EasyAuthClaim>? Claims { get; set; }
    }

    private sealed class EasyAuthClaim
    {
        [JsonPropertyName("typ")]
        public string? Type { get; set; }
        [JsonPropertyName("val")]
        public string? Value { get; set; }
    }
}
