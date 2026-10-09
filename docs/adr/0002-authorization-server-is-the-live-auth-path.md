# ADR-0002: The OIDC authorization server is the only live browser sign-in path

- **Status:** Accepted
- **Date:** 2026-06-30

## Context

`AuthenticationController.AuthenticateUser` historically contained several browser sign-in branches gated by three settings:

- `EnableOidc` — OIDC sign-in enabled at all.
- `ForceOidc` — OIDC is the default (no `iss` required).
- `AuthorizationServerEnabled` — use the modern authorization-server flow (vs. an older OIDC-redirect-here flow, vs. a legacy Altinn 2 SBL redirect).

The branches included: an OIDC **code-exchange handled at this endpoint**, an older **redirect-to-OIDC-provider** branch, and an `EnableOidc=false` **SBL redirect** fallback. In the authorization-server model the upstream callback goes to `GET /upstream/callback` — never back to `/authentication` with a `code` — so the code-exchange branch was unreachable in production.

Production runs **`EnableOidc=true`, `ForceOidc=true`, `AuthorizationServerEnabled=true` in every environment** (confirmed with the team), which makes all of those legacy branches dead.

## Decision

We will treat the **authorization-server flow as the single live browser sign-in path** and collapse `AuthenticateUser` to it, removing the legacy code-exchange / OIDC-redirect / SBL-redirect branches and the dead helpers, settings usage (`sblRedirectUrl`/`SBLRedirectEndpoint`), and the legacy `IUserProfileService.GetUser`/`CreateUser` provisioning path they pulled in.

## Consequences

- `AuthenticateUser` is now a single, readable flow (see [flows/oidc-authorization-server.md](../flows/oidc-authorization-server.md)); ~1400 lines of dead code/tests removed.
- This **depends on the three settings staying true in all environments.** If a new environment ever sets one false, the removed behaviour does not come back — that would need a deliberate redesign.
- The three settings still exist in `GeneralSettings`/appsettings (they were not deleted), but their usage now differs: `ForceOidc` is still read by `OidcServerService`, `AuthorizationServerEnabled` by `LogoutController` (which still has its own legacy branch — a future collapse candidate), while `EnableOidc` is now read **nowhere** and is a dead-property cleanup candidate.

## References

- Implemented in PR #2073 (issue [#2071](https://github.com/Altinn/altinn-authentication/issues/2071)).
- Part of the Altinn 2 decommission, [ADR-0004](0004-sbl-bridge-altinn2-decommission.md).
