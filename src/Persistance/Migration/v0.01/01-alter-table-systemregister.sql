ALTER TABLE altinn_authentication_integration.system_register
DROP COLUMN system_vendor;

ALTER TABLE altinn_authentication_integration.system_register
ADD COLUMN systemvendor_orgnumber varchar(12);

ALTER TABLE altinn_authentication_integration.system_register
RENAME COLUMN registered_system_id TO system_id;

ALTER TABLE altinn_authentication_integration.system_register
RENAME COLUMN default_rights to rights;

ALTER TABLE altinn_authentication_integration.system_register
RENAME COLUMN friendly_product_name to system_name;

