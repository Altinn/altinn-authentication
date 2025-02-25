-- ALTER TABLE : business_application.system_user_profile

ALTER TABLE business_application.system_user_profile
ADD COLUMN sequence_no BIGINT NOT NULL DEFAULT business_application.tx_nextval('business_application.systemuser_seq');

ALTER TABLE business_application.system_user_profile
ADD CONSTRAINT uq_sequence_no UNIQUE (sequence_no);

