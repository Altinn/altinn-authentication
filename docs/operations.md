# Operations

How the service is configured, observed, and operated.

## Configuration

Configuration is standard ASP.NET Core layered config: `appsettings.json` (base) → `appsettings.{Environment}.json` → environment variables / Key Vault. Settings are bound into option classes, the largest being `GeneralSettings` (`src/Authentication/Configuration/GeneralSettings.cs`).

Notable settings:

| Setting | Meaning |
| --- | --- |
| `GeneralSettings:OidcRefreshTokenPepper` | **Secret.** Server-side pepper for hashing session handles / refresh tokens. Required; supply via Key Vault / env. |
| `GeneralSettings:JwtCookieName`, `SblAuthCookieName`, `AltinnSessionCookieName`, … | Cookie names (see [flows/sessions-and-cookies.md](flows/sessions-and-cookies.md)). |
| `GeneralSettings:EnableOidc`, `ForceOidc`, `AuthorizationServerEnabled` | All **true** in every environment; the authorization-server flow is the only live path (see [ADR-0002](adr/0002-authorization-server-is-the-live-auth-path.md)). |
| `GeneralSettings:MaskinportenWellKnownConfigEndpoint`, `IdPortenWellKnownConfigEndpoint` (+ `*Alternative*`) | Upstream discovery endpoints used to fetch signing keys. |
| `PlatformSettings:Api*Endpoint` | URLs of the Altinn platform dependencies (Register, Access Management, Profile, …). |
| `kvSetting` / Key Vault | Source of the JWT **signing certificates**. |

> ⚠️ The base `appsettings.json` currently pins concrete AT22-test endpoints and contains placeholder secrets (`CertificatePwd`, an Azurite key, DB passwords). Moving secrets out of source and making base config environment-neutral are tracked in [issue #2074](https://github.com/Altinn/altinn-authentication/issues/2074).

## Feature flags

Uses `Microsoft.FeatureManagement`; flags are defined in `src/Authentication/Configuration/FeatureFlags.cs`.

| Flag | Effect |
| --- | --- |
| `AuditLog` | When on, authentication events are written to the audit queue. **Note: off by default in base/prod config** — confirm this is intended. |
| `SystemUser` | Gates the system-user controller. Effectively always on. |

The SBL-decommission flags (`EnterpriseUserAuthenticationDisabled`, `CookieTicketDecryptionDisabled`, `RegisterSelfIdentifiedUserProvisioning`, `LocalSelfIdentifiedCredentialValidation`, `IdPortenUserLookupFromRegister`, `Altinn2LogoutRedirectDisabled`) have all been **removed** — the Altinn-3 behaviour is now permanent (see [ADR-0004](adr/0004-sbl-bridge-altinn2-decommission.md)).

## Secrets & certificates

- **JWT signing certs** come from Azure Key Vault. The newest certificate that has been valid for at least the configured *rollover delay* is used to sign, so new certs propagate to verifiers (via JWKS) before they sign. Public keys are published at `GET /openid/.well-known/openid-configuration/jwks`.
- **Pepper** (`OidcRefreshTokenPepper`) and DB credentials must come from secret stores, not committed config.

## Health & observability

- **Health:** `GET /health` — currently **liveness only** (always returns healthy). Readiness/dependency checks (PostgreSQL, Key Vault, the audit queue, upstream well-known endpoints) are a known gap; see [issue #2074](https://github.com/Altinn/altinn-authentication/issues/2074).
- **Telemetry:** OpenTelemetry tracing + metrics across all assemblies, exported to Application Insights (`Azure.Monitor.OpenTelemetry.AspNetCore`).
- **Logging:** structured logging configured per category under `Logging:LogLevel`.
- **Audit:** authentication events → Azure Storage Queue via `EventLogService` (gated by `AuditLog`).

## Runbook — common situations

| Symptom | Likely cause / where to look |
| --- | --- |
| Users get `401` on `exchange/id-porten` despite valid login | Could be a **Register outage** mis-mapped to `401` (see [#2072](https://github.com/Altinn/altinn-authentication/issues/2072)). Check Register health and the logs around `PartiesClient`. |
| Token-exchange failures after a deploy | Check whether the upstream **issuer/JWKS** config changed, and whether the **signing certificate** rolled (a too-fresh or expired cert breaks verification). |
| Notifications/emails (SI link) going to the wrong environment | Per-environment `PlatformSettings__ApiNotificationsEndpoint` override missing (base default is AT22). |
| Audit events missing | `AuditLog` flag off, or the (currently fire-and-forget) audit write was dropped under load — see [#2074](https://github.com/Altinn/altinn-authentication/issues/2074). |
| `scan` CI job red on every PR | Known: the archived `Azure/container-scan` action flags base-image CVEs. Not a regression — see [#2074](https://github.com/Altinn/altinn-authentication/issues/2074). |

## CI/CD

GitHub Actions: **Build and Test** (Docker-backed integration tests), **Analyze**/**Analyze (csharp)** (CodeQL), **SonarCloud**, **CodeRabbit**, and **scan** (container scan). The Docker-backed *Build and Test* job is the authoritative gate — a green local `dotnet build` is **not** sufficient (see [development.md](development.md)).
