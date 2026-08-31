# Flow: OIDC authorization server (browser sign-in)

**Entry point:** `GET authentication/api/v1/authentication`
**Upstream callback:** `GET authentication/api/v1/upstream/callback`
**Code:** `AuthenticationController.AuthenticateUser`, `OidcFrontChannelController`, `OidcServerService`

This is the stateful, browser-facing face of the service. An unauthenticated user is redirected to an upstream identity provider (ID-porten by default, or FEIDE/UIDP), proofs their identity there, and is sent back; the service then establishes an Altinn **session**, sets cookies, and redirects the user to where they wanted to go (`goTo`).

> **Important:** this is the **only** live browser sign-in path. Production runs `EnableOidc=true`, `ForceOidc=true` and `AuthorizationServerEnabled=true`, so the legacy non-authorization-server branches have been removed. See [ADR-0002](../adr/0002-authorization-server-is-the-live-auth-path.md).

## The happy path

```mermaid
sequenceDiagram
    participant U as User (browser)
    participant App as Altinn App / Arbeidsflate
    participant Auth as Authentication (authorization server)
    participant IdP as Upstream IdP (ID-porten / FEIDE / UIDP)

    U->>App: open protected page
    App->>Auth: redirect to GET /authentication?goto=...
    Note over Auth: goTo is validated by IsSafeSameOrSubdomainHttps (open-redirect guard)
    alt already has a valid Altinn session/cookie at the requested level
        Auth-->>U: 302 redirect to goTo (reuse session)
    else not authenticated (or step-up needed)
        Auth->>Auth: AuthorizeUnregisteredClient (pick provider, build state/nonce/PKCE)
        Auth-->>U: 302 redirect to IdP /authorize
        U->>IdP: authenticate
        IdP-->>U: 302 redirect to GET /upstream/callback?code=...&state=...
        U->>Auth: GET /upstream/callback
        Auth->>IdP: exchange code -> id_token (+ validate issuer/nonce)
        Auth->>Auth: resolve user, create session, set cookies
        Auth-->>U: 302 redirect to goTo
    end
```

## Steps in detail

1. **`GET /authentication?goto=<url>`** (`AuthenticateUser`):
   - Validates `goTo` with `IsSafeSameOrSubdomainHttps` — an **open-redirect guard** that only allows an absolute `https` URL whose host equals or is a subdomain of the service host, with no embedded credentials. Anything else redirects to `BaseUrl`. *(This guard is intentional; the CodeQL "URL redirection" alerts on the subsequent redirects are dismissed false positives.)*
   - Sets `no-store`/`no-cache` headers (auth responses must never be cached).
   - If the user already has a valid session (auth cookie or Altinn session cookie) that meets the requested `acr` level, redirects straight to `goTo`.
   - Otherwise calls `OidcServerService.AuthorizeUnregisteredClient`, which selects the upstream provider (from the `iss` query param, the requested `acr`, or the configured default `idporten`), builds the upstream authorize URL (state, nonce, PKCE S256), persists an upstream login transaction, and redirects the browser to the IdP.

