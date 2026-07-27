using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Client;
using WebCcoeDemo.Web.Models;
using WebCcoeDemo.Web.Services;

namespace WebCcoeDemo.Web.Controllers;

public sealed class HomeController : Controller
{
    private readonly AppSettings _settings;
    private readonly AuthDemoService _authDemoService;

    public HomeController(AppSettings settings, AuthDemoService authDemoService)
    {
        _settings = settings;
        _authDemoService = authDemoService;
    }

    [HttpGet("/")]
    public IActionResult Index()
    {
        var msalUser = _authDemoService.GetMsalUser(HttpContext.Session);
        var easyAuthUser = _authDemoService.GetEasyAuthUser(HttpContext);
        PopulateLayoutViewData(msalUser, easyAuthUser);

        return View(new IndexViewModel
        {
            MsalUser = msalUser,
            EasyAuthUser = easyAuthUser
        });
    }

    [HttpGet("/login/msal")]
    public async Task<IActionResult> LoginMsal()
    {
        _authDemoService.AddTimelineEvent(HttpContext.Session, "MSAL sign-in started", "MSAL");
        var authUri = await _authDemoService.BuildMsalLoginUrlAsync(HttpContext);
        return Redirect(authUri);
    }

    [HttpGet("/login")]
    public IActionResult Login()
    {
        return RedirectToAction(nameof(LoginMsal));
    }

    [HttpGet("/login/easyauth")]
    public IActionResult LoginEasyAuth()
    {
        _authDemoService.AddTimelineEvent(HttpContext.Session, "Easy Auth sign-in started", "Easy Auth");
        var postLoginRedirectUri = Url.Action(nameof(ProfileEasyAuth), "Home", values: null, protocol: Request.Scheme) ?? "/profile/easyauth";
        return Redirect(_authDemoService.BuildEasyAuthLoginUrl(postLoginRedirectUri));
    }

    [HttpGet("/auth/callback")]
    public async Task<IActionResult> Authorized()
    {
        if (Request.Query.ContainsKey("error"))
        {
            PopulateLayoutViewData();
            return View("~/Views/Error/AuthError.cshtml", new AuthErrorViewModel
            {
                Error = Request.Query["error"].ToString(),
                ErrorDescription = Request.Query["error_description"].ToString()
            });
        }

        var code = Request.Query["code"].ToString();
        if (string.IsNullOrWhiteSpace(code))
        {
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var msalUser = await _authDemoService.ExchangeCodeForUserAsync(HttpContext, code);
            _authDemoService.SetMsalUser(HttpContext.Session, msalUser);
            _authDemoService.AddTimelineEvent(HttpContext.Session, "MSAL sign-in completed", "MSAL");
            return RedirectToAction(nameof(ProfileMsal));
        }
        catch (MsalException ex)
        {
            PopulateLayoutViewData();
            return View("~/Views/Error/AuthError.cshtml", new AuthErrorViewModel
            {
                Error = ex.ErrorCode,
                ErrorDescription = ex.Message
            });
        }
    }

    [HttpGet("/profile/msal")]
    public IActionResult ProfileMsal()
    {
        var user = _authDemoService.GetMsalUser(HttpContext.Session);
        if (user.Count == 0)
        {
            return RedirectToAction(nameof(LoginMsal));
        }

        _authDemoService.AddTimelineEvent(HttpContext.Session, "Viewed profile", "MSAL");
        PopulateLayoutViewData(user, _authDemoService.GetEasyAuthUser(HttpContext));
        var badges = AuthDemoService.BuildIdentityBadges(user, user);

        return View("~/Views/Home/Profile.cshtml", new ProfileViewModel
        {
            User = user,
            AuthMode = "MSAL",
            ClaimItems = AuthDemoService.BuildClaimItems(user),
            TenantBadges = badges.tenant,
            RoleBadges = badges.roles
        });
    }

    [HttpGet("/profile")]
    public IActionResult Profile()
    {
        return RedirectToAction(nameof(ProfileMsal));
    }

