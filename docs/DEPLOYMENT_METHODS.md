# Deployment Methods

This repo implements two Azure DevOps deployment pipelines, one GitHub Actions deployment workflow, and documents two additional Azure App Service alternatives for comparison.

Implemented in this repo:

1. `azure-pipelines.yml`
2. `run_from_package.yml`
3. `.github/workflows/azure-webapp.yml`

Documented alternatives outside this repo:

4. `Deployment Center`
5. `ACR/custom container`

All five can move this app to Azure App Service or support its release flow, but they differ in where the publish artifact is prepared, whether App Service runs a mounted package, and how much of the runtime the platform versus the container owns.

## Language support

The same four broad deployment methods are commonly available across Python, Node.js, and .NET App Service workloads, but support details differ by runtime.

| Runtime | `azure-pipelines.yml` | `run_from_package.yml` | `azure-webapp.yml` | `Deployment Center` | `ACR/custom container` | Recommended default |
| --- | --- | --- | --- | --- | --- | --- |
| Python | Yes | Generally not recommended for App Service Python | Yes | Yes | Yes | `azure-pipelines.yml` |
| Node.js | Yes | Yes | Yes | Yes | Yes | `azure-pipelines.yml` |
| .NET | Yes | Yes | Yes | Yes | Yes | `azure-pipelines.yml` |

Notes:

- .NET is a good fit for both plain ZIP deployment of published output and Run From Package.
- `run_from_package.yml` is useful when you want to mount a published ZIP directly and keep the artifact immutable after deployment.
- In this repo, `run_from_package.yml` has `none` for both CI and PR triggers, so it behaves as a manual or explicitly-invoked pipeline.
- `ACR/custom container` is available when you need full control over the .NET runtime image.

Official references:

- ZIP deploy:
  - https://learn.microsoft.com/en-us/azure/app-service/deploy-zip
- App Service deployment best practices:
  - https://learn.microsoft.com/en-us/azure/app-service/deploy-best-practices
- Run From Package:
  - https://learn.microsoft.com/en-us/azure/app-service/deploy-run-package
- Deployment Center / continuous deployment:
  - https://learn.microsoft.com/en-us/azure/app-service/deploy-continuous-deployment
- Custom containers on App Service:
  - https://learn.microsoft.com/en-us/azure/app-service/configure-custom-container
- CI/CD for custom containers:
  - https://learn.microsoft.com/en-us/azure/app-service/deploy-ci-cd-custom-container

## Methods at a glance

| Method | Main idea | Build location | Startup control | Artifact style | Best fit |
| --- | --- | --- | --- | --- | --- |
| `azure-pipelines.yml` | Publish app in pipeline, deploy ZIP output | Pipeline before deploy | App Service runtime config | Published ZIP | Standard .NET App Service deployment |
| `run_from_package.yml` | Publish package first, mount it read-only | Pipeline before deploy | App Service runtime config | Published ZIP package | Immutable package promotion and release-artifact workflows |
| `azure-webapp.yml` | Build in GitHub Actions, deploy ZIP output, optionally mirror repo snapshots | GitHub Actions before deploy | App Service runtime config plus workflow gating | Published ZIP and optional clean repo snapshot | GitHub-based CI/CD with optional GitHub and ADO mirror publishing |
| `Deployment Center` | App Service pulls from repo and deploys from portal-connected source | App Service or external workflow after repo sync | App Service config or auto-detect | Repo contents or workflow output | Quick setup and portal-managed deployment |
| `ACR/custom container` | Build and push a container image, then have App Service run that image | Pipeline before deploy | Container entrypoint and App Service container settings | OCI container image | Full runtime control and portable app packaging |

## Side-by-side comparison

| Topic | `azure-pipelines.yml` | `run_from_package.yml` | `azure-webapp.yml` | `Deployment Center` | `ACR/custom container` |
| --- | --- | --- | --- | --- | --- |
| Deployment trigger | Auto on `main`, `dev`, and `sbx`, with PR validation on those branches | Manual by default in this repo | Auto on `main`, `dev`, and `sbx`, plus optional manual deploy toggle on workflow dispatch | Portal sync or connected source trigger | Pipeline run or container image update |
| Source of truth | YAML pipeline in repo | YAML pipeline in repo | GitHub Actions workflow in repo plus repo variables and secrets | Mix of repo and App Service portal settings | Dockerfile, image build, registry config, and pipeline |
| Package contents | `dotnet publish` output | `dotnet publish` output mounted read-only | `dotnet publish` output as ZIP artifact; optional clean repo snapshot mirrors | Raw repo contents or workflow-produced package | Full container filesystem |
| Build step | `dotnet publish` in pipeline | `dotnet publish` in pipeline | `dotnet publish` in GitHub Actions | Depends on portal/workflow configuration | Installed during image build |
| App setting pattern | Removes `WEBSITE_RUN_FROM_PACKAGE`, `SCM_DO_BUILD_DURING_DEPLOYMENT`, and `ENABLE_ORYX_BUILD`; sets app metadata | Sets `WEBSITE_RUN_FROM_PACKAGE=1`, removes build-during-deploy settings, and sets app metadata | Removes `WEBSITE_RUN_FROM_PACKAGE`, `SCM_DO_BUILD_DURING_DEPLOYMENT`, and `ENABLE_ORYX_BUILD`; sets app metadata; mirror publish uses GitHub variables/secrets | Depends on portal configuration | Container registry/image settings and optional app settings |
| Startup behavior | Default ASP.NET Core hosting behavior | Default ASP.NET Core hosting behavior from mounted package | Default ASP.NET Core hosting behavior | Must be manually configured or auto-detected | Usually Docker `CMD`/`ENTRYPOINT` |
| Mutability at runtime | Normal App Service content layout | Mounted package is read-only | Normal App Service content layout | Normal App Service content layout | Container image is immutable |
| Windows/Linux handling | Detects target OS and sets `DOTNETCORE|8.0` only for Linux targets | Detects target OS and sets `DOTNETCORE|8.0` only for Linux targets | Detects target OS and sets `DOTNETCORE|8.0` only for Linux targets | Depends on how each target is configured | Depends on container base image and App Service container settings |

## 1. `azure-pipelines.yml`

This is the pipeline-driven method where the pipeline runs `dotnet publish`, packages the published output into a ZIP, and deploys that ZIP to App Service.

### How it works

1. The pipeline restores packages and runs `dotnet publish`.
2. The pipeline archives the publish output into `app.zip`.
3. The pipeline validates that `web.config` exists at the package root.
4. The pipeline resolves up to three App Service targets and skips any optional target that is blank or not found.
5. The pipeline checks SCM/Kudu DNS and TCP 443 connectivity before each deployment.
6. The pipeline detects whether the target App Service is Linux or Windows.
7. The pipeline removes `WEBSITE_RUN_FROM_PACKAGE`, `SCM_DO_BUILD_DURING_DEPLOYMENT`, and `ENABLE_ORYX_BUILD`.
8. The pipeline sets `APP_SERVICE_NAME`, `APP_SERVICE_SUBSCRIPTION_ID`, and `APP_SERVICE_RESOURCE_GROUP`.
9. The pipeline clears linked source-control config and deploys the ZIP with `az webapp deployment source config-zip`.

### Pros

- Good default fit for App Service-hosted .NET apps.
- Publish output is created in a controlled pipeline build.
- Keeps deployment behavior in version-controlled YAML.
- Works cleanly across Windows and Linux App Service targets.
- Supports one required target plus one optional extra target in the shared Azure DevOps deploy stage.

### Cons

- Requires SCM/Kudu reachability from the runner.
- Artifact is published per deployment run rather than mounted as an immutable package by default.

## 2. `run_from_package.yml`

This is the immutable package approach, where the pipeline publishes the app first, then configures App Service to run directly from the mounted ZIP package.

### How it works

1. The pipeline runs `dotnet publish`.
2. The pipeline archives the publish output into `app.zip`.
3. The deploy stage is manual by default because CI and PR triggers are set to `none`.
4. The deploy stage resolves each configured App Service target and skips blank or missing targets.
5. The pipeline checks SCM/Kudu DNS and TCP 443 connectivity before deployment.
6. The deploy stage sets `WEBSITE_RUN_FROM_PACKAGE=1`.
7. The pipeline applies the Linux runtime stack only when the target App Service is Linux.
8. App Service mounts the ZIP package and runs the app from that package.

### Pros

