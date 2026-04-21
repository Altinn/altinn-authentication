-- Tables: business_application.request
--         business_application.request_archive
--         business_application.change_request
--
-- Widen the redirect_urls column from varchar(255) to text so that long
-- redirect URLs (e.g. ones carrying state/query parameters) can be stored
-- without causing a 500 on POST /authentication/api/v1/systemuser/request/vendor.

ALTER TABLE business_application.request
    ALTER COLUMN redirect_urls TYPE text;

ALTER TABLE business_application.request_archive
    ALTER COLUMN redirect_urls TYPE text;

ALTER TABLE business_application.change_request
    ALTER COLUMN redirect_urls TYPE text;
