ALTER TABLE altinn_authentication_integration.system_register
DROP COLUMN rights;

ALTER TABLE altinn_authentication_integration.system_register
ADD COLUMN rights jsonb[];