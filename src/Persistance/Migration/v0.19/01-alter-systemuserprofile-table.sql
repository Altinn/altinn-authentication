-- ALTER TABLE business_application.system_userprofile

-- Enum: business_application.systemuser_type
CREATE TYPE business_application.systemuser_type AS ENUM(
  'default',
  'agent'
);

ALTER TABLE business_application.system_user_profile
ADD COLUMN systemuser_type business_application.systemuser_type NOT NULL default 'default'::business_application.systemuser_type;

ALTER TABLE business_application.request
ADD COLUMN systemuser_type business_application.systemuser_type NOT NULL default 'default'::business_application.systemuser_type;

ALTER TABLE business_application.request_archive
ADD COLUMN systemuser_type business_application.systemuser_type NOT NULL default 'default'::business_application.systemuser_type;

UPDATE business_application.system_user_profile 
SET systemuser_type = 'agent' 
WHERE system_user_type = 'Agent';

UPDATE business_application.request
SET systemuser_type = 'agent' 
WHERE system_user_type = 'Agent';

UPDATE business_application.request_archive
SET systemuser_type = 'agent' 
WHERE system_user_type = 'Agent';

ALTER TABLE business_application.system_user_profile
DROP COLUMN system_user_type;

ALTER TABLE business_application.request
DROP COLUMN system_user_type;

ALTER TABLE business_application.request_archive
DROP COLUMN system_user_type;