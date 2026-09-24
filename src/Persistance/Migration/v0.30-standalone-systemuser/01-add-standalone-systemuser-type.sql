-- Adds the 'standalone' value to the systemuser_type enum, for system users that an organisation
-- creates for its own, self-built system (see SystemUserType.Standalone).
-- Kept in its own file/transaction: Postgres does not allow a newly added enum value to be
-- referenced by other DDL/DML in the same transaction it was added in.
ALTER TYPE business_application.systemuser_type ADD VALUE 'standalone';
