# ADR-0004: Remove the Altinn 2 / SBL Bridge integration

- **Status:** Accepted
- **Date:** 2026-06-30

## Context

Altinn Authentication depended on the Altinn 2 backend (the **"SBL Bridge"**) for several things: cookie/ticket-based authentication, enterprise-user (`virksomhetsbruker`) authentication, self-identified credential validation, and user/profile lookups. Altinn 2 was **shut down in 2026**, making all of those integrations dead or non-functional. The work was tracked under umbrella issue [#2030](https://github.com/Altinn/altinn-authentication/issues/2030).

## Decision

We will remove the Altinn 2 / SBL Bridge integration entirely, in incremental, independently-shippable PRs, replacing each path with its Altinn-3 equivalent and deleting the feature flags that selected between them. Specifically:

- **Enterprise-user auth** → permanently `410 Gone` (replaced by system users / ID-porten).
- **Logout** → always redirect to `BaseUrl` (dropped the SBL logout redirect).
- **Cookie-ticket decryption / A2-ticket auth** → removed.
- **SI provisioning + credential validation** → Register-only + local ([ADR-0003](0003-register-is-canonical-for-user-and-org-lookup.md)).
- **ID-porten exchange user lookup** → Register-only ([ADR-0003](0003-register-is-canonical-for-user-and-org-lookup.md)).
- **Legacy browser sign-in branches** → collapsed to the authorization-server flow ([ADR-0002](0002-authorization-server-is-the-live-auth-path.md)).
- Removed dead config: `BridgeAuthnApiEndpoint`, `BridgeProfileApiEndpoint`, `SBLRedirectEndpoint`, and all SBL-decommission feature flags.

## Consequences

- The SBL Bridge HTTP surface and all six decommission feature flags are gone; the Altinn-3 behaviour is now permanent and unconditional.
- **Deliberately kept:** the `SblAuthCookieName` / `SblAuthCookieEnvSpecificName` cookie *names* and the legacy-cookie **delete** logic (`DeleteLegacySblCookies` / `BuildLegacySblCookieDeletes`). These still run on logout/session creation to **drain stale Altinn 2 cookies** from users' browsers. Do not remove this until the team decides those cookies are fully drained.
- A few latent issues were exposed (not introduced) by becoming the sole path — e.g. the Register-outage→`401` behaviour ([#2072](https://github.com/Altinn/altinn-authentication/issues/2072)).

## References

- Umbrella: [#2030](https://github.com/Altinn/altinn-authentication/issues/2030).
- PRs: #2063, #2065, #2066, #2067, #2068, #2070, #2073.
- Related: [ADR-0002](0002-authorization-server-is-the-live-auth-path.md), [ADR-0003](0003-register-is-canonical-for-user-and-org-lookup.md).
