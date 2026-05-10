# Pipeline Guide

This document describes the primary Azure DevOps pipeline in `azure-pipelines.yml` and the alternate manual pipeline in `run_from_package.yml`.

## Primary pipeline

The main delivery path for this repo is `azure-pipelines.yml`.

### Trigger behavior

- CI runs on pushes to `main`
- PR validation runs for pull requests targeting `main`
- The `Deploy` stage is skipped for pull request builds

### Runner and shared template

- The pipeline runs on the `IaCRunner` pool
- It requires a Linux agent with `Agent.OS -equals Linux`
- It imports shared runner hygiene steps from the `IaC/template` repository through the `tf_tpl` repository resource

### Branch-to-environment mapping

The YAML includes environment and subscription mappings for `main`, `dev`, and `sbx`, even though only `main` is currently triggered automatically in this pipeline.

| Branch | `env` | `subscription` | Service connection |
| --- | --- | --- | --- |
| `main` | `prod` | `prod` | `sc-ccoe-iac-devops-prod` |
| `dev` | `dev` | `nonprod` | `sc-ccoe-iac-devops-nonprod` |
| `sbx` | `sbx` | `sbx` | `sc-ccoe-iac-devops-sbx` |

### Key variables

| Variable | Purpose |
| --- | --- |
| `projectFile` | Project to restore and publish: `WebCcoeDemo.Web.csproj` |
| `buildConfiguration` | Build mode: `Release` |
| `webAppNamePrimary` | Required primary App Service target |
| `webAppNameSecondary` | Optional second App Service target |
| `webAppNameThird` | Optional third App Service target |
| `publishDir` | Publish output folder in the pipeline workspace |
| `packagePath` | ZIP artifact path: `app.zip` |
| `dotnetSdkVersion` | .NET SDK version: `8.0.x` |
| `linuxDotnetRuntimeStack` | Linux runtime stack: `DOTNETCORE|8.0` |

## Stage flow

### 1. Build stage

The `Build` stage:

1. Installs the .NET 8 SDK
2. Restores packages using `NuGet.config`
3. Runs `dotnet publish` in `Release`
4. Archives the published output into `app.zip`
5. Verifies that `web.config` exists at the ZIP root
6. Publishes the ZIP as the `drop` artifact

The `web.config` validation matters because the package is intended to stay deployable to Windows App Service as well as Linux App Service.

### 2. Deploy stage

The `Deploy` stage runs only when:

- the `Build` stage succeeded
- the pipeline run is not a pull request

The deploy job then:

1. Checks whether each configured App Service target exists
2. Stores the discovered subscription ID and resource group for each valid target
3. Skips blank or missing optional targets safely
4. Downloads the `drop/app.zip` artifact when at least one target is deployable
5. Runs ZIP deployment to each valid target

## Deployment behavior

### Target discovery

Before deployment, the pipeline resolves each target with Azure CLI:

- `webAppNamePrimary`
- `webAppNameSecondary`
- `webAppNameThird`

For each target:

- blank values are skipped with a warning
- missing App Services are skipped with a warning
- valid targets have their subscription ID and resource group captured into pipeline variables

This makes the primary target effectively required for a normal release, while keeping the secondary and third targets optional.

### Connectivity preflight

Before a ZIP push, the pipeline checks the App Service SCM/Kudu endpoint:

- DNS resolution for `<app-name>.scm.azurewebsites.net`
- TCP connectivity to port `443`

If the build agent cannot resolve or reach the SCM endpoint, deployment fails before the ZIP upload starts.

### OS detection and runtime handling

For each valid target, the pipeline checks whether the App Service is Linux or Windows:

- if Linux, it sets `linux-fx-version` to `DOTNETCORE|8.0`
- if Windows, it leaves the runtime stack unchanged

### App settings cleanup and metadata

Before deploying, the pipeline removes settings that can interfere with this deployment model:

- `WEBSITE_RUN_FROM_PACKAGE`
- `SCM_DO_BUILD_DURING_DEPLOYMENT`
- `ENABLE_ORYX_BUILD`

It then sets:

- `APP_SERVICE_NAME`
- `APP_SERVICE_SUBSCRIPTION_ID`
- `APP_SERVICE_RESOURCE_GROUP`

These values give the app runtime easy access to its own hosting metadata.

### ZIP deployment method

The main pipeline clears any linked source-control deployment configuration and then deploys with:

- `az webapp deployment source config-zip`

This means the pipeline ships the already-published app artifact to App Service and does not rely on App Service to build the project from source.

## Alternate manual pipeline

`run_from_package.yml` is the alternate pipeline.

Key differences:

- CI and PR triggers are disabled with `none`
- it is intended for manual or explicitly-invoked use
- it sets `WEBSITE_RUN_FROM_PACKAGE=1`
- it deploys the published ZIP so App Service runs directly from the mounted package

Use this pipeline when immutable package mounting is the goal. Use `azure-pipelines.yml` when standard ZIP deployment is the preferred operating model.

## Operational notes

- If deployment is skipped unexpectedly, check whether the run was a pull request build
- If no app is updated, verify that `webAppNamePrimary` is set correctly and that the target exists in Azure
- If deployment fails before upload, check SCM/Kudu DNS and outbound `443` access from the runner
- If Linux startup fails, confirm the target is compatible with `DOTNETCORE|8.0`
- If behavior suggests package mounting, verify that `WEBSITE_RUN_FROM_PACKAGE` was removed for the main pipeline

## Related files

- `azure-pipelines.yml`
- `run_from_package.yml`
- `docs/DEPLOYMENT_METHODS.md`
- `README.md`
