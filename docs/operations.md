# Operations

How the service is configured, observed, and operated.

## Configuration

Configuration is standard ASP.NET Core layered config: `appsettings.json` (base) → `appsettings.{Environment}.json` → environment variables / Key Vault. Settings are bound into option classes, the largest being `GeneralSettings` (`src/Authentication/Configuration/GeneralSettings.cs`).

Notable settings:

| Setting | Meaning |
| --- | --- |
| `GeneralSettings:OidcRefreshTokenPepper` | **Secret.** Server-side pepper for hashing session handles / refresh tokens. Required; supply via Key Vault / env. |
| `GeneralSettings:JwtCookieName`, `SblAuthCookieName`, `AltinnSessionCookieName`, … | Cookie names (see [flows/sessions-and-cookies.md](flows/sessions-and-cookies.md)). |
| `GeneralSettings:ForceOidc`, `AuthorizationServerEnabled` | The legacy browser-sign-in branches were collapsed (see [ADR-0002](adr/0002-authorization-server-is-the-live-auth-path.md)); `AuthenticateUser` no longer reads these. `ForceOidc` is still used by `OidcServerService`; `AuthorizationServerEnabled` by `LogoutController` (which still has a legacy branch). `EnableOidc` is now unused (dead-property cleanup candidate). |
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
- **Telemetry:** OpenTelemetry tracing + metrics across all assemblies, exported to Application Insights (`Azure.Monitor.OpenTelemetry.AspNetCore`). Custom metrics land in the `customMetrics` table with their dimensions in `customDimensions`; outbound HTTP calls land in `dependencies` (via `AddHttpClientInstrumentation`).
- **Logging:** structured logging configured per category under `Logging:LogLevel`.
- **Audit:** authentication events → Azure Storage Queue via `EventLogService` (gated by `AuditLog`).

### Custom metrics

Metrics classes follow the `IMetrics<T>` pattern from `Altinn.Authorization.ServiceDefaults.Telemetry`: a nested `sealed class Metrics(Meter meter)` obtained through `IMetricsProvider`. The meter name defaults to the declaring assembly, and each assembly is registered once in `AuthenticationHost` via `AddAssemblyMetrics<T>()` — so a new metric in an already-registered assembly needs no host change.

| Metric | Dimensions | Recorded in |
| --- | --- | --- |
| `altinn.authentication.oidc.upstream_token_exchange` | `provider`, `http.response.status_code` (omitted when no response was received), `error.type` | `OidcProviderService` — one per authorization-code-to-token call against the upstream IdP. |
| `altinn.authentication.oidc.upstream_token_validation` | `provider`, `token.type` (`id_token` / `access_token`), `error.type` | `UpstreamTokenValidator` — one per upstream token validated. |
| `altinn.authentication.oidc.sessions_created` | – | `OidcSessionRepository`. |

Both OIDC counters count **successes as well as failures**, deliberately: an alert belongs on the failure *rate*, since an absolute failure count either cries wolf at peak traffic or stays silent at night.

