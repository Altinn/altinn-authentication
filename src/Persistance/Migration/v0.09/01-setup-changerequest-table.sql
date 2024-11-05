-- Table: business_application.request

-- DROP TABLE IF EXISTS business_application.change_request

CREATE TABLE IF NOT EXISTS business_application.change_request(
    id uuid PRIMARY KEY,
    external_ref varchar(255) NOT NULL,
    system_id varchar(255) NOT NULL,
    system_user_id uuid NOT NULL,
    party_org_no varchar(255) NOT NULL, 
    required_rights jsonb,
    unwanted_rights jsonb,
    request_status varchar(255) NOT NULL,
    redirect_urls varchar(255),
    created timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
    last_changed timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,    
    is_deleted bool DEFAULT false,   
    changed_by varchar(255)
)

TABLESPACE pg_default;