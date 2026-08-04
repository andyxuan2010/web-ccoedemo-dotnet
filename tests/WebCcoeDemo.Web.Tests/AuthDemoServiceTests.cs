using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using WebCcoeDemo.Web.Models;
using WebCcoeDemo.Web.Services;

namespace WebCcoeDemo.Web.Tests;

public sealed class AuthDemoServiceTests
{
    [Fact]
    public void ValidateAndConsumeMsalState_AcceptsOnceAndRejectsReplay()
    {
        var service = CreateService();
        var session = new TestSession();
        session.SetString("msal_state", "expected-state");

        Assert.True(service.ValidateAndConsumeMsalState(session, "expected-state"));
        Assert.False(service.ValidateAndConsumeMsalState(session, "expected-state"));
    }

    [Fact]
    public void ValidateAndConsumeMsalState_RejectsMismatchAndConsumesState()
    {
        var service = CreateService();
        var session = new TestSession();
        session.SetString("msal_state", "expected-state");

        Assert.False(service.ValidateAndConsumeMsalState(session, "different-state"));
        Assert.False(session.TryGetValue("msal_state", out _));
    }

    [Fact]
    public void GetEasyAuthUser_ParsesPrincipalAndClaims()
    {
        var service = CreateService();
        var context = NewContext();
        var principal = new
        {
            identityProvider = "aad",
            userId = "user-id",
            userDetails = "user@example.com",
            claims = new[]
            {
                new { typ = "name", val = "Example User" },
                new { typ = "tid", val = "tenant-id" }
            }
        };
        context.Request.Headers["X-MS-CLIENT-PRINCIPAL"] = Convert.ToBase64String(
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(principal)));

        var user = service.GetEasyAuthUser(context);

        Assert.Equal("Example User", user["name"]);
        Assert.Equal("user@example.com", user["preferred_username"]);
        Assert.Equal("tenant-id", user["tid"]);
        var claims = Assert.IsType<Dictionary<string, string>>(user["claims"]);
        Assert.Equal("Example User", claims["name"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-base64")]
    public void GetEasyAuthUser_InvalidPrincipalReturnsEmpty(string header)
    {
        var service = CreateService();
        var context = NewContext();
        context.Request.Headers["X-MS-CLIENT-PRINCIPAL"] = header;

        Assert.Empty(service.GetEasyAuthUser(context));
    }

    [Fact]
    public void BuildRedirectUri_UsesRequestWhenNotConfigured()
    {
        var service = CreateService(new AppSettings { AadRedirectPath = "/auth/callback" });
        var context = NewContext("https", "demo.example.com");

        Assert.Equal("https://demo.example.com/auth/callback", service.BuildRedirectUri(context));
    }

    [Fact]
    public void BuildPortalUrls_EncodeConfiguredIdentifiers()
    {
        var service = CreateService(new AppSettings
        {
            AadClientId = "client/id",
            AppServiceName = "demo app",
            AppServiceSubscriptionId = "sub/id",
            AppServiceResourceGroup = "demo rg"
        });
        var context = NewContext();

        var appUrl = service.BuildAppServicePortalUrl(context);
        var registrationUrl = service.BuildAppRegistrationPortalUrl(context);

        Assert.Contains("subscriptions/sub%2Fid", appUrl);
        Assert.Contains("resourceGroups/demo%20rg", appUrl);
        Assert.Contains("sites/demo%20app", appUrl);
        Assert.EndsWith("client%2Fid", registrationUrl);
    }

    [Fact]
    public void TimelineRetainsNewestTwelveInReverseOrder()
    {
        var service = CreateService();
        var session = new TestSession();
        for (var index = 1; index <= 14; index++)
        {
            service.AddTimelineEvent(session, $"event-{index}");
        }

        var timeline = service.GetSessionTimeline(session);

        Assert.Equal(12, timeline.Count);
        Assert.Equal("event-14", timeline[0].Event);
        Assert.Equal("event-3", timeline[^1].Event);
    }

    private static AuthDemoService CreateService(AppSettings? settings = null) => new(settings ?? new AppSettings());

    private static DefaultHttpContext NewContext(string scheme = "https", string host = "demo.azurewebsites.net")
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = scheme;
        context.Request.Host = new HostString(host);
        return context;
    }
}
