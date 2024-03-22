--Table: altinn_authentication.maskinporten_client
CREATE TABLE IF NOT EXISTS altinn_authentication.maskinporten_client
(
	client_id uuid PRIMARY KEY, --the client_id is created and maintained by Maskinporten. We need this table only as a reference, and to ensure uniqueness
	is_deleted bool default False,
	created timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
	UNIQUE (client_id)
)

TABLESPACE pg_default;


