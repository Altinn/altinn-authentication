# Altinn Authentication — Documentation

Documentation for the **Altinn Platform Authentication** component: the service that authenticates users, systems and organisations accessing the Altinn 3 platform, and that issues Altinn JSON Web Tokens (JWT).

These docs are written for **both humans and AI coding agents** — the conceptual pages below are the single source of truth. Agents should also read [`../AGENTS.md`](../AGENTS.md) for operational conventions and known landmines.

## Map

| Doc | What it covers |
| --- | --- |
| [architecture.md](architecture.md) | The big picture: components, dependencies, where things live in the codebase |
| [flows/oidc-authorization-server.md](flows/oidc-authorization-server.md) | Browser sign-in: the OIDC authorization-server flow and upstream callback |
| [flows/token-exchange.md](flows/token-exchange.md) | Exchanging an external token (ID-porten / Maskinporten / Altinn Studio) for an Altinn JWT |
| [flows/sessions-and-cookies.md](flows/sessions-and-cookies.md) | Sessions, the cookie model, refresh, and logout |
| [flows/system-user.md](flows/system-user.md) | System users (Systembruker): system register, request/approval, agent delegation |
| [operations.md](operations.md) | Configuration, feature flags, secrets, health, observability, runbook |
| [development.md](development.md) | Local setup, build, run, and the (Docker-backed) test strategy |
| [adr/](adr/) | Architecture Decision Records — the **why** behind the design |

## Where to start

- **New to the service?** Read [architecture.md](architecture.md), then the flow that matches your task.
- **Changing an auth flow?** Read the relevant [ADR](adr/) first — several behaviours that look removable are load-bearing (or were deliberately kept).
- **Operating it?** [operations.md](operations.md).
- **An agent picking up a task?** [`../AGENTS.md`](../AGENTS.md) + the relevant ADR.

## Conventions

- Diagrams are [Mermaid](https://mermaid.js.org/) — they render on GitHub and diff cleanly in PRs. No binary images.
- Decisions are recorded as immutable, numbered [ADRs](adr/); a decision is never edited, only superseded.
- Keep these docs updated **in the same PR** as the change that invalidates them.
