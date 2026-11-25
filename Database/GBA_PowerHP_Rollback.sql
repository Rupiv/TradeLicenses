/* Rollback for GBA_PowerHP_Migration.sql.
   Drops the HP table: stored HP values are lost and the API falls back to the minimum-amount
   check (trade fees + 500) for every draft. Take a backup of the table first if the values matter. */
IF OBJECT_ID('dbo.Licence_Application_PowerHP', 'U') IS NOT NULL
    DROP TABLE dbo.Licence_Application_PowerHP;
GO
