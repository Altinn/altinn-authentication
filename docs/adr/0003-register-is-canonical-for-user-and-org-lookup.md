# ADR-0003: Register is the canonical source for user/party/org lookup

- **Status:** Accepted
- **Date:** 2026-06-30

## Context

Resolving a person/organisation to their Altinn identifiers (`userId`, `partyId`, `partyUuid`, username) historically went through the Altinn 2 **SBL Bridge** profile API. As part of the Altinn 2 decommission ([ADR-0004](0004-sbl-bridge-altinn2-decommission.md)), that lookup was first moved behind feature flags with two live Altinn-3 sources:

- **Register** — `POST /register/api/v2/internal/parties/query` (via `PartiesClient`).
- The platform **Profile API** — used as a fallback.

For self-identified (SI) user provisioning during sign-in there was similarly a Register path vs. an SBL path.

The Profile API path was only ever a safety net in case Register did not work.

## Decision

We will use **Register as the single, canonical source** for ID-porten token-exchange user lookup and for SI user provisioning, and remove the Profile-API fallback and the associated feature flags (`IdPortenUserLookupFromRegister`, `RegisterSelfIdentifiedUserProvisioning`). SI **credential validation** is performed locally (`LocalSelfIdentifiedCredentialValidation` removed; the local path is permanent, used to link old SI profiles to email users).

## Consequences

- One code path, no flag — simpler and matching production behaviour.
- The id-porten exchange now depends entirely on Register availability. A Register **outage** currently surfaces as `401` (because `PartiesClient` returns `null` for both "not found" and infra failures, and the controller maps `null` → `Unauthorized`). This is a known gap tracked in [#2072](https://github.com/Altinn/altinn-authentication/issues/2072) — it should return `5xx` for infra failures.
- `IUserProfileService.GetUser`/`CreateUser` (the last SBL profile calls) were removed once unused.

## References

- PRs #2067 (SI provisioning + local validation) and #2068 (id-porten exchange).
- Follow-up: [#2072](https://github.com/Altinn/altinn-authentication/issues/2072).
