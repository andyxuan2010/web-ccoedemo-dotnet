# web-ccoedemo-dotnet

ASP.NET Core (`net8.0`) Microsoft Entra authentication demo site for Azure App Service.

## Screenshot

![web-ccoedemo-dotnet screenshot 1](docs/images/readme-screenshot.png)

![web-ccoedemo-dotnet screenshot 2](docs/images/readme-screenshot-2.png)

![web-ccoedemo-dotnet screenshot 3](docs/images/readme-screenshot-3.png)

## Map of Content

| Document | Purpose |
| --- | --- |
| [README.md](README.md) | Repo overview, current scope, and quick links into the project. |
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | Application architecture, runtime components, and the MSAL versus Easy Auth request flows. |
| [docs/aadsso.md](docs/aadsso.md) | Easy Auth plus Entra ID SSO access-control layers and the portal settings that affect sign-in behavior. |
| [docs/DEPLOYMENT_METHODS.md](docs/DEPLOYMENT_METHODS.md) | Side-by-side comparison of Azure DevOps, GitHub Actions, Run From Package, Deployment Center, and container-based deployment options. |
| [docs/PIPELINE.md](docs/PIPELINE.md) | Focused walkthrough of the primary Azure DevOps pipeline and the alternate manual package-mounted pipeline. |
| [SECURITY.md](SECURITY.md) | Vulnerability reporting and operational security policy. |

## Current Scope

- Demonstrates both MSAL and Easy Auth patterns in one site
- Uses MVC and Razor views from the repo root
- Uses `azure-pipelines.yml` as the primary Azure DevOps ZIP deploy pipeline on `main`
- Includes an alternate manual `run_from_package.yml` pipeline with triggers disabled by default
- Includes `.github/workflows/azure-webapp.yml` for GitHub Actions build, deploy, and mirror-publish flows
- Includes deeper design notes in `docs/ARCHITECTURE.md`

## Prerequisites

- .NET 8 SDK
- An Entra app registration for the application-managed MSAL flow
- Azure App Service Authentication configured for the Easy Auth flow (when testing that mode)

## Run Locally

1. Restore and build the application:

   ```powershell
   dotnet restore WebCcoeDemo.Web.csproj --configfile NuGet.config
   dotnet build WebCcoeDemo.Web.csproj -c Release --no-restore
   ```

2. Set the MSAL environment variables in your shell or in the ignored local `.env` used by the repository setup tooling:

   ```text
   AAD_CLIENT_ID=<application-client-id>
   AAD_CLIENT_SECRET=<application-client-secret>
   AAD_TENANT_ID=<tenant-id>
   AAD_REDIRECT_URI=https://localhost:<port>/auth/callback
   ```

3. Start the site and open the URL printed by ASP.NET Core:

   ```powershell
   dotnet run --project WebCcoeDemo.Web.csproj
   ```

The landing page can run without identity credentials, but the MSAL health indicator and sign-in flow require `AAD_CLIENT_ID` and `AAD_CLIENT_SECRET`. Easy Auth identity headers are supplied by App Service and are not present in a normal local run.

## Configuration

| Variable | Required | Default | Purpose |
| --- | --- | --- | --- |
| `AAD_CLIENT_ID` | MSAL only | None | Entra application client ID. |
| `AAD_CLIENT_SECRET` | MSAL only | None | Entra application credential; keep it out of source control. |
| `AAD_TENANT_ID` | No | `common` | Tenant or authority segment. Prefer a tenant ID for single-tenant deployments. |
| `AAD_SCOPES` | No | `User.Read` | Comma-separated delegated scopes. |
| `AAD_REDIRECT_PATH` | No | `/auth/callback` | Callback path mapped by the application. |
| `AAD_REDIRECT_URI` | No | Derived from the request | Explicit callback URI; useful behind proxies or custom domains. |
| `AAD_POST_LOGOUT_REDIRECT_URI` | No | Derived from the request | Explicit post-logout destination. |
| `EASY_AUTH_LOGIN_PATH` | No | `/.auth/login/aad` | App Service authentication login endpoint. |
| `EASY_AUTH_LOGOUT_PATH` | No | `/.auth/logout` | App Service authentication logout endpoint. |
| `APP_SERVICE_NAME` | No | App Service metadata | Fallback used for portal links and runtime details. |
| `APP_SERVICE_SUBSCRIPTION_ID` | No | App Service metadata | Fallback subscription ID for portal links. |
| `APP_SERVICE_RESOURCE_GROUP` | No | App Service metadata | Fallback resource group for portal links. |

