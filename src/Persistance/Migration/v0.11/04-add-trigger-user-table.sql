-- ALTER TABLE : business_application.system_user_profile

CREATE OR REPLACE FUNCTION business_application.update_sequence_no()
RETURNS TRIGGER AS $BODY$
BEGIN
  NEW.sequence_no = business_application.tx_nextval('business_application.systemuser_seq');
  RETURN NEW;
END
$BODY$ 
LANGUAGE plpgsql;

CREATE TRIGGER update_systemuser_seqno
BEFORE UPDATE on business_application.system_user_profile
FOR EACH ROW EXECUTE PROCEDURE business_application.update_sequence_no();