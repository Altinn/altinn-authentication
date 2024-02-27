--Table: altinn_authentication.system_integration
CREATE TABLE IF NOT EXISTS altinn_authentication.system_register
(
	hidden_internal_id uuid DEFAULT gen_random_uuid() PRIMARY KEY,
	registered_system_id varchar(255) NOT NULL,
	friendly_product_name varchar(255),
	system_vendor varchar(255) NOT NULL,
	default_rights text array,
	is_deleted bool default False,
	created timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
	UNIQUE (registered_system_id)
)

TABLESPACE pg_default;


