-- Update new name column with system_name value

UPDATE business_application.system_register
SET name = hstore('en', system_name::text) || hstore('nb', system_name::text) || hstore('nn', system_name::text);

UPDATE business_application.system_register
SET description = hstore('en', system_name::text) || hstore('nb', system_name::text) || hstore('nn', system_name::text);