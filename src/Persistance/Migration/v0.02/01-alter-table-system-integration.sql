﻿--Table: altinn_authentication_integration.system_integration
ALTER TABLE altinn_authentication_integration.system_user_integration
DROP CONSTRAINT fk_integrations_clientids;

ALTER TABLE altinn_authentication_integration.system_user_integration
DROP COLUMN client_id; -- foreign key to client_id table 

ALTER TABLE altinn_authentication_integration.system_user_integration
RENAME COLUMN product_name TO system_internal_id; --foreign key to system_register table.system_internal_id

ALTER TABLE altinn_authentication_integration.system_user_integration
DROP COLUMN supplier_name;

ALTER TABLE altinn_authentication_integration.system_user_integration
DROP COLUMN supplier_org_no;

--Table: altinn_authentication_intgration.system_register
ALTER TABLE altinn_authentication_integration.system_register
RENAME COLUMN hidden_internal_id to system_internal_id;