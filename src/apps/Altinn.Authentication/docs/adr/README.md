# Architecture Decision Records (ADRs)

An ADR captures a single architectural decision: its **context**, the **decision**, and its **consequences**. ADRs explain the *why* — the thing source code and git history don't preserve and that humans and agents otherwise re-derive at great cost.

## Rules

- ADRs are **immutable**. Once accepted, a decision is never edited — if it changes, write a **new** ADR that supersedes the old one and mark the old one `Superseded by ADR-XXXX`.
- Numbered sequentially, zero-padded: `0001`, `0002`, …
- Keep them short (one page). Link to code/PRs/issues for detail.
- Use [template.md](template.md) for new ADRs.

## Index

| ADR | Title | Status |
| --- | --- | --- |
| [0001](0001-record-architecture-decisions.md) | Record architecture decisions | Accepted |
| [0002](0002-authorization-server-is-the-live-auth-path.md) | The OIDC authorization server is the only live browser sign-in path | Accepted |
| [0003](0003-register-is-canonical-for-user-and-org-lookup.md) | Register is the canonical source for user/party/org lookup | Accepted |
| [0004](0004-sbl-bridge-altinn2-decommission.md) | Remove the Altinn 2 / SBL Bridge integration | Accepted |
