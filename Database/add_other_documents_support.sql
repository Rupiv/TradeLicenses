-- add_other_documents_support.sql
-- Adds DocumentDescription column to Licence_Application_Document
-- so "Other Documents" (DocumentID = 11) can carry a user-entered label.
-- Run ONCE on the TradeLicence database.

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'Licence_Application_Document' AND COLUMN_NAME = 'DocumentDescription'
)
BEGIN
    ALTER TABLE dbo.Licence_Application_Document ADD DocumentDescription VARCHAR(200) NULL;
    PRINT 'Added DocumentDescription to Licence_Application_Document';
END
ELSE
    PRINT 'DocumentDescription already exists in Licence_Application_Document';
GO
