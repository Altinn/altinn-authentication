# SI-user credential migration (Altinn 2 → Altinn 3)

One-off manual data migration of self-identified (SI) user credentials. Tracked in issue #2025.

- **Source:** Altinn 2 SQL Server, `[AuthenticationDB].[dbo].[AUTHN_UserProfile]`
- **Target:** Altinn 3 Postgres, `oidcserver.selfidentified_user_credential`
  (created by migration `src/Persistance/Migration/v0.27/`)

## Procedure

1. **Create the target table** — apply migration `v0.27` to the Altinn 3 Postgres database (runs automatically with the normal migration flow).

2. **Generate the inserts** — run [`01-generate-postgres-inserts.sql`](01-generate-postgres-inserts.sql) against the Altinn 2 SQL Server. Follow the SSMS output settings in the file header (Results to Text, 8192 char column width, no column headers). Save the output as `inserts.generated.sql`.

3. **Import** — run the generated file against Postgres:
   ```
   psql -h <host> -d <db> -U <user> -f inserts.generated.sql
   ```
   Statements use `ON CONFLICT (user_id) DO NOTHING`, so the import is re-runnable.

4. **Verify** — run [`02-verify-after-import.sql`](02-verify-after-import.sql) against Postgres and check: row count matches the source, no duplicate usernames, no empty credential fields. The expired-password count feeds the expiry-policy decision in #2025.

## Notes

- Only `UserTypeID = 2` (self-identified), `statusId = 1` (active) rows are exported — SSN (`1`) and enterprise (`3`) users are excluded.
- `password_hash` and `salt` are copied **verbatim** (Base64). Altinn 3 re-runs the same SHA1+salt verification — no password reset required.
- `passwordExpiry` is emitted as UTC ISO-8601 with a trailing `Z`. Confirm the source column is stored in UTC before importing.
- `email` (source `AUTHN_UserProfile.email`, VARCHAR 400) is exported for the **forgot-password** flow — Altinn 3 emails reset info to this address; it does **not** generate a new password. `NULL` emails are preserved as `NULL`; the verify script counts how many users lack an email. The actual reset-ticket/flow (Altinn 2 `AUTHN_PasswordResetTicket`) is a separate runtime feature, not part of this data migration.
