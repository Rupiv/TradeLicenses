/* =====================================================================
   GBA user jurisdiction (Corporation / Zone / Ward of an officer)
   ---------------------------------------------------------------------
   ADDITIVE ONLY. Creates one new table; does not touch Login_Master,
   usp_LoginMaster_CRUD or the existing Office / MOH routing.

   One row per assignment:
     corporationID only                    -> whole corporation
     corporationID + gbaZoneID             -> whole GBA zone
     corporationID + gbaZoneID + gbaWardID -> one ward
   Changing a user's assignment deactivates old rows (isActive = 0); rows are
   never deleted, so assignment history is kept.

   Prerequisite: GBA_Corporation_Ward_Migration.sql (Master_Corporation /
   Master_GBAZone / Master_GBAWard). Safe to run more than once.
   ===================================================================== */
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID('dbo.Master_GBAWard', 'U') IS NULL
BEGIN
    RAISERROR('Run GBA_Corporation_Ward_Migration.sql first (Master_GBAWard is missing).', 16, 1);
    SET NOEXEC ON;
END
GO

IF OBJECT_ID('dbo.Login_GBAJurisdiction', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Login_GBAJurisdiction
    (
        jurisdictionID INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Login_GBAJurisdiction PRIMARY KEY,
        loginID        INT      NOT NULL CONSTRAINT FK_LoginGBAJur_Login       REFERENCES dbo.Login_Master(loginID),
        corporationID  INT      NOT NULL CONSTRAINT FK_LoginGBAJur_Corporation REFERENCES dbo.Master_Corporation(corporationID),
        gbaZoneID      INT      NULL     CONSTRAINT FK_LoginGBAJur_Zone        REFERENCES dbo.Master_GBAZone(gbaZoneID),
        gbaWardID      INT      NULL     CONSTRAINT FK_LoginGBAJur_Ward        REFERENCES dbo.Master_GBAWard(gbaWardID),
        isActive       BIT      NOT NULL CONSTRAINT DF_LoginGBAJur_isActive    DEFAULT (1),
        entryDate      DATETIME NOT NULL CONSTRAINT DF_LoginGBAJur_entryDate   DEFAULT (GETDATE()),
        entryBy        INT      NULL,
        updatedDate    DATETIME NULL,
        updatedBy      INT      NULL,
        CONSTRAINT CK_LoginGBAJur_WardNeedsZone CHECK (gbaWardID IS NULL OR gbaZoneID IS NOT NULL)
    );

    -- plain (not filtered) index: filtered indexes reject writes from QUOTED_IDENTIFIER OFF sessions
    CREATE INDEX IX_LoginGBAJur_Login ON dbo.Login_GBAJurisdiction(loginID, isActive);
END
GO

SET NOEXEC OFF;
GO

/* Rollback (only if no assignments were made yet):
   IF NOT EXISTS (SELECT 1 FROM dbo.Login_GBAJurisdiction) DROP TABLE dbo.Login_GBAJurisdiction;
*/
