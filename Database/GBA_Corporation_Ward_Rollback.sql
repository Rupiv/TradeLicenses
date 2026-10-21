/* =====================================================================
   ROLLBACK for GBA_Corporation_Ward_Migration.sql
   Drops ONLY the objects that migration created. Existing tables are
   not touched.

   WARNING: Licence_GBALocation holds the corporation/ward confirmed by
   applicants after go-live. This script refuses to run if it has rows;
   back it up first if you really need to roll back after go-live.
   ===================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID('dbo.Licence_GBALocation', 'U') IS NOT NULL
   AND EXISTS (SELECT 1 FROM dbo.Licence_GBALocation)
BEGIN
    RAISERROR('Licence_GBALocation contains data - back it up and empty it before rolling back.', 16, 1);
    RETURN;
END

BEGIN TRANSACTION;
IF OBJECT_ID('dbo.vw_LicenceCorporation', 'V') IS NOT NULL      DROP VIEW  dbo.vw_LicenceCorporation;
IF OBJECT_ID('dbo.Licence_GBALocation', 'U') IS NOT NULL        DROP TABLE dbo.Licence_GBALocation;
IF OBJECT_ID('dbo.Master_LegacyGBAWardMapping', 'U') IS NOT NULL DROP TABLE dbo.Master_LegacyGBAWardMapping;
IF OBJECT_ID('dbo.Master_GBAWard', 'U') IS NOT NULL             DROP TABLE dbo.Master_GBAWard;
IF OBJECT_ID('dbo.Master_GBAZone', 'U') IS NOT NULL             DROP TABLE dbo.Master_GBAZone;
IF OBJECT_ID('dbo.Master_Corporation', 'U') IS NOT NULL         DROP TABLE dbo.Master_Corporation;
COMMIT TRANSACTION;