    [HttpGet("/profile/easyauth")]
    public IActionResult ProfileEasyAuth()
    {
        var easyAuthUser = _authDemoService.GetEasyAuthUser(HttpContext);
        if (easyAuthUser.Count == 0)
        {
            return RedirectToAction(nameof(LoginEasyAuth));
        }

        _authDemoService.AddTimelineEvent(HttpContext.Session, "Viewed profile", "Easy Auth");
        var user = ToStringMap(easyAuthUser);
        var claims = easyAuthUser.TryGetValue("claims", out var rawClaims) && rawClaims is Dictionary<string, string> map
            ? map
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var badges = AuthDemoService.BuildIdentityBadges(user, claims);

        PopulateLayoutViewData(_authDemoService.GetMsalUser(HttpContext.Session), easyAuthUser);
        return View("~/Views/Home/Profile.cshtml", new ProfileViewModel
        {
            User = user,
            AuthMode = "Easy Auth",
            ClaimItems = AuthDemoService.BuildClaimItems(claims),
            TenantBadges = badges.tenant,
            RoleBadges = badges.roles
        });
    }

    [HttpGet("/logout/msal")]
    public IActionResult LogoutMsal()
    {
        _authDemoService.AddTimelineEvent(HttpContext.Session, "Signed out", "MSAL");
        _authDemoService.ClearMsalSession(HttpContext.Session);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("/logout")]
    public IActionResult Logout()
    {
        return RedirectToAction(nameof(LogoutMsal));
    }

    [HttpGet("/logout/easyauth")]
    public IActionResult LogoutEasyAuth()
    {
        _authDemoService.AddTimelineEvent(HttpContext.Session, "Signed out", "Easy Auth");
        var postLogoutRedirectUri = Url.Action(nameof(Index), "Home", values: null, protocol: Request.Scheme) ?? "/";
        return Redirect(_authDemoService.BuildEasyAuthLogoutUrl(postLogoutRedirectUri));
    }

    [HttpGet("/logout/all")]
    public IActionResult LogoutAll()
    {
        _authDemoService.AddTimelineEvent(HttpContext.Session, "Signed out", "All");
        _authDemoService.ClearMsalSession(HttpContext.Session);

        var easyAuthUser = _authDemoService.GetEasyAuthUser(HttpContext);
        if (easyAuthUser.Count > 0)
        {
            var postLogoutRedirectUri = Url.Action(nameof(Index), "Home", values: null, protocol: Request.Scheme) ?? "/";
            return Redirect(_authDemoService.BuildEasyAuthLogoutUrl(postLogoutRedirectUri));
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet("/error")]
    public IActionResult Error()
    {
        PopulateLayoutViewData();
        return View("~/Views/Error/AuthError.cshtml", new AuthErrorViewModel
        {
            Error = "application_error",
            ErrorDescription = "Unexpected server error."
        });
    }

    private void PopulateLayoutViewData(
        Dictionary<string, string>? msalUser = null,
        Dictionary<string, object>? easyAuthUser = null)
    {
        msalUser ??= _authDemoService.GetMsalUser(HttpContext.Session);
        easyAuthUser ??= _authDemoService.GetEasyAuthUser(HttpContext);

        var activeAuthModes = new List<string>();
        if (msalUser.Count > 0)
        {
            activeAuthModes.Add("MSAL");
        }

        if (easyAuthUser.Count > 0)
        {
            activeAuthModes.Add("Easy Auth");
        }

        var authHealth = _authDemoService.BuildAuthHealth();
        ViewBag.ActiveAuthModes = activeAuthModes;
        ViewBag.IsSignedIn = activeAuthModes.Count > 0;
        ViewBag.CacheToken = Request.Query["cb"].ToString();
        ViewBag.AppServicePortalUrl = _authDemoService.BuildAppServicePortalUrl(HttpContext);
        ViewBag.AppRegistrationPortalUrl = _authDemoService.BuildAppRegistrationPortalUrl(HttpContext);
        ViewBag.SessionTimeline = _authDemoService.GetSessionTimeline(HttpContext.Session);
        ViewBag.AuthHealth = authHealth;
        ViewBag.AuthHealthReady = authHealth.All(item => item.Ok);
        ViewBag.RuntimeInfo = _authDemoService.BuildRuntimeInfo();
        ViewBag.AadRedirectPath = _settings.AadRedirectPath;
    }

    private static Dictionary<string, string> ToStringMap(Dictionary<string, object> source)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in source)
        {
            if (kvp.Value is string text && !string.IsNullOrWhiteSpace(text))
            {
                result[kvp.Key] = text;
            }
        }
        return result;
    }
}
