-- ALTER TABLE business_application.system_register

ALTER TABLE business_application.system_register
ADD COLUMN name business_application.translated_text;

ALTER TABLE business_application.system_register
ADD COLUMN description business_application.translated_text;

ALTER TABLE business_application.system_register
ADD COLUMN allowedredirecturls business_application.uri;