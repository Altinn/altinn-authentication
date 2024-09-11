-- Domain: system_register.uri
CREATE DOMAIN business_application.uri AS text CONSTRAINT uri_valid CHECK (value ~ '^https?:\/\/$');