DO $$ BEGIN
    CREATE ROLE platform_authentication;
EXCEPTION
    WHEN duplicate_object THEN null;
END $$;