2. **Upstream callback** — **`GET /upstream/callback`** (`OidcFrontChannelController` → `OidcServerService.HandleUpstreamCallback`):
   - Looks up the persisted upstream transaction by `state`.
   - Exchanges the `code` for the upstream `id_token`, validates it (issuer + nonce) via `UpstreamTokenValidator`.
   - Resolves/provisions the Altinn user (from Register), creates an Altinn **session** and the cookie set, and redirects to the client / `goTo`.
   - If the upstream token exchange fails (refused, unreachable, or a body that is not a token response) or the returned tokens do not validate, sign-in stops **fail-closed**: no session is created, and the user is sent back to the downstream client with `error=temporarily_unavailable`. Where there is no validated `redirect_uri` to return to — the unregistered-client (`goto`) flow — it is a local `502` instead, because bouncing back to the `goto` URL without a session would start another login attempt. Both causes are counted on `altinn.authentication.oidc.upstream_token_exchange` / `…upstream_token_validation` (see [operations.md](../operations.md#custom-metrics)).
   - Other failures return a `LocalError` (e.g. `500`) rather than establishing a partial session.

3. **`acr_values` / step-up:** the entry point accepts an optional space-separated `acr_values` query parameter (allowed values validated by `AuthenticationHelper.TryParseAcrValues`). If the existing session does not meet the requested level, the user is re-authenticated upstream at the higher level.

## Registered downstream clients

Beyond the "unregistered client" browser flow above, the service is also a small OIDC provider for **registered** downstream clients (e.g. Arbeidsflate) via `GET /authorize` + `POST /token` (`OidcFrontChannelController` / `OidcTokenController`), with discovery at `GET /openid/.well-known/openid-configuration` and keys at `.../jwks`.

## acr_values

The accepted set is **derived from the configured ID-providers** rather than hardcoded: it is the union of every `AuthLevels[].Acr` declared under `OidcProviders`, plus ID-porten's built-in vocabulary. See `IAcrValueCatalog` / `OidcAcrValueCatalog`. Any other value yields `400 Bad Request`.

With ID-porten alone that set is `idporten-loa-substantial`, `idporten-loa-high` and `selfregistered-email`; the legacy `level0` / `level1` / `level2` values are still accepted but **deprecated**.

These acr values are **Altinn-facing**. They are what a client requests, what is stored on the session, and what is emitted in the `acr` claim — deliberately not the upstream provider's vocabulary. Each configured level carries its own `UpstreamAcrValues`, which is what gets sent to that provider's authorize endpoint, so one provider's vocabulary is never forwarded to another.

### Providers outside ID-porten's conventions

A provider whose token does not follow ID-porten's claim names or values is described in configuration, not in code:

| Setting | Purpose |
|---|---|
| `ClaimMappings` | Which claim carries pid / level / method / email. Defaults to `pid`, `acr`, `amr`, `email`. |
| `AuthLevels` | The levels the provider offers: Altinn-facing `Acr`, normalised `Level`, `UpstreamAcrValues` to request it, and the `ClaimValues` that come back for it. |
| `AuthMethodMappings` | Provider method-claim values to Altinn's `AuthenticationMethod`. |
| `DefaultUpstreamAcrValues` | What to send when the client requested no level. |

Declaring `AuthLevels` is what makes a provider reachable via `acr_values`; providers without it do not take part in acr routing and are selected by the `iss` parameter instead.

Step-up compares **normalised levels**, not acr strings (`AuthenticationHelper.NeedAcrUpgrade`), so a session from one provider can satisfy a level requested in another's vocabulary. A session whose acr resolves to no level does not satisfy a request above level 0.

Acr values declared by a provider must be unique across the whole configuration, and only `idporten` may declare ID-porten's built-in values. Violations fail at startup rather than being resolved by configuration order, which would silently route existing clients to a different ID-provider.

### Trust equivalence between providers — open policy question

Comparing normalised levels means **equal numeric levels from different providers are treated as interchangeable**. A `helseid-loa-high` session (level 4) satisfies a request for `idporten-loa-high` (level 4) without a step-up.

This is a deliberate consequence of normalising, and it is what makes step-up work at all for a provider that does not emit `acr` — but it is a *trust* decision, not a technical one, and it has **not been ratified**. It deserves scrutiny because HelseID federates onward: a HelseID level-4 session may be an ID-porten BankID authentication underneath, or it may come from a regional health IdP such as Helse Midt-Norge or Helseplattformen. Whether those are equivalent for an Altinn client that asked specifically for ID-porten level 4 is a question for the auth team and, plausibly, for legal.

Until that is settled, treat cross-provider level equivalence as unratified. See [#2163](https://github.com/Altinn/altinn-authentication/issues/2163).

### Not yet enforced: the level actually achieved

The requested `acr_values` are used to pick the provider, to build the upstream request, and to decide whether an *existing* session needs a step-up. They are persisted on the login transaction, but **nothing compares them against the level actually achieved once the user returns from the upstream provider.** A client can request level 4, be authenticated at level 0, and receive a session with no error. This predates configurable providers and applies to every provider.

## Related

- The session + cookie mechanics, refresh, and logout: [sessions-and-cookies.md](sessions-and-cookies.md).
- The API token-exchange face: [token-exchange.md](token-exchange.md).