- Strong artifact immutability story.
- Clear promotion path for a single published artifact across environments.
- Good fit when you want mounted-package semantics.

### Cons

- Read-only package layout may affect assumptions about writable content.
- Deployment troubleshooting still depends on SCM/Kudu reachability and App Service startup behavior.

## 3. `.github/workflows/azure-webapp.yml`

This is the GitHub Actions deployment path. It follows the sibling app pipeline pattern with separate build, deployment precheck, deploy, pre-publish check, and mirror-publish jobs.

### How it works

1. The `build-package` job restores packages, runs `dotnet publish`, archives the output into `app.zip`, and uploads it as a workflow artifact.
2. The `deployment-precheck` job validates branch-specific Azure deploy inputs before the deploy job is allowed to run.
3. The `deploy` job runs only for non-PR events, only when deploy auth is present, and only when manual workflow dispatch has `deploy=true`.
4. The deploy job logs into Azure with `azure/login@v2`, downloads the artifact, resolves up to three App Service targets, checks SCM/Kudu connectivity, and performs ZIP deploy with `az webapp deploy`.
5. The `pre-publish-check` job validates GitHub Actions repository variables `STAGE_REPO_URL` and `ADO_REPO_URL`, plus secrets `STAGE_REPO_TOKEN` and `ADO_REPO_PAT`.
6. If a mirror-publish variable or secret is missing, the workflow emits a warning and skips only that publish job, matching the sibling repo behavior.
7. When configured, `publish-stage-repo` and `publish-ado-repo` push clean snapshot mirrors to GitHub and Azure DevOps target repos.

### Required GitHub Actions configuration

- Deploy authentication:
  - `AZURE_CLIENT_ID_PROD`, `AZURE_TENANT_ID_PROD`, `AZURE_SUBSCRIPTION_ID_PROD`
  - `AZURE_CLIENT_ID_NONPROD`, `AZURE_TENANT_ID_NONPROD`, `AZURE_SUBSCRIPTION_ID_NONPROD`
  - `AZURE_CLIENT_ID_SBX`, `AZURE_TENANT_ID_SBX`, `AZURE_SUBSCRIPTION_ID_SBX`
- Mirror publish:
  - Repository variables: `STAGE_REPO_URL`, `ADO_REPO_URL`
  - Repository secrets: `STAGE_REPO_TOKEN`, `ADO_REPO_PAT`

### Pros

- Fits teams standardizing on GitHub Actions instead of Azure DevOps.
- Uses the same stage and condition pattern as sibling app repos.
- Supports optional mirror publishing without blocking the main app deployment when mirror credentials are missing.
- Keeps deploy and publish logic under source control.

### Cons

- Requires more GitHub-side configuration than the Azure DevOps pipeline files.
- Branch-based Azure authentication setup is more verbose because each environment has separate secrets.

## 4. `Deployment Center`

This is the portal-managed path where App Service is connected directly to a source repository or branch and deployment is driven through App Service Deployment Center.

### Pros

- Quick to set up from the Azure portal.
- No Azure DevOps packaging pipeline required.

### Cons

- Higher configuration drift risk.
- Harder to guarantee consistency across environments.
- Less deployment behavior is captured in source-controlled YAML.

## 5. `ACR/custom container`

This is the container-based path where the application is packaged as a Docker image, pushed to Azure Container Registry, and run by App Service as a custom container.

### Pros

- Maximum runtime control.
- Good when you need exact .NET runtime control, OS packages, or a portable image.
- The same image can be reused outside App Service.

### Cons

- More operational overhead than code-based deployment methods.
- You must maintain the Dockerfile, base image, patching, and registry lifecycle.

## Recommendation for this repo

Recommended default: `azure-pipelines.yml`

Reasons:

- It is the best operational fit for a .NET App Service workload like this one.
- It keeps deployment behavior under source control.
- It works cleanly across Windows and Linux App Service targets.
- It uses standard published output without forcing mounted-package semantics.
- It already includes the repo's multi-target deployment checks and app-setting cleanup flow.

Use `run_from_package.yml` when immutable package behavior is a deliberate requirement.

Use `Deployment Center` only when simplicity matters more than reproducibility and strict environment consistency.

Use `ACR/custom container` when runtime control is more important than deployment simplicity.
