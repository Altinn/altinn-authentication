-- Tables: business_application.request
--		   business_application.request_archive

-- ALTER TABLE business_application.request

ALTER TABLE business_application.request
ADD COLUMN IF NOT EXISTS integration_title varchar(255) NOT NULL DEFAULT '';

ALTER TABLE business_application.request_archive
ADD COLUMN IF NOT EXISTS integration_title varchar(255) NOT NULL DEFAULT '';
