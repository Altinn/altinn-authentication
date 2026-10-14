GRANT USAGE ON SCHEMA business_application TO auth_authentication;
GRANT SELECT,INSERT,UPDATE,REFERENCES,DELETE,TRUNCATE,TRIGGER ON ALL TABLES IN SCHEMA business_application TO auth_authentication;
GRANT ALL ON ALL SEQUENCES IN SCHEMA business_application TO auth_authentication;
