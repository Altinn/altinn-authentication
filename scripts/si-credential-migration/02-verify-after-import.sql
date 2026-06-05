-- Run against Altinn 3 Postgres after importing the generated INSERTs (issue #2025).

-- Row count imported
SELECT count(*) AS imported_rows
FROM oidcserver.selfidentified_user_credential;

-- Duplicate usernames (must be 0 — uq_si_user_credential_username also guards this)
SELECT user_name, count(*)
FROM oidcserver.selfidentified_user_credential
GROUP BY user_name
HAVING count(*) > 1;

-- Already-expired passwords at import time (input for the expiry-policy decision)
SELECT count(*) AS expired_rows
FROM oidcserver.selfidentified_user_credential
WHERE password_expiry < now();

-- Sanity: rows with empty credential fields (must be 0)
SELECT count(*) AS bad_rows
FROM oidcserver.selfidentified_user_credential
WHERE coalesce(password_hash, '') = '' OR coalesce(salt, '') = '';
