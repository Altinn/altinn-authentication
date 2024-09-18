-- Table: business_application.request

-- DROP TABLE IF EXISTS business_application.request

CREATE TABLE IF NOT EXISTS business_application.request(
    id uuid PRIMARY KEY,
    external_ref varchar(255) NOT NULL,
    system_id varchar(255) NOT NULL,
    party_org_no varchar(255) NOT NULL, 
    rights jsonb[],
    request_status varchar(255) NOT NULL,
    created timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
    last_changed timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,    
    is_deleted bool DEFAULT false   
)

TABLESPACE pg_default;