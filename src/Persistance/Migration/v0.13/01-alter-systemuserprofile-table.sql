-- ALTER TABLE business_application.system_userprofile

ALTER TABLE business_application.system_user_profile
ADD COLUMN system_user_type character varying(50);

ALTER TABLE business_application.request
ADD COLUMN system_user_type character varying(50);

ALTER TABLE business_application.request
ADD COLUMN accesspackages jsonb;
