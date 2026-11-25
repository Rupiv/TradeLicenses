/* =====================================================================================
   GBA - owner of draft licence records (D6)            PREPARED, NOT RUN. Needs approval.

   Problem: POST /api/trade-licence creates a Licence_Master_Temp row before any application
   points to it. While unlinked, the API could not tell who created it, so any trader could read
   (name, mobile, address) or edit it by id.

   Change (additive only):
     dbo.Licence_Master_Temp_Owner (tradeLicenceID -> ownerLoginID), written by the API from the
     signed-in user's token when the record is created (TradeLicenceController.CreateDraft).
     The API then allows an UNLINKED record only to its recorded owner (CallerAccess.MayAccessLicenceAsync).
     Linked records keep the existing rules (owner of the application, legacy loginID = -1 mobile
     match, officer jurisdiction, admin) - this table is not consulted for them.

   No existing table, procedure or row is changed.

   BACKFILL: none. Ownership is NOT inferred for existing records:
     - linked records do not need it (their application carries the owner);
     - existing UNLINKED records have no reliable owner (Licence_Master_Temp has no creator column;
       mobileNumber is applicant data typed into the form, not proof of who created the row).
     After the migration such records are not opened to any trader. They are abandoned half-saved
     drafts (the second save step failed); the trader starts the application again. The query in
     section 3 lists them for the owner's review; removing them is a separate, approved decision.

   Order: run AFTER the API build containing LicenceOwnerStore is deployed or BEFORE - both are
   safe (without the table the API keeps its previous behaviour; with the table and an older API
   the table is simply unused). Rollback: GBA_LicenceOwner_Rollback.sql. Run on STAGING first.
   ===================================================================================== */
SET XACT_ABORT ON;
GO

/* 1. Table ------------------------------------------------------------------------------ */
IF OBJECT_ID('dbo.Licence_Master_Temp_Owner', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Licence_Master_Temp_Owner
    (
        tradeLicenceID BIGINT       NOT NULL
            CONSTRAINT PK_Licence_Master_Temp_Owner PRIMARY KEY,
        ownerLoginID   BIGINT       NOT NULL,
        createdOn      DATETIME2(0) NOT NULL
            CONSTRAINT DF_Licence_Master_Temp_Owner_createdOn DEFAULT (SYSUTCDATETIME())
    );
    CREATE INDEX IX_Licence_Master_Temp_Owner_owner ON dbo.Licence_Master_Temp_Owner(ownerLoginID);
END;
GO
-- No foreign key: the key columns of Licence_Master_Temp / User_Login are not defined in this
-- repository. Add after confirming them on staging (Retrieve_Definitions_ReadOnly.sql), e.g.
--   ALTER TABLE dbo.Licence_Master_Temp_Owner ADD CONSTRAINT FK_LMTOwner_Temp
--     FOREIGN KEY (tradeLicenceID) REFERENCES dbo.Licence_Master_Temp(tradeLicenceID);

/* 2. Least privilege: the API login needs SELECT + INSERT only (replace <api_login>) ------- */
-- GRANT SELECT, INSERT ON dbo.Licence_Master_Temp_Owner TO [<api_login>];

/* 3. Review list (read only): existing unlinked records that no trader can open afterwards -- */
SELECT COUNT(*) AS unlinked_records_without_owner
FROM dbo.Licence_Master_Temp m
WHERE NOT EXISTS (SELECT 1 FROM dbo.Licence_Application_Temp t WHERE t.tradeLicenceID = m.tradeLicenceID)
  AND NOT EXISTS (SELECT 1 FROM dbo.Licence_Application la WHERE la.tradeLicenceID = m.tradeLicenceID)
  AND NOT EXISTS (SELECT 1 FROM dbo.Licence_Master_Temp_Owner o WHERE o.tradeLicenceID = m.tradeLicenceID);
GO
