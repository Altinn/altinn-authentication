ALTER TABLE oidcserver.refresh_token_family ADD CONSTRAINT client_subject_op UNIQUE (client_id, subject_id, op_sid);