## Validate Changes

Run the same focused checks used during repository maintenance:

```powershell
dotnet build WebCcoeDemo.Web.csproj -c Release
dotnet test tests/WebCcoeDemo.Web.Tests/WebCcoeDemo.Web.Tests.csproj -c Release
dotnet list WebCcoeDemo.Web.csproj package --vulnerable --include-transitive
pre-commit run --all-files
./scripts/validate-github-workflow.ps1
./scripts/validate-shared-ui.ps1
```

The test project covers configuration parsing, OAuth state handling, Easy Auth principal parsing, URL generation, session timelines, readiness endpoints, security headers, and unauthenticated routing. Live Entra and App Service authentication still require post-deployment smoke testing because those identity services are external to the test host.

## Key Files

- `Program.cs`
- `Controllers/`
- `Views/`
- `Services/`
- `azure-pipelines.yml`
- `run_from_package.yml`
- `docs/ARCHITECTURE.md`
- `docs/aadsso.md`
- `docs/PIPELINE.md`
- `.github/workflows/azure-webapp.yml`
- `docs/DEPLOYMENT_METHODS.md`

## Notes

- The main Azure DevOps deploy template currently handles a required primary App Service target and an optional secondary target.
- The alternate `run_from_package.yml` pipeline and the GitHub Actions workflow still expose a third target variable, but it is blank by default.
- The Azure DevOps deployment flow uses `AzureCLI@2` and `az webapp deployment source config-zip` rather than `AzureWebApp@1`.
- The GitHub Actions deployment flow uses `azure/login@v2` plus `az webapp deploy --type zip`.
- The primary deployment method is ZIP deployment of published output with `WEBSITE_RUN_FROM_PACKAGE` removed before deployment.
- The alternate package-mounted method in `run_from_package.yml` sets `WEBSITE_RUN_FROM_PACKAGE=1` and is intended for manual use.
- The Azure DevOps pipeline creates an annotated semantic version tag on non-PR runs, and the GitHub Actions workflow mirrors that tag logic.
- The GitHub Actions workflow follows the sibling app pipeline pattern with separate build, deployment precheck, deploy, pre-publish check, and mirror-publish jobs.
- GitHub Actions deployment checks branch-specific Azure auth before deploy and uses repository variables `STAGE_REPO_URL` and `ADO_REPO_URL` plus secrets `STAGE_REPO_TOKEN` and `ADO_REPO_PAT` for optional mirror publishing.
- Missing mirror-publish variables or secrets emit warnings and skip only the affected publish job, matching the sibling repo behavior.
- Mirror snapshots exclude `.git/` and `.github/`, preventing source workflows and Dependabot configuration from running in staging repositories.
- A focused pipeline walkthrough is available in [docs/PIPELINE.md](docs/PIPELINE.md).
- Architecture details live in [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md), and the Easy Auth access-control note lives in [docs/aadsso.md](docs/aadsso.md).
- The package remains Windows-friendly because it includes `web.config`, but the pipeline can deploy to either Windows or Linux App Service depending on the target app's OS.
- Broader App Service options such as Deployment Center and custom containers are documented in [docs/DEPLOYMENT_METHODS.md](docs/DEPLOYMENT_METHODS.md), but they are not implemented by this repo.
- This repo is already well-documented; this README is kept as the current snapshot rather than a placeholder.
- Pre-commit enforces file hygiene, conflict detection, large-file prevention, and private-key detection.
- Dependabot monitors NuGet and GitHub Actions dependencies weekly.
- Generated build outputs are excluded from version control; CI rebuilds artifacts from source.
