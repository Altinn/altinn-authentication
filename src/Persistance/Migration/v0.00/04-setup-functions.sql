-- Function: insert_system_user_integration
CREATE OR REPLACE FUNCTION altinn_authentication.insert_system_user_integration(
	_integration_title varchar,
	_integration_description varchar,
	_product_name varchar,
	_owned_by_party_id varchar, 
	_supplier_name varchar,
	_supplier_org_no varchar,
	_client_id varchar
)
RETURNS uuid AS
$BODY$
DECLARE returnId uuid;
BEGIN
	INSERT INTO altinn_authentication.system_user_integration(	
	integration_title,
	integration_description,
	product_name,
	owned_by_party_id, 
	supplier_name,
	supplier_org_no,
	client_id)
	VALUES (
	_integration_title,
	_integration_description,
	_product_name,
	_owned_by_party_id, 
	_supplier_name,
	_supplier_org_no,
	_client_id
	)
	RETURNING system_user_integration_id INTO returnId;
	RETURN returnID;
END
$BODY$
LANGUAGE plpgsql;

-- Function: get_system_user_integration_by_id
CREATE OR REPLACE FUNCTION altinn_authentication.get_system_user_integration_by_id(
	_system_user_integration_id uuid
)
RETURNS altinn_authentication.system_user_integration AS
$BODY$
BEGIN
	SELECT * from altinn_authentication.system_user_integration sui 
	WHERE sui.system_user_integration_id = _system_user_integration_id
	AND sui.is_deleted = false;
END
$BODY$
LANGUAGE plpgsql;

-- Function: get_all_active_integrations_for_party
CREATE OR REPLACE FUNCTION altinn_authentication.get_system_user_integration_by_id(
	_owned_by_party_id varchar
)
RETURNS altinn_authentication.system_user_integration AS
$BODY$
BEGIN
	SELECT * from altinn_authentication.system_user_integration sui 
	WHERE sui.owned_by_party_id = _owned_by_party_id;
END
$BODY$
LANGUAGE plpgsql;