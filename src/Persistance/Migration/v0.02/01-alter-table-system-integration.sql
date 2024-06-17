--Table: altinn_authentication_integration.system_integration
ALTER TABLE altinn_authentication_integration.system_user_integration
DROP CONSTRAINT fk_integrations_clientids;

ALTER TABLE altinn_authentication_integration.system_user_integration
DROP COLUMN client_id; -- foreign key to client_id table 




