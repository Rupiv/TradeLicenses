/* Rollback for GBA_LicenceOwner_Migration.sql.
   After dropping the table the API returns to the previous behaviour for unlinked draft records
   (any trader can open them - D6 reopens). Back up the table first if its rows are needed. */
IF OBJECT_ID('dbo.Licence_Master_Temp_Owner', 'U') IS NOT NULL
    DROP TABLE dbo.Licence_Master_Temp_Owner;
GO
