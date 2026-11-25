/* =====================================================================================
   GBA - store Power HP / Generator HP declared on the New Licence form
   Approved 2026-11-24 (full server-side fee validation).

   Why: the payable amount is  trade fees + max(power HP, generator HP) x 100 + 500.
   The HP values were only kept in the browser, so the API could not recompute the amount.
   The API now saves them with the draft (PUT/POST licence-application/draft) and checks the
   amount sent to the gateway against the recomputed fee.

   Additive only: one new table, no change to existing tables, procedures or data.
   Idempotent: safe to run more than once. Rollback: GBA_PowerHP_Rollback.sql
   Run on STAGING first.

   Key: LicenceApplicationID = the draft id (dbo.Licence_Application_Temp.licenceApplicationID),
   the same id the payment uses (Payment_Audit_Log.LicenceApplicationId).
   No foreign key is declared because the primary key of Licence_Application_Temp is not
   defined in this repository; add one after confirming it on staging if wanted:
     ALTER TABLE dbo.Licence_Application_PowerHP ADD CONSTRAINT FK_LicenceApplicationPowerHP_Temp
       FOREIGN KEY (LicenceApplicationID) REFERENCES dbo.Licence_Application_Temp(licenceApplicationID);
   ===================================================================================== */
SET XACT_ABORT ON;
GO

IF OBJECT_ID('dbo.Licence_Application_PowerHP', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Licence_Application_PowerHP
    (
        LicenceApplicationID BIGINT        NOT NULL
            CONSTRAINT PK_Licence_Application_PowerHP PRIMARY KEY,
        PowerHP              DECIMAL(12,4) NOT NULL
            CONSTRAINT CK_LicenceApplicationPowerHP_Power CHECK (PowerHP >= 0),
        GeneratorHP          DECIMAL(12,4) NOT NULL
            CONSTRAINT CK_LicenceApplicationPowerHP_Generator CHECK (GeneratorHP >= 0),
        UpdatedBy            BIGINT        NULL,
        UpdatedOn            DATETIME2(0)  NOT NULL
            CONSTRAINT DF_LicenceApplicationPowerHP_UpdatedOn DEFAULT (SYSUTCDATETIME())
    );
END;
GO

SELECT 'Licence_Application_PowerHP' AS [object],
       CASE WHEN OBJECT_ID('dbo.Licence_Application_PowerHP', 'U') IS NULL THEN 'MISSING' ELSE 'ok' END AS status;
GO
