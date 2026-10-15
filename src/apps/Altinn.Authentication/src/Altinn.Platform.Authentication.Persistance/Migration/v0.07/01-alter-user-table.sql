-- ALTER TABLE : business_application.system_user_profile

ALTER TABLE business_application.system_user_profile
ADD COLUMN created_by character varying(255);

ALTER TABLE business_application.system_user_profile
ADD COLUMN changed_by character varying(255);

ALTER TABLE business_application.request
ADD COLUMN changed_by character varying(255);