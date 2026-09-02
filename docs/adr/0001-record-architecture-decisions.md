# ADR-0001: Record architecture decisions

- **Status:** Accepted
- **Date:** 2026-06-30

## Context

This service carries a lot of implicit knowledge — which code paths are live vs. dead, why certain "removable-looking" behaviour is load-bearing, what production assumes about configuration. That knowledge lived only in people's heads and in scattered PR discussions, and was repeatedly and expensively re-derived (including during the Altinn 2 decommission, where confirming "is this branch actually dead?" gated every change). Both human contributors and AI coding agents need the *why*, not just the *what*.

## Decision

We will record significant architectural and behavioural decisions as **Architecture Decision Records** in `docs/adr/`, following the lightweight format in [template.md](template.md). ADRs are immutable and superseded rather than edited. Decisions that affect how an auth flow behaves, what is intentionally kept/removed, or what production configuration is assumed, are in scope.

## Consequences

- Contributors and agents have a durable, greppable record of *why*, reducing re-derivation and accidental regressions.
- A small ongoing cost: a decision-worthy change should add or supersede an ADR in the same PR.
- The first ADRs (0002–0004) backfill the most important recent decisions.

## References

- `docs/` documentation set
- Tech-debt tracking: [#2074](https://github.com/Altinn/altinn-authentication/issues/2074)
