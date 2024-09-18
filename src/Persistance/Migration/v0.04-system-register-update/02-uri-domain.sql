-- Domain: system_register.uri
CREATE DOMAIN business_application.uri AS text CONSTRAINT uri_scheme_check CHECK (value ~ '^https?:\/\/[^.]+');