# Pipeline Guide

This document describes the active Azure DevOps pipeline in `azure-pipelines.yml`, the shared deploy-stage template in `azure-pipelines/deploy-stage.yml`, and the alternate manual pipeline in `run_from_package.yml`.

## Primary Azure DevOps pipeline

The main Azure DevOps delivery path for this repo is `azure-pipelines.yml`.

### Trigger behavior

- CI runs on pushes to `main`
- PR validation runs for pull requests targeting `main`
- Deploy stages are skipped for pull request builds

### Runner and shared template

- The pipeline runs on the Microsoft-hosted `Azure Pipelines` pool
- It uses `ubuntu-latest`
- It imports shared runner hygiene steps from the `IaC/template` repository through the `templates` repository resource

### Key variables

| Variable | Purpose |
| --- | --- |
| `projectFile` | Project to restore and publish: `WebCcoeDemo.Web.csproj` |
| `buildConfiguration` | Build mode: `Release` |
| `webAppNameSecondary` | Optional second App Service target |
| `publishDir` | Publish output folder in the pipeline workspace |
| `packagePath` | ZIP artifact path: `app.zip` |
| `dotnetSdkVersion` | .NET SDK version: `8.0.x` |
| `linuxDotnetRuntimeStack` | Linux runtime stack: `DOTNETCORE|8.0` |

## Stage flow

### 1. Build

The `Build` stage:

1. Installs the .NET 8 SDK
2. Restores packages using `NuGet.config`
3. Audits direct and transitive NuGet dependencies for known vulnerabilities
4. Builds in `Release` with .NET analyzers enabled
5. Runs `dotnet publish` in `Release`
6. Archives the published output into `app.zip`
7. Verifies that `web.config` exists at the ZIP root
8. Publishes the ZIP as the `drop` artifact

The `web.config` validation matters because the package remains deployable to Windows App Service as well as Linux App Service.

### 2. DeploySandbox

`DeploySandbox` depends on `Build`.

It uses:

- service connection `sc-platform-sbx`
- primary target `web-platform-cc-sbx-dotnet`

### 3. DeployDev

`DeployDev` depends on `DeploySandbox`, which keeps the Azure DevOps flow aligned with the sibling `landingzone` sequencing pattern.

It uses:

- service connection `sc-platform-dev`
- primary target `web-platform-eus-dev-dotnet`

## Deployment behavior

The deploy stages are generated from `azure-pipelines/deploy-stage.yml`.

Each deploy stage:

1. Checks the primary target
2. Checks the optional secondary target
3. Downloads `drop/app.zip` only when at least one target is deployable
4. Runs SCM/Kudu DNS and TCP `443` preflight checks
5. Detects whether the target App Service is Linux or Windows
6. Removes `WEBSITE_RUN_FROM_PACKAGE`, `SCM_DO_BUILD_DURING_DEPLOYMENT`, and `ENABLE_ORYX_BUILD`
7. Sets `APP_SERVICE_NAME`, `APP_SERVICE_SUBSCRIPTION_ID`, and `APP_SERVICE_RESOURCE_GROUP`
8. Clears any linked source-control deployment config
9. Deploys with `az webapp deployment source config-zip`

### Target behavior

- `webAppNamePrimary` is the expected main deployment target for the stage
- `webAppNameSecondary` is optional
- blank or missing optional targets are skipped safely
- `webAppNameThird` still exists as a top-level variable in some workflows, but the shared Azure DevOps deploy template does not consume it

### Connectivity preflight

Before a ZIP push, the pipeline checks the App Service SCM/Kudu endpoint:

- DNS resolution for `<app-name>.scm.azurewebsites.net`
- TCP connectivity to port `443`

If the runner cannot resolve or reach the SCM endpoint, deployment fails before the ZIP upload starts.

## GitHub Actions workflow

This repo also includes `.github/workflows/azure-webapp.yml`.

The workflow is aligned with the current Azure DevOps structure in these ways:

- runs on `main`
- uses separate deployment precheck, build, tag, deploy, and mirror-publish jobs
- uses GitHub environment-scoped Azure credentials
- grants read-only repository access by default and write access only to the tag job
- cancels superseded runs for the same branch and applies job timeouts
- audits NuGet dependencies and builds with analyzers before packaging
- keeps semantic tag creation aligned with the Azure DevOps logic
- publishes clean snapshots to optional GitHub and Azure DevOps mirror repos when credentials are present
- deploys with `az webapp deploy --type zip`, so the GitHub Actions deploy mechanism differs slightly from the Azure DevOps `config-zip` path

## Alternate manual pipeline

`run_from_package.yml` is the alternate pipeline.

Key differences:

- CI and PR triggers are disabled with `none`
- it is intended for manual or explicitly-invoked use
- it sets `WEBSITE_RUN_FROM_PACKAGE=1`
- it deploys the published ZIP so App Service runs directly from the mounted package

Use this pipeline when immutable package mounting is the goal. Use `azure-pipelines.yml` when standard ZIP deployment is the preferred operating model.

## Operational notes

- If a deploy stage is skipped unexpectedly, check whether the run was a pull request build
- If no app is updated, verify that the stage-specific primary App Service name matches the target environment
- If deployment fails before upload, check SCM/Kudu DNS and outbound `443` access from the runner
- If Linux startup fails, confirm the target is compatible with `DOTNETCORE|8.0`
- If behavior suggests package mounting, verify that `WEBSITE_RUN_FROM_PACKAGE` was removed for the main pipeline

## Related files

- `azure-pipelines.yml`
- `azure-pipelines/deploy-stage.yml`
- `run_from_package.yml`
- `.github/workflows/azure-webapp.yml`
- `docs/DEPLOYMENT_METHODS.md`
