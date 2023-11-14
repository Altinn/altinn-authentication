GRANT USAGE ON SCHEMA altinn_authentication TO platform_authorization;
GRANT SELECT,INSERT,UPDATE,REFERENCES,DELETE,TRUNCATE,TRIGGER ON ALL TABLES IN SCHEMA altinn_authentication TO platform_authorization;
GRANT ALL ON ALL SEQUENCES IN SCHEMA altinn_authentication TO platform_authorization;
