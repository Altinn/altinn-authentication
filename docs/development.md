# Development

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- A container runtime — **required for the integration tests** (they spin up PostgreSQL via Testcontainers). Either [Docker](https://www.docker.com/get-docker) or [Podman](https://podman.io/) works; see [Container runtime](#container-runtime) below.
- Git, and a code editor / IDE.

## Build & run

```bash
# Build the whole solution
dotnet build Altinn.Platform.Authentication.sln

# Run the service locally
cd src/Authentication
dotnet run
# Swagger UI: http://localhost:<port>/authentication/swagger
```

Local runtime config comes from `appsettings.Development.json`; secrets should come from user-secrets, not committed files.

## Tests

```bash
# Unit + integration tests (a container runtime MUST be running)
dotnet test test/Altinn.Platform.Authentication.Tests/Altinn.Platform.Authentication.Tests.csproj
```

- **Integration tests need a container runtime.** Most controller tests run through a `WebApplicationFixture` backed by a Testcontainers PostgreSQL instance, applying migrations on startup. They are slow but cover real request/DB behaviour.
- **A green local `dotnet build` is not enough.** Compilation passing does not mean the tests pass. The authoritative signal is the CI **Build and Test** job (it runs the container-backed suite). When you can't run containers locally, push and watch CI.

### Container runtime

**Podman works as a drop-in for Docker** — Testcontainers talks to its Docker-compatible API, and no Docker CLI is needed. The usual failure is simply that the machine is not running:

```bash
podman machine list     # is there a machine, and is it up?
podman machine start
```

On Windows and macOS, `podman machine start` sets up Docker API forwarding (on Windows, `npipe:////./pipe/docker_engine`) and Testcontainers picks it up with no further configuration — rootless mode is fine, including the Ryuk resource reaper. On Linux, point Testcontainers at Podman's socket:

```bash
export DOCKER_HOST="unix://$(podman info --format '{{.Host.RemoteSocket.Path}}')"
```
- `test/.../SystemIntegrationTests` and `.../PerformanceTests` are **opt-in** suites that hit deployed environments / load — not part of the normal loop.

### Testing patterns

- Pure logic (claim mapping, ACR parsing, scope checks, helpers) should be unit-tested directly — see `AuthenticationHelperTests`.
- Outbound clients should be tested with a mocked `HttpMessageHandler` — see `AccessManagementClientTests` for the pattern.
- The Register contract types (`Altinn.Register.Contracts`) are easiest to build via JSON deserialization in tests. ⚠️ Gotcha: a `PartyUser` with a `userId` set **must** also set `userIds` (e.g. `"userIds": [ <id> ]`), or its constructor throws at runtime.

## Migrations

SQL migrations live under `src/Persistance/Migration/` in versioned directories (`v0.xx-...`), plus lifecycle folders (`_pre`, `_post`, `_init`, `_erase`, `_draft`). They are applied automatically when the test/host database starts.

## Conventions

- **Branch per change**; never commit straight to `main`.
- Conventional-ish commit subjects (`chore:`, `fix:`, `docs:`, `test:`); reference the issue/PR.
- Keep public APIs documented with XML doc comments.
- Update the relevant doc/ADR **in the same PR** as a behaviour change.

See [`../AGENTS.md`](../AGENTS.md) for the agent-facing version of these conventions, including the known landmines.
