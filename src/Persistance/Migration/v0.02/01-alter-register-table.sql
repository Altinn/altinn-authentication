-- Table: business_application.system_register_resource

-- ALTER TABLE business_application.system_register

ALTER TABLE business_application.system_register
ALTER COLUMN rights TYPE jsonb USING rights[1]::jsonb;

