# Architecture Guide

## Overview

`web-ccoedemo-dotnet` is an ASP.NET Core MVC demo that supports two sign-in models in one application:

1. `MSAL` flow managed by application code
2. `Easy Auth` flow managed by Azure App Service

Both modes share the same UI shell and session timeline, so behavior can be compared directly.

## Runtime Components

- `Program.cs`
  - Registers MVC, session services, `AppSettings`, and `AuthDemoService`
  - Configures middleware and route mapping
  - Normalizes alternate `AAD_REDIRECT_PATH` to `/auth/callback`
- `Controllers/HomeController.cs`
  - Owns page routes, sign-in entry points, callbacks, profile views, and sign-out actions
- `Services/AuthDemoService.cs`
  - Builds MSAL confidential client
  - Creates authorization URL
  - Exchanges auth code for token
  - Decodes Easy Auth principal header
  - Creates auth health and identity badges
- `Models/AppSettings.cs`
  - Reads environment variables and applies defaults
- `Views/*`
  - Landing page (`Index`)
  - Profile page with claim explorer (`Profile`)
  - Error page (`AuthError`)
  - Shared layout with side panel and client-side utilities

## Request Flows

### MSAL Flow (Application-Managed)

1. `GET /login/msal`
2. `AuthDemoService.BuildMsalLoginUrlAsync` builds auth URL with redirect URI
3. Browser redirects to Microsoft identity platform
4. Identity platform redirects to `GET /auth/callback?code=...`
5. `AuthDemoService.ExchangeCodeForUserAsync` exchanges code for token using same redirect URI
6. ID token claims are parsed and stored in session (`msal_user`)
7. User is redirected to `GET /profile/msal`

Key point:

- Redirect URI consistency between login request and token exchange is required to avoid `invalid_client` (`AADSTS500112`).

### Easy Auth Flow (Platform-Managed)

1. `GET /login/easyauth`
2. Browser redirects to `/.auth/login/aad?post_login_redirect_uri=...`
3. App Service handles Entra sign-in
4. App Service injects `X-MS-CLIENT-PRINCIPAL` header on app request
5. App decodes principal in `GetEasyAuthUser`
6. User is shown in `GET /profile/easyauth`

## Session and State

- ASP.NET Core session is used for:
  - `msal_user` claim map
  - `session_timeline` events (max 12 stored)
- Session backend is in-memory (`AddDistributedMemoryCache`)
- Session cookie:
  - name: `.AadSsoDemo.Session`
  - timeout: 30 minutes
  - secure policy: `Always` when running on App Service (`WEBSITE_SITE_NAME` is set)

## Configuration Model

`AppSettings.FromEnvironment()` binds runtime settings:

- AAD: client ID/secret/tenant/scopes/redirect settings
- Easy Auth endpoints: login/logout path
- Optional portal links for UI shortcuts

Defaults:

- `AAD_TENANT_ID=common`
- `AAD_REDIRECT_PATH=/auth/callback`
- `AAD_SCOPES=User.Read`
- `EASY_AUTH_LOGIN_PATH=/.auth/login/aad`
- `EASY_AUTH_LOGOUT_PATH=/.auth/logout`

Optional portal-link settings:

- `APP_SERVICE_PORTAL_URL`
- `APP_REGISTRATION_PORTAL_URL`
- `APP_SERVICE_NAME`
- `APP_SERVICE_SUBSCRIPTION_ID`
- `APP_SERVICE_RESOURCE_GROUP`

When the portal URL settings are blank, the app builds:

- a direct App Service portal URL from the App Service ARM resource ID components
- a direct App Registration portal URL from `AAD_CLIENT_ID`

The App Service link prefers built-in App Service metadata first:

- `WEBSITE_SITE_NAME`
- `WEBSITE_RESOURCE_GROUP`
- `WEBSITE_OWNER_NAME` (subscription segment)

Pipeline-provided `APP_SERVICE_*` settings are a fallback when those platform values are unavailable.

## Deployment Architecture

### Build + Package

Primary pipeline (`azure-pipelines.yml`) performs:

1. `UseDotNet@2` installs .NET 8 SDK
2. `DotNetCoreCLI@2 restore` using repo `NuGet.config`
3. `DotNetCoreCLI@2 publish` to staging folder
4. `ArchiveFiles@2` creates `app.zip`
5. Guard step ensures `web.config` exists at package root
6. Alternate pipeline `run_from_package.yml` uses the same published artifact shape but deploys it with `WEBSITE_RUN_FROM_PACKAGE=1`
7. Non-PR runs create an annotated semantic version tag in the `CreateGitTag` stage

### Release

- The shared Azure DevOps deploy template checks up to two App Service targets before deployment:
  - primary: `webAppNamePrimary`
  - secondary: `webAppNameSecondary`
- Secondary is optional and is skipped when blank or not found.
- The build artifact is downloaded once and reused for every eligible target.
- The release stage uses `AzureCLI@2` rather than `AzureWebApp@1`.
- Before each deployment, the pipeline:
  - resolves the target App Service and captures subscription/resource group metadata
  - runs SCM/Kudu DNS and TCP 443 preflight checks
  - switches `az` context to the target subscription
- Deployment then clears linked source-control config and uses `az webapp deployment source config-zip`.
- Target OS is detected dynamically from the App Service `reserved` flag:
  - Linux targets are configured with `DOTNETCORE|8.0`
  - Windows targets use standard ZIP deploy in `azure-pipelines.yml`
- The alternate `run_from_package.yml` pipeline sets `WEBSITE_RUN_FROM_PACKAGE=1` instead of using the standard content layout.
- `run_from_package.yml` is configured for manual use by default because both CI and PR triggers are set to `none`.
- The GitHub Actions workflow uses the same build artifact pattern, but its deploy job still iterates across primary, secondary, and third target variables and uses `az webapp deploy --type zip`.
- Because the published package includes `web.config`, the artifact remains compatible with Windows App Service/IIS hosting while still allowing Linux App Service deployment.

## Security and Auth Boundaries

- MSAL mode:
  - App handles OAuth authorization code flow and token handling
  - Requires correct app registration redirect URI and secret
- Easy Auth mode:
  - Platform handles upstream auth challenge and token issuance
  - App consumes normalized principal header

This separation makes it useful for operational comparison and troubleshooting.

## Validation Checklist

Use this checklist before deployment:

1. `dotnet restore WebCcoeDemo.Web.csproj --configfile NuGet.config`
2. `dotnet build WebCcoeDemo.Web.csproj -c Release`
3. Confirm app registration redirect URI contains:
   - `https://<app-name>.azurewebsites.net/auth/callback`
4. Confirm App Service app settings include required AAD values
5. Confirm pipeline service connection and app name match target environment
6. If the target App Service uses a private endpoint, confirm the self-hosted runner can resolve and reach `<app-name>.scm.azurewebsites.net` over TCP 443

## Known Limitations

- Automated tests cover application helpers and the local ASP.NET Core request pipeline; live Entra and App Service authentication flows remain outside the test boundary.
- Session store is in-memory only (not distributed across instances).
- MSAL user claims are read from the returned ID token and stored in session; this demo does not persist an MSAL token cache.
- Easy Auth behavior requires Azure App Service and cannot be exercised end-to-end by a normal local run.
- Some legacy naming remains (`FLASK_SECRET_KEY`) for compatibility.
- Local builds from the NAS-backed workspace may emit intermittent `obj` write warnings even when the build succeeds.
