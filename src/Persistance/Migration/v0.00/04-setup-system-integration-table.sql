--Table: altinn_authentication_integration.system_integration
CREATE TABLE IF NOT EXISTS altinn_authentication_integration.system_user_integration
(
	system_user_integration_id uuid DEFAULT gen_random_uuid() PRIMARY KEY,
	integration_title varchar(255) NOT NULL,
	product_name varchar(255) NOT NULL, --foreign key to system_register.registered_system_id
	owned_by_party_id varchar(255) NOT NULL,
	supplier_name varchar(255),
	supplier_org_no varchar(255),
	client_id uuid, -- foreign key to client_id table 
	is_deleted bool default False,
	created timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
	UNIQUE (owned_by_party_id, product_name),
	CONSTRAINT fk_integrations_registeredsystems
	FOREIGN KEY (product_name)
	REFERENCES altinn_authentication_integration.system_register (registered_system_id)
	ON DELETE CASCADE,
	CONSTRAINT fk_integrations_clientids
	FOREIGN KEY (client_id)
	REFERENCES altinn_authentication.maskinporten_client (client_id)
	ON DELETE NO ACTION
)

TABLESPACE pg_default;


