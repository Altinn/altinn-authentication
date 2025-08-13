-- ALTER TABLE business_application.change_request

ALTER TABLE business_application.change_request
ADD COLUMN required_accesspackages jsonb;

ALTER TABLE business_application.change_request
ADD COLUMN unwanted_accesspackages jsonb;