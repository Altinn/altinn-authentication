# Altinn Platform Authentication

This repository contains the **Altinn Platform Authentication** component. It is responsible for authenticating the users, systems and organisations that access the Altinn 3 platform, and for issuing the Altinn JSON Web Tokens (JWT) that the rest of the platform trusts.

It does two things:

- **Browser sign-in** — a small OIDC authorization server that delegates identity proofing to upstream providers (ID-porten, FEIDE, UIDP) and establishes an Altinn session.
- **Token exchange** — exchanges a trusted external token (ID-porten / Maskinporten / Altinn Studio) for an Altinn JWT.

Read more on docs.altinn.studio:

- [Authentication capabilities](https://docs.altinn.studio/technology/architecture/capabilities/runtime/security/authentication/)
- [Solution components](https://docs.altinn.studio/technology/architecture/components/application/solution/altinn-platform/authentication/)
- [Construction components](https://docs.altinn.studio/technology/architecture/components/application/construction/altinn-platform/authentication/)

## Build status
[![Build status](https://dev.azure.com/brreg/altinn-studio/_apis/build/status/altinn-platform/authentication-master?label=platform/authentication)](https://dev.azure.com/brreg/altinn-studio/_build/latest?definitionId=41)

## Documentation

In-repo documentation lives in **[`docs/`](docs/README.md)**:

| Doc | What |
| --- | --- |
| [docs/architecture.md](docs/architecture.md) | The big picture: components, dependencies, code layout |
| [docs/flows/](docs/flows/) | The auth flows (browser sign-in, token exchange, sessions/cookies) |
| [docs/operations.md](docs/operations.md) | Config, feature flags, secrets, health, runbook |
| [docs/development.md](docs/development.md) | Local setup, build, run, tests |
| [docs/adr/](docs/adr/) | Architecture Decision Records — the *why* |
| [AGENTS.md](AGENTS.md) | Guide for AI coding agents |

## Getting started

### Prerequisites

1. [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
2. [Docker](https://www.docker.com/get-docker) — required to run the integration tests
3. Newest [Git](https://git-scm.com/downloads)
4. A code editor / IDE of your choice

### Run locally

Clone the repo and run the component from `src/Authentication`:

```bash
cd src/Authentication
dotnet run
```

Swagger UI is then available under `http://localhost:<port>/authentication/swagger`.

### Build & test

```bash
dotnet build Altinn.Platform.Authentication.sln
dotnet test test/Altinn.Platform.Authentication.Tests/Altinn.Platform.Authentication.Tests.csproj   # needs Docker
```

See [docs/development.md](docs/development.md) for details — note that a passing local build does **not** substitute for the Docker-backed test suite.

## Contributing

- Branch per change; open a pull request.
- Keep [`docs/`](docs/README.md) / ADRs updated in the same PR as behaviour changes.
- Known tech debt is tracked in [#2074](https://github.com/Altinn/altinn-authentication/issues/2074).
