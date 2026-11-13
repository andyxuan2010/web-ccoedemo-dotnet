# Azure App Service Easy Auth + Entra ID SSO: Access Control Layers

When you use Azure App Service Authentication (Easy Auth) with Microsoft Entra ID SSO, access is controlled by **three layers**.
Users can access the site only when all three layers are aligned.

## 1. Enterprise Application Access Control (Tenant Level)

This layer controls who is allowed to sign in to the application in Entra ID.

### Portal Path

`Azure Portal` -> `Microsoft Entra ID` -> `Enterprise Applications` -> `<Your Application>` -> `Properties`

### Key Setting

- **Assignment required?**
  - `Yes`: only users/groups assigned under `Enterprise Applications -> Users and groups` can sign in.
  - `No`: any authenticated user in the tenant can sign in.

### For "everyone in the enterprise can access"

- Set **Assignment required = No**

## 2. App Service Authentication Authorization Rules

Even if Entra ID sign-in succeeds, App Service can still block access.

### Portal Path

`App Service` -> `Authentication` -> `Edit`

### Check These Settings

- **Unauthenticated requests**
  - Recommended: `Require authentication`
- **Authorization**
  - Should be: `Allow access to any authenticated user`
  - If set to `Allow specific users/groups`, you must explicitly list users/groups.

## 3. Application-Level Authorization (Inside Your Code)

Even if Entra ID and App Service allow access, your app code can still deny it.

### Typical Examples

- **Dotnet (imperative check)**
  ```csharp
  if (!User.IsInRole("Admin"))
  {
      return Forbid();
  }
  ```

- **.NET**
  ```csharp
  [Authorize(Roles = "Admin")]
  ```

### Symptom

- Authentication succeeds, but app returns:
  - `403 Forbidden`
  - or `You do not have permission to view this directory or page`

## How to Identify Which Layer Is Blocking Access

### Step 1: Check Entra Sign-in Logs

Path: `Entra ID` -> `Sign-in logs`

- If login shows **Success**, Entra ID allowed access.

### Step 2: Check Easy Auth Token Endpoint

Open:

`https://<app>.azurewebsites.net/.auth/me`

Expected output (example):

```json
{
  "provider_name": "aad",
  "user_id": "...",
  "user_claims": []
}
```

- If this works but your app page fails, the issue is likely in application code.

### Step 3: Check App Service Logs

Enable and observe:

`App Service` -> `Log stream`

Then access the site and inspect real-time logs.

## Recommended Enterprise Pattern

Instead of granting broad tenant-wide access directly, many enterprises use:

- **Authentication**: tenant allowed
- **Authorization**: security group based

Example group:

- `app-website-users`

### Benefits

- Least privilege
- Better conditional access control
- Auditable membership

## Security Note

If access is set to "everyone in enterprise":

- Any compromised employee account can gain access
- Role separation is weaker
- Governance is harder

Common enterprise default:

- **Default**: deny
- **Access**: explicit security group membership
