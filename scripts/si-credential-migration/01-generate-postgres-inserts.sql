/* ============================================================================
   Manual SI-user credential migration  —  Altinn 2 (SQL Server)  ->  Altinn 3 (Postgres)
   Issue: #2025

   WHAT THIS DOES
   --------------
   Run this query against the Altinn 2 SQL Server [AuthenticationDB]. Each output
   row is a complete Postgres INSERT statement for
   oidcserver.selfidentified_user_credential. Save the result set as text and run
   it against the Altinn 3 Postgres database.

   This is a *generator*: it does not modify any data. It only SELECTs and emits text.

   HOW TO RUN (SSMS)
   -----------------
   1. Query > Results To > Results to Text            (Ctrl+T)
   2. Tools > Options > Query Results > SQL Server > Results to Text:
        - Maximum number of characters displayed in each column = 8192
   3. Query > Query Options > Results > Text:
        - Uncheck "Include column headers in the result set"
   4. Execute, then copy the output into a .sql file (e.g. inserts.generated.sql).
   5. Run that file against Postgres (psql -f inserts.generated.sql).

   IDEMPOTENCY
   -----------
   Generated statements use ON CONFLICT (user_id) DO NOTHING, so re-running the
   import will not error on rows that already exist.

   FILTER (matches issue #2025 / #2007 task C):
     - UserTypeID = 2   -> self-identified only (NOT 1=SSN, NOT 3=enterprise)
     - statusId   = 1   -> active users only
     - username/password/salt present
   Remove the passwordExpiry filter intentionally: expired rows are imported and
   the expiry policy is enforced in Altinn 3 (see issue "Open decisions").

   COLUMN MAPPING
   --------------
     AUTHN_UserProfile.UserUUID_AK  -> party_uuid
     AUTHN_UserProfile.uid          -> user_id  AND  altinn2_user_id
     AUTHN_UserProfile.username     -> user_name
     AUTHN_UserProfile.password     -> password_hash   (Base64 SHA1, verbatim)
     AUTHN_UserProfile.salt         -> salt            (Base64, verbatim)
     AUTHN_UserProfile.passwordExpiry -> password_expiry (UTC -> ...Z)
   ============================================================================ */

SET NOCOUNT ON;

SELECT
    'INSERT INTO oidcserver.selfidentified_user_credential '
  + '(party_uuid, user_id, user_name, password_hash, salt, password_expiry, is_active, altinn2_user_id) VALUES ('
  + '''' + LOWER(CONVERT(varchar(36), up.UserUUID_AK)) + ''', '
  + CONVERT(varchar(20), up.uid) + ', '
  + '''' + REPLACE(up.username, '''', '''''') + ''', '
  + '''' + REPLACE(up.password, '''', '''''') + ''', '
  + '''' + REPLACE(up.salt,     '''', '''''') + ''', '
  + '''' + CONVERT(varchar(23), up.passwordExpiry, 126) + 'Z'', '
  + 'TRUE, '
  + CONVERT(varchar(20), up.uid)
  + ') ON CONFLICT (user_id) DO NOTHING;'  AS pg_insert
FROM
    [AuthenticationDB].[dbo].[AUTHN_UserProfile] AS up
WHERE
    up.UserTypeID = 2
    AND up.statusId = 1
    AND up.username IS NOT NULL
    AND up.password IS NOT NULL
    AND up.salt IS NOT NULL
ORDER BY
    up.uid;
