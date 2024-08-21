-- Table: business_application.maskinporten_client

ALTER TABLE IF EXISTS business_application.maskinporten_client 
ADD COLUMN system_internal_id uuid;

ALTER TABLE IF EXISTS business_application.maskinporten_client 
ADD CONSTRAINT fk_maskinporten_client_system_register FOREIGN KEY (system_internal_id) REFERENCES business_application.system_register (system_internal_id);