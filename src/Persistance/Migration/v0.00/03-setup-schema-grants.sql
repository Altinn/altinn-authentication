GRANT USAGE ON SCHEMA altinn_authentication TO platform_authentication;
GRANT SELECT,INSERT,UPDATE,REFERENCES,DELETE,TRUNCATE,TRIGGER ON ALL TABLES IN SCHEMA altinn_authentication TO platform_authentication;
GRANT ALL ON ALL SEQUENCES IN SCHEMA altinn_authentication TO platform_authentication;
