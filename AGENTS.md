# AGENTS.md — guide for AI coding agents

Operational guide for AI agents (Claude Code, Cursor, Copilot, …) working in this repo. Humans: see [`README.md`](README.md) and [`docs/`](docs/README.md). Keep this file **short and high-signal** — it is loaded into limited context.

## What this is

Altinn Platform Authentication — an ASP.NET Core (.NET 10) service that authenticates users/orgs/systems and issues Altinn JWTs, and acts as a small OIDC authorization server for browser sign-in. Read [`docs/architecture.md`](docs/architecture.md) first.

## Commands

```bash
# Build
dotnet build Altinn.Platform.Authentication.sln

# Test (Docker MUST be running — Testcontainers PostgreSQL)
dotnet test test/Altinn.Platform.Authentication.Tests/Altinn.Platform.Authentication.Tests.csproj

# Compile-only check of one project (fast)
dotnet build src/Authentication/Altinn.Platform.Authentication.csproj -clp:ErrorsOnly --nologo
```

## Critical workflow rules

- **A green local `dotnet build` is NOT sufficient.** The integration tests need Docker, and compilation passing says nothing about them. The authoritative gate is the CI **Build and Test** job. If you can't run Docker, push and watch CI before claiming done.
- **Branch per change; never commit to `main`.** Commit/push only when asked.
- Update the relevant [`docs/`](docs/README.md) page or ADR **in the same PR** as a behaviour change.
- Respond to CodeRabbit/CodeQL review comments; don't silently ignore them.

## Where things live

- `src/Authentication` — host, controllers, app services, config. `src/Core` — models/interfaces/pure helpers (no I/O). `src/Integration` — outbound clients (Register/`PartiesClient`, Access Management, Profile). `src/Persistance` — Postgres + SQL migrations. `src/jwtcookie` — shared JWT-cookie library.
- Auth flows are documented in [`docs/flows/`](docs/flows/). The **why** behind the design is in [`docs/adr/`](docs/adr/) — **read the relevant ADR before changing an auth flow.**

## Landmines — do NOT "fix" these without understanding

- **Production runs `EnableOidc=true`, `ForceOidc=true`, `AuthorizationServerEnabled=true`** in every environment. The authorization-server flow is the only live browser path ([ADR-0002](docs/adr/0002-authorization-server-is-the-live-auth-path.md)). The checked-in `appsettings.json` says `AuthorizationServerEnabled=false` — that is **not** prod.
- **`IsSafeSameOrSubdomainHttps`** (`AuthenticationController`) is an intentional open-redirect guard. The CodeQL "URL redirection" alerts on the `goTo`/upstream redirects are **dismissed false positives** — don't rewrite the redirects to appease a scanner.
- **`IsValidIssuer`** is *misnamed* (returns `true` for an invalid issuer) but its control flow is **correct**. Fix the name/substring-match if asked, but do not "invert" the logic — that would break it.
- **`SblAuthCookie*` names + the legacy-cookie delete logic are intentionally kept** to drain stale Altinn 2 cookies, even though the rest of the SBL integration is gone ([ADR-0004](docs/adr/0004-sbl-bridge-altinn2-decommission.md)).
- Token-validation in the exchange path disables issuer/audience checks — flagged for review in [#2074](https://github.com/Altinn/altinn-authentication/issues/2074); **verify intent against tests before changing.**

## Test gotchas

- Build Register contract types (`Altinn.Register.Contracts`) via JSON deserialization in tests. A `PartyUser` with `userId` set **must** also set `userIds` (e.g. `"userIds": [ <id> ]`) or its constructor throws at runtime.
- Mock outbound clients with a mocked `HttpMessageHandler` (see `AccessManagementClientTests`); unit-test pure logic directly (see `AuthenticationHelperTests`).

## Known tech debt

A prioritised top-30 lives in [#2074](https://github.com/Altinn/altinn-authentication/issues/2074). Open follow-ups: [#2072](https://github.com/Altinn/altinn-authentication/issues/2072).
