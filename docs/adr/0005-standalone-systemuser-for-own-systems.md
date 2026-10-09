# ADR-0005: Standalone SystemUserType for an organisation's own systems

- **Status:** Accepted
- **Date:** 2026-09-02

## Context

The System Register / system user model assumes a vendor/customer split: a **vendor** registers a
system (`systemregister/vendor`, declaring the `Rights`/`AccessPackages` it needs), and a **customer**
either approves a vendor-initiated request or, for an already-registered system, self-serves a
system user via `systemuser/{party}/create`. Both paths always point at a pre-existing Registered
System.

Organisations that build a system purely for their own internal use don't fit that shape: they are
simultaneously the vendor and the customer. Requiring them to register a system, then send themselves
a request, then approve it, is pointless ceremony — approving your own request is a no-op. They also
don't know in advance which `Rights`/`AccessPackages` they'll need; they want to create the system
user first and delegate whatever's necessary afterwards through the Access Management UI, the same
way any other delegation works.

## Decision

Add a third `SystemUserType`, `Standalone`, and a single endpoint, `POST systemuser/own`, that creates
the Registered System and the SystemUser **together, in one call**:

- The caller must already hold the Maskinporten `altinn:authentication/systemregister.write` scope —
  the same gate as the existing vendor-facing System Register API. This is **not** relaxed to a
  browser-session flow; the organisation calls it machine-to-machine, the same way vendors call the
  existing System Register API today.
- The vendor org number and Maskinporten `ClientId` are read from the caller's own token (the
  `consumer` and `client_id` claims) rather than supplied in the request body, since the calling
  organisation is both the vendor and the customer of the system it's creating.
- The created Registered System has empty `Rights`/`AccessPackages` and `IsVisible = false`, so it
  never appears in another org's "pick a system" catalog browsing.
- No request/approve lifecycle runs. The SystemUser is pushed to Access Management immediately (as
  every system user is), making it a valid delegation target right away.

## Consequences

- Rights/access packages for a standalone system user are **only** ever delegated after creation,
  through Access Management's normal delegation flow — there is no "required rights" concept for this
  type, and the detail endpoint (`GET systemuser/{party}/{systemUserId}`) reads delegations live from
  Access Management (`GetListOfDelegationsForStandardSystemUser`) rather than from anything declared
  on the Registered System.
- A `client_id` can only ever back one Registered System (`DoesClientIdExists`); calling
  `systemuser/own` again with the same token's `client_id` returns `409 Conflict`
  (`SystemRegister_ClientId_AlreadyExists`) rather than silently creating a duplicate or returning the
  existing system user. A genuine retry should mint a fresh Maskinporten client if a new standalone
  system is actually intended.
- `SystemUserType.Standalone` is a new Postgres enum value on `business_application.systemuser_type`
  (migration `v0.30-standalone-systemuser`), added via its own `ALTER TYPE ... ADD VALUE` so it isn't
  referenced by other DDL/DML in the same transaction it's added in.

## References

- Flow doc: [docs/flows/system-user.md](../flows/system-user.md).
