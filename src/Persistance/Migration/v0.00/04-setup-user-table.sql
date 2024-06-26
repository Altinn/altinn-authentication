-- Table: business_application.system_user_profile

-- DROP TABLE IF EXISTS business_application.system_user_profile;

CREATE TABLE IF NOT EXISTS business_application.system_user_profile
(
    system_user_profile_id uuid NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
    integration_title character varying(255) COLLATE pg_catalog."default" NOT NULL,    
    system_internal_id uuid NOT NULL,
    reportee_party_id character varying(255) COLLATE pg_catalog."default" NOT NULL,
    reportee_org_no character varying(255) COLLATE pg_catalog."default" NOT NULL,
    created timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
    last_changed timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
    is_deleted boolean DEFAULT false,
    CONSTRAINT system_user_profile_system_internal_id_fkey FOREIGN KEY (system_internal_id)
        REFERENCES business_application.system_register (system_internal_id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE NO ACTION
)

TABLESPACE pg_default;