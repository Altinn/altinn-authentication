-- Domain: system_register.uri
CREATE DOMAIN business_application.uri AS text CONSTRAINT uri_startswith_https CHECK (value ~ '^https?:\/\/[^.]+');