ALTER TABLE altinn_authentication_integration.system_user_integration
RENAME COLUMN system_internal_id TO temp;

ALTER TABLE altinn_authentication_integration.system_user_integration
ADD COLUMN IF NOT EXISTS system_internal_id uuid;

UPDATE altinn_authentication_integration.system_user_integration i
SET system_internal_id = r.system_internal_id
FROM altinn_authentication_integration.system_register r 
WHERE r.system_id = i.temp;

ALTER TABLE altinn_authentication_integration.system_user_integration
DROP COLUMN temp;

ALTER TABLE altinn_authentication_integration.system_user_integration
ALTER COLUMN system_internal_id SET NOT NULL;

ALTER TABLE altinn_authentication_integration.system_user_integration
	ADD FOREIGN KEY (system_internal_id)
	REFERENCES altinn_authentication_integration.system_register (system_internal_id)
	ON UPDATE NO ACTION
	ON DELETE NO ACTION;

