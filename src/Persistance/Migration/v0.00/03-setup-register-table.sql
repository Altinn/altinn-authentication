-- Table: business_application.system_register

-- DROP TABLE IF EXISTS business_application.system_register;

CREATE TABLE IF NOT EXISTS business_application.system_register
(
    system_internal_id uuid DEFAULT gen_random_uuid() PRIMARY KEY,
    system_id varchar(255) NOT NULL,
    system_name varchar(255),        
    systemvendor_orgnumber varchar(255),    
    rights jsonb[],
    client_id text array,    
    created timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
    last_changed timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
    is_visible bool DEFAULT true,
    is_deleted bool DEFAULT false,    
    UNIQUE (system_id)
)

TABLESPACE pg_default;