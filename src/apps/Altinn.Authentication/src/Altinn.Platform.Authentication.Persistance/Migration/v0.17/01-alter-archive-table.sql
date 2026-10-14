-- ALTER TABLE business_application.system_register

ALTER TABLE business_application.request_archive
ADD COLUMN IF NOT EXISTS system_user_type character varying(50);

ALTER TABLE business_application.request_archive
ADD COLUMN IF NOT EXISTS accesspackages jsonb;