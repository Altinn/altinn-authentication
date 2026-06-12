# Why mockporten is safe to enable in production

This document explains, in detail, why configuring Altinn Authentication to trust
the **mockporten** test identity provider — *even in a production environment* — is
**not dangerous**, and specifically **why it is not possible to log in as a real
person through it**.

> **One-sentence answer.** The production safety does **not** rely on trusting
> mockporten. It relies on an independent gate inside Altinn Authentication that
> rejects any `pid` that is not a *synthetic (Tenor) test* fødselsnummer — and
> real and synthetic fødselsnummer live in **disjoint, non-overlapping** number
> ranges. The worst anyone can achieve through mockporten is to authenticate as a
> fake test person who does not exist in the population register and holds no real
> rights.

---

## 1. The structural invariant real identities can never satisfy

A Norwegian national identity number (fødselsnummer / fnr) is 11 digits:

```
D D M M Y Y I I I K K
└─day─┘ └month┘ └yr┘ └indiv┘ └ 2 mod11 control digits
```

- A **real** person's fnr always encodes a **real calendar month**, so `MM ∈ 01–12`
  (a D-number adds 40 to the *day*, but the **month is still 01–12**).
- A **synthetic** (Tenor / Skatteetaten test) person is marked by **adding 80 to the
  month**, so `MM ∈ 81–92` (real month = `MM − 80`). This is the official synthetic
  test convention.

These two ranges **cannot overlap**. There is no fødselsnummer that is both a
real, register-allocated identity and has a month in 81–92. So a test on
`MM ∈ 81–92` is a **total partition** of the `pid` space:

| Input | Month | Result |
|---|---|---|
| Ordinary real fnr | 01–12 | **rejected** |
| Real D-number | 01–12 (day +40) | **rejected** |
| Synthetic Tenor fnr | 81–92 | allowed |
| Synthetic Tenor D-number | 81–92 (day +40) | allowed |

The whole argument rests on one documented assumption: **the Norwegian population
register never issues a real fnr with month 81–92.** That is the definition of the
synthetic range, so the gate is sound by construction.

---

## 2. The authoritative gate lives in Altinn Authentication, not in mockporten

This is the key point for the "is it dangerous in production?" question.

The trust decision is **not** delegated to the upstream IdP. Altinn Authentication
independently re-derives the identity from the token's `pid` claim and enforces the
synthetic-only rule itself. A provider is marked test-only with one flag:

- [`OidcProvider.RequireSyntheticPid`](../../src/Authentication/Model/OidcProvider.cs) — `default false`. Set `true` only on the
  mockporten provider entry in `OidcProviders` config.

The gate, in [`AuthenticationHelper.GetUserFromToken`](../../src/Authentication/Helpers/AuthenticationHelper.cs):

```csharp
if (provider.RequireSyntheticPid
    && !string.IsNullOrEmpty(userAuthenticationModel.SSN)
    && !SyntheticPersonIdentifier.IsSyntheticTenor(userAuthenticationModel.SSN))
{
    throw new AuthenticationException(
        "pid is not a synthetic (Tenor) identifier; this provider only allows synthetic test persons");
}
```

And the check itself, [`SyntheticPersonIdentifier.IsSyntheticTenor`](../../src/Authentication/Helpers/SyntheticPersonIdentifier.cs):

```csharp
public static bool IsSyntheticTenor(string? pid)
{
    // Validity: 11 digits + mod11 control digits (old and new algorithm),
    // delegated to PersonIdentifier from Altinn.Register.Contracts.
    if (!PersonIdentifier.TryParse(pid, provider: null, out _))
    {
        return false;
    }

    // Synthetic marker: month component has 80 added → MM in 81–92.
    int month = int.Parse(pid!.AsSpan(2, 2), CultureInfo.InvariantCulture);
    return month is >= 81 and <= 92;
}
```

Properties that make this safe:

1. **Fail-closed.** Any malformed, invalid-mod11, wrong-length, or non-synthetic
   number returns `false` → the gate throws → the request **aborts**. It does not
   fall through to a "not authenticated" model that could be mishandled
   downstream; it throws an `AuthenticationException`.
2. **It does not trust the upstream IdP.** Even if mockporten's signing key were
   compromised, or an attacker stood up a malicious IdP impersonating mockporten,
   and it asserted a *real* `pid`, Altinn Authentication re-checks the `pid` here
   and **rejects it**. The upstream is never the authority on whether the identity
   is real.
3. **Covers every identity path.** `GetUserFromToken` is the single choke point
   through which identity is extracted for both the legacy `authentication?goto=`
   flow and the OIDC authorization-server flow, so the gate applies to both.
4. **Default-off.** `RequireSyntheticPid` defaults to `false`, so no existing
   provider (ID-porten, Feide, …) changes behaviour. It is opt-in per provider.

### Precise scope (honest caveat)

The gate keys on the **`pid` claim** (the national identity number). The condition
requires a non-empty `SSN`, so:

- A `pid` present and **real** → **rejected** (the case we care about).
- A `pid` present and **synthetic** → allowed (the intended use).
- **No** `pid` at all → this specific check is skipped — but then there is no
  national identity being asserted, hence no real person to impersonate. In
  practice mockporten's login form always requires a `pid` (see below), so a token
  from mockporten always carries one.

So the precise guarantee is: **for the national-identity (pid) login path, an
ordinary fødselsnummer can never be authenticated through a `RequireSyntheticPid`
provider, regardless of what the upstream IdP claims.**

### How a mockporten token is actually trusted (and why it can't be hijacked)

Before the synthetic gate even runs, a token from mockporten goes through ordinary
OIDC validation in
[`UpstreamTokenValidator`](../../src/Authentication/Services/UpstreamTokenValidator.cs).
The trust is **pinned by server-side configuration**, not by anything in the
request:

1. **Signing keys come from the configured well-known endpoint.**
   [`SigningKeysRetriever`](../../src/Authentication/Services/SigningKeysRetriever.cs)
   → [`ConfigurationMangerHelper`](../../src/Authentication/Helpers/ConfigurationMangerHelper.cs)
   builds a `ConfigurationManager<OpenIdConnectConfiguration>` pointed at
   [`OidcProvider.WellKnownConfigEndpoint`](../../src/Authentication/Model/OidcProvider.cs),
   fetches the discovery document and its `jwks_uri`, and the token signature must
   verify against those keys (`ValidateIssuerSigningKey = true`).
2. **Issuer must match the configured `Issuer`.** `ValidateIssuer = true` with an
   exact `iss` comparison to `OidcProvider.Issuer` (only a trailing-slash
   difference is tolerated). Plus expiry (`ValidateLifetime`, 10 s skew) and, for
   ID tokens, a constant-time `nonce` check.

`WellKnownConfigEndpoint` and `Issuer` are **operator config** (appsettings / Key
Vault) — they are never caller-supplied. So a third party cannot point Altinn at a
different IdP or inject their own keys without compromising the server config. To
forge an accepted token, an attacker must sign with a key in the JWKS that **your
configured URL** publishes. Microsoft's `HttpDocumentRetriever` enforces **HTTPS by
default** (not overridden here), so the discovery doc and `jwks_uri` must be
`https`, blocking in-transit key substitution.

Takeover therefore reduces to controlling the configured host (DNS / domain), its
TLS, or mockporten's signing key / Key Vault. **And even a total breach of that
trust is contained:** the `RequireSyntheticPid` gate (§2) re-derives identity from
the `pid` independently, so even a stolen signing key or hijacked endpoint asserting
a *real* `pid` is still rejected. Conventional OIDC trust prevents takeover; the
synthetic gate caps the blast radius if it ever fails.

---

## 3. Defence in depth — mockporten gates itself too

The production-safety argument above stands on the Altinn-side gate alone. The
mockporten service adds **independent** layers that shrink the attack surface so it
cannot even be used as a token oracle. None of these is load-bearing for the "no
real person" guarantee, but together they mean a misconfiguration would have to
defeat *several* fail-closed controls at once:

1. **Master kill switch** — [`GeneralSettings.TestIdpEnabled`](Mockporten/Configuration/GeneralSettings.cs) (`default false`).
   When off, **every** endpoint short-circuits (`404`) and no token is issued. It
   must be explicitly enabled per environment.
2. **Shared access password** — [`GeneralSettings.TestIdpSharedPassword`](Mockporten/Configuration/GeneralSettings.cs),
   injected from Key Vault, **fail-closed when empty** (refuses all logins). It is
   the right to *use* the IdP at all (not a per-user credential). Validation is
   [constant-time](Mockporten/Services/Implementation/SharedAccessPasswordValidator.cs)
   (`CryptographicOperations.FixedTimeEquals`) with a **global lockout** after
   `SharedPasswordMaxFailures` (default 5) for `SharedPasswordLockoutMinutes`
   (default 15) — because it is a single shared secret, failures are counted
   globally, so it is the one brute-force target and it is rate-limited.
3. **Same synthetic-only gate, applied at issuance** — before minting a code,
   [`AuthorizeController`](Mockporten/Controllers/AuthorizeController.cs) calls
   [`NorwegianIdentityNumber.IsSyntheticTenorPid`](Mockporten/Helpers/NorwegianIdentityNumber.cs)
   (the same `MM ∈ 81–92` + mod11 logic) and returns `access_denied` for a
   non-synthetic `pid`. So mockporten won't even *issue* a token for a real
   identity. The order matters: the shared password is checked **first**, so until
   it is proven the endpoint reveals nothing — no redirect, no distinction between
   a bad password and a bad `pid` — and cannot be used as an unauthenticated
   oracle.
4. **No more `?pid=` shortcut.** The previous query-parameter shortcut that issued
   a code with no interaction was an unauthenticated pid→token oracle and has been
   removed (#1983).
5. **PKCE / PAR support** — code-interception hardening (`RequirePkce`, RFC 9126
   request objects) for the OAuth flow.

---

## 4. What is the worst that can happen?

Assume the strongest realistic attacker: mockporten is enabled, its shared password
leaks, and the attacker can drive its login form freely. They can then mint tokens —
**but only for synthetic (Tenor) test persons** (gate #3 above). And even if they
could forge tokens entirely (compromised signing key) and assert a *real* `pid`,
Altinn Authentication's `RequireSyntheticPid` gate (§2) **rejects** it.

The maximum achievable outcome is therefore: **authenticate as a synthetic test
person.** Such a person:

- does not exist in the real population register,
- is not a real human being, and
- holds no real roles, delegations, or authorizations over any real party.

So there is **no path to a real person's identity, data, or rights** through
mockporten. That is why trusting it — gated by `RequireSyntheticPid` — is safe even
in production.

---

## 5. Configuration checklist for a safe production deployment

- On the **mockporten provider entry** in Altinn Authentication's `OidcProviders`
  config, set **`RequireSyntheticPid: true`**. (This is the load-bearing control.)
- Leave `RequireSyntheticPid` **unset/false** on every real provider.
- On the **mockporten service**: keep `TestIdpEnabled` controlled per environment,
  supply `TestIdpSharedPassword` from Key Vault (never commit it), and prefer
  `RequirePkce: true`.

## References

- Altinn issue **#1409** — synthetic-only gate design.
- Altinn issue **#1983** — Test-IDP hardening (oracle removal, fail-closed gates).
- PR **#1984** — `RequireSyntheticPid` implementation in Altinn Authentication.
