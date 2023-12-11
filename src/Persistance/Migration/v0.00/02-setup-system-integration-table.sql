--Table: altinn_authentication.system_integration
CREATE TABLE IF NOT EXISTS altinn_authentication.system_user_integration
(
	system_user_integration_id uuid DEFAULT gen_random_uuid() PRIMARY KEY,
	integration_title varchar(255) NOT NULL,
	product_name varchar(255) NOT NULL,
	owned_by_party_id varchar(255) NOT NULL,
	supplier_name varchar(255),
	supplier_org_no varchar(255),
	client_id varchar(255),
	is_deleted bool default False,
	created timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
	UNIQUE (owned_by_party_id, product_name)
)

TABLESPACE pg_default;


