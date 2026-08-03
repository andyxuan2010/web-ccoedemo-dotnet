# Security Policy

## Supported version

Security fixes are applied to the `main` branch. Deployments should use the latest
successful build from `main`.

## Reporting a vulnerability

Do not open a public issue for a suspected vulnerability or include credentials,
tokens, tenant data, or user claims in an issue.

Report vulnerabilities through the repository's GitHub **Security** tab using
private vulnerability reporting. If private reporting is unavailable, contact the
CCOE-Azure repository administrators through an approved internal channel.

Include the affected revision, reproduction steps, impact, and any suggested
mitigation. The maintainers will acknowledge the report, assess severity, and
coordinate remediation and disclosure.

## Operational security

- Store application and deployment secrets in Azure Key Vault, GitHub environment
  secrets, or Azure DevOps secret variables; never commit them.
- Prefer workload identity federation/OIDC over long-lived client secrets.
- Protect `main` with required reviews and successful build/security checks when
  the GitHub plan supports branch protection.
- Treat authentication claims and pipeline logs as sensitive operational data.
