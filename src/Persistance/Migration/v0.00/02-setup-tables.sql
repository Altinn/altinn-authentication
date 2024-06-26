-- Table: business_application.maskinporten_client

-- DROP TABLE IF EXISTS business_application.maskinporten_client;

CREATE TABLE IF NOT EXISTS business_application.maskinporten_client
(
    client_id uuid NOT NULL,    
    created timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
    is_deleted boolean DEFAULT false
    CONSTRAINT maskinporten_client_pkey PRIMARY KEY (client_id)
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS business_application.maskinporten_client
    OWNER to auth_authentication_admin;

GRANT ALL ON TABLE business_application.maskinporten_client TO auth_authentication;

GRANT ALL ON TABLE business_application.maskinporten_client TO auth_authentication_admin;

-------------

-- Table: business_application.system_register

-- DROP TABLE IF EXISTS business_application.system_register;

CREATE TABLE IF NOT EXISTS business_application.system_register
(
    system_internal_id uuid NOT NULL DEFAULT gen_random_uuid(),
    system_id character varying(255) COLLATE pg_catalog."default" NOT NULL,
    system_name character varying(255) COLLATE pg_catalog."default",        
    systemvendor_orgnumber character varying(12) COLLATE pg_catalog."default",    
    rights jsonb[],
    client_id text[] COLLATE pg_catalog."default",    
    created timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
    last_changed timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
    is_visible boolean DEFAULT true,
    is_deleted boolean DEFAULT false
    CONSTRAINT system_register_pkey PRIMARY KEY (system_internal_id),
    CONSTRAINT system_register_registered_system_id_key UNIQUE (system_id)
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS business_application.system_register
    OWNER to auth_authentication_admin;

GRANT ALL ON TABLE business_application.system_register TO auth_authentication;

GRANT ALL ON TABLE business_application.system_register TO auth_authentication_admin;

----------

-- Table: business_application.system_user_profile

-- DROP TABLE IF EXISTS business_application.system_user_profile;

CREATE TABLE IF NOT EXISTS business_application.system_user_profile
(
    system_user_profile_id uuid NOT NULL DEFAULT gen_random_uuid(),
    integration_title character varying(255) COLLATE pg_catalog."default" NOT NULL,    
    system_internal_id uuid NOT NULL,
    reportee_party_id character varying(255) COLLATE pg_catalog."default" NOT NULL,
    reportee_org_no character varying(255) COLLATE pg_catalog."default" NOT NULL,
    created timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
    last_changed timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
    is_deleted boolean DEFAULT false,
    CONSTRAINT system_user_profile_pkey PRIMARY KEY (system_user_profile_id),
    CONSTRAINT system_user_profile_system_internal_id_fkey FOREIGN KEY (system_internal_id)
        REFERENCES business_application.system_register (system_internal_id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE NO ACTION
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS business_application.system_user_profile
    OWNER to auth_authentication_admin;

GRANT ALL ON TABLE business_application.system_user_profile TO auth_authentication;

GRANT ALL ON TABLE business_application.system_user_profile TO auth_authentication_admin;