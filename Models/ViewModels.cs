namespace WebCcoeDemo.Web.Models;

public sealed class IndexViewModel
{
    public Dictionary<string, string> MsalUser { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, object> EasyAuthUser { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class ProfileViewModel
{
    public Dictionary<string, string> User { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public string AuthMode { get; init; } = string.Empty;
    public List<KeyValuePair<string, string>> ClaimItems { get; init; } = new();
    public List<string> TenantBadges { get; init; } = new();
    public List<string> RoleBadges { get; init; } = new();
}

public sealed class AuthErrorViewModel
{
    public string Error { get; init; } = "authentication_error";
    public string ErrorDescription { get; init; } = "Unknown authentication error.";
}

public sealed class TimelineEvent
{
    public string Event { get; init; } = string.Empty;
    public string Mode { get; init; } = string.Empty;
    public string Detail { get; init; } = string.Empty;
    public string At { get; init; } = string.Empty;
}

public sealed class AuthHealthItem
{
    public string Name { get; init; } = string.Empty;
    public bool Ok { get; init; }
    public string Value { get; init; } = string.Empty;
}