Following the [OpenTelemetry convention](https://opentelemetry.io/docs/specs/semconv/registry/attributes/error/), `error.type` is present **only on failure** — a measurement without it is a success, and that absence is what an alert query filters on. Its values are:

- **Token exchange:** the upstream OAuth error code (`invalid_grant`, `invalid_client`, `invalid_request`, `unauthorized_client`, `unsupported_grant_type`, `invalid_scope`, `server_error`, `temporarily_unavailable`), `invalid_response` for a 2xx that carried no usable `id_token`, the exception type name when no response was received at all (`System.Net.Http.HttpRequestException`, a request timeout, an open circuit breaker), or `_OTHER`.
- **Token validation:** `signing_keys_unavailable`, `signing_key_not_found`, `invalid_signature`, `invalid_issuer`, `expired`, `invalid_token`, `missing_token`, or `_OTHER`.

The upstream error code is restricted to a known allowlist and everything else folded into `_OTHER`, to keep the dimension — and the Application Insights bill — bounded when the upstream invents a new code. The accompanying `http.response.status_code` still narrows down what an `_OTHER` was.

### Alerting on upstream sign-in failures

`invalid_grant` is routinely user-driven (back button, replayed or expired code) and is excluded from the failure count below; watch it as its own, more forgiving alert, since a sudden spike usually means code lifetimes or clocks upstream have shifted.

```kusto
customMetrics
| where name == "altinn.authentication.oidc.upstream_token_exchange"
| extend provider  = tostring(customDimensions["provider"]),
         errorType = tostring(customDimensions["error.type"])   // empty == success
| where provider == "idporten"
| summarize
    failed = sumif(valueSum, isnotempty(errorType) and errorType != "invalid_grant"),
    total  = sum(valueSum)
  by bin(timestamp, 5m)
| where total >= 20          // don't fire on a handful of night-time attempts
| extend failureRate = todouble(failed) / total
```

Suggested rule: failure rate above `0.2`, evaluated every 5 minutes over a 15-minute window, firing after two consecutive breaches. Pair it with a **floor alert** — `sumif(valueSum, isempty(errorType))` at zero for 10 minutes during business hours — because a rate alert is blind to traffic disappearing entirely.

Use **log alerts** (`azurerm_monitor_scheduled_query_rules_alert_v2`) rather than platform metric alerts: metric alerts on custom metrics require "Custom metrics dimension collection" to be enabled on the Application Insights resource, and without it `provider` and `error.type` are stripped. Note that alert rules are **not currently version-controlled** anywhere; the Application Insights resource itself is `azurerm_application_insights.telemetry` in the Altinn authorization infrastructure repo (`infra/deploy/spoke/telemetry.tf`), whose Log Analytics workspace retains 30 days.

## Runbook — common situations

| Symptom | Likely cause / where to look |
| --- | --- |
| Users get `401` on `exchange/id-porten` despite valid login | Could be a **Register outage** mis-mapped to `401` (see [#2072](https://github.com/Altinn/altinn-authentication/issues/2072)). Check Register health and the logs around `PartiesClient`. |
| Token-exchange failures after a deploy | Check whether the upstream **issuer/JWKS** config changed, and whether the **signing certificate** rolled (a too-fresh or expired cert breaks verification). |
| Browser sign-in fails; users bounce back to the client with `temporarily_unavailable` | The upstream token exchange or upstream token validation failed. Split `altinn.authentication.oidc.upstream_token_exchange` by `error.type` and `http.response.status_code`: `invalid_client` = our client secret/registration at the IdP, `invalid_grant` = user-driven or a code-lifetime/clock problem, a 5xx/429 or a missing status code = the IdP is down, rate-limiting us, or the circuit breaker is open. If the exchange metric looks healthy, check `…upstream_token_validation` — `signing_key_not_found` means the IdP rolled its signing keys and our JWKS cache is stale. The matching `LogError`/`LogWarning` carries the upstream `error_description`. |
| Notifications/emails (SI link) going to the wrong environment | Per-environment `PlatformSettings__ApiNotificationsEndpoint` override missing (base default is AT22). |
| Audit events missing | `AuditLog` flag off, or the (currently fire-and-forget) audit write was dropped under load — see [#2074](https://github.com/Altinn/altinn-authentication/issues/2074). |
| `scan` CI job red on every PR | Known: the archived `Azure/container-scan` action flags base-image CVEs. Not a regression — see [#2074](https://github.com/Altinn/altinn-authentication/issues/2074). |

## CI/CD

GitHub Actions: **Build and Test** (container-backed integration tests), **Analyze**/**Analyze (csharp)** (CodeQL), **SonarCloud**, **CodeRabbit**, and **scan** (container scan). The container-backed *Build and Test* job is the authoritative gate — a green local `dotnet build` is **not** sufficient (see [development.md](development.md)).
