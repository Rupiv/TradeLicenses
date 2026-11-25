/* =====================================================================================
   STAGING VALIDATION - READ ONLY
   Run against a STAGING copy (restored from a backup), never as a substitute for it.
   Contains only SELECT statements: no INSERT / UPDATE / DELETE / DDL.
   Each result set has a "check" column; rows with status = 'MISSING' / 'PROBLEM' need action
   before the corrected API is deployed.
   ===================================================================================== */
SET NOCOUNT ON;
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

/* 1. Stored procedures the API calls ------------------------------------------------ */
SELECT 'stored procedure' AS [check], v.name,
       CASE WHEN OBJECT_ID('dbo.' + v.name, 'P') IS NULL THEN 'MISSING' ELSE 'ok' END AS status
FROM (VALUES
 ('USP_ADMIN_FORGOT_PASSWORD'),('USP_OTP_PROCESS'),('sp_GetApprovedLicenceCertificate_FullTrade'),
 ('sp_GetFullTradeLicenceApplication_ByUserID'),('sp_GetTradeLicenceApplications_Admin'),
 ('sp_GetTradeLicenceApplications_Approver'),('sp_GetTradeLicenceApplications_SeniorApprover'),
 ('sp_GetTradeLicenceApplications_ZoneApprover'),('sp_MLC_CRUD'),('sp_Master_AssemblyConstituency_CRUD'),
 ('sp_Master_BBMPWard_CRUD'),('sp_Master_BBMPZone_CRUD'),('usp_ActionReasons_CRUD'),
 ('usp_Dashboard_ApplicationStatusCount'),('usp_GetTradeLicenceFee_ByTradeSubID_Only'),
 ('usp_GetUserWithLicenceDetails'),('usp_LicenceApplicationTemp_GetByLogin_Paged'),
 ('usp_LicenceApplicationTemp_GetFullDetails'),('usp_LicenceApplication_CRUD'),
 ('usp_LicenceApplication_Document_CRUD'),('usp_LicenceApplication_Geo_Get'),('usp_LicenceApplication_Geo_Save'),
 ('usp_LicenceApplication_GetAll_Paged'),('usp_LicenceApplication_GetByLogin_Paged'),
 ('usp_LicenceApplication_TrackStatus'),('usp_LicenceApplication_Workflow_Action'),('usp_LicenceTradeDetails_CRUD'),
 ('usp_Licence_ApplicationStatus_CRUD'),('usp_Licence_CurrentStatus_CRUD'),('usp_Licence_SubmitAction'),
 ('usp_LoginMaster_CRUD'),('usp_LoginUser'),('usp_MOH_CRUD'),('usp_MasterDocument_CRUD'),
 ('usp_MasterOfficeDetails_CRUD'),('usp_MasterOfficeType_CRUD'),('usp_Master_FinancialYear_CRUD'),
 ('usp_Master_LicenceProcess_CRUD'),('usp_Master_MOH_CRUD'),('usp_PaymentGatewayBank_CRUD'),
 ('usp_Payment_Audit_Log'),('usp_Report_ControlSheet'),('usp_Report_RevenueCollection'),
 ('usp_Report_WardWiseLicenses'),('usp_SMS_Message_Log'),('usp_TradeLicence_CRUD'),('usp_TradeMajor_CRUD'),
 ('usp_TradeMajor_GetAll'),('usp_TradeMinor_ByMajor'),('usp_TradeMinor_CRUD'),('usp_TradeSub_ByMinor'),
 ('usp_TradeSub_CRUD'),('usp_TradeType_CRUD'),('usp_TradeZonalClassification_CRUD'),('usp_Trade_LicenceFee_CRUD'),
 ('usp_UserAuth_CRUD'),('usp_Tradelicenseno')
) v(name)
ORDER BY status DESC, v.name;

GO
/* 2. Tables / columns used by the new security checks ------------------------------- */
SELECT 'column' AS [check], v.tbl, v.col,
       CASE WHEN COL_LENGTH('dbo.' + v.tbl, v.col) IS NULL THEN 'MISSING' ELSE 'ok' END AS status
FROM (VALUES
 ('Licence_Application_Temp','licenceApplicationID'),('Licence_Application_Temp','loginID'),
 ('Licence_Application_Temp','tradeLicenceID'),('Licence_Application_Temp','receiptNumber'),
 ('Licence_Application','licenceApplicationID'),('Licence_Application','loginID'),
 ('Licence_Application','tradeLicenceID'),('Licence_Application','licenceApplicationStatusID'),
 ('Licence_Application','applicationNumber'),
 ('Licence_Master','tradeLicenceID'),('Licence_Master','mobileNumber'),('Licence_Master','wardID'),
 ('Licence_Master_Temp','tradeLicenceID'),('Licence_Master_Temp','mobileNumber'),
 ('Licence_Application_Document','ApplicationDocumentID'),('Licence_Application_Document','LicenceApplicationID'),
 ('Payment_Audit_Log','LicenceApplicationId'),('Payment_Audit_Log','CorporationId'),('Payment_Audit_Log','TxnId'),
 ('Payment_Audit_Log','Amount'),('Payment_Audit_Log','PaymentStage'),('Payment_Audit_Log','CreatedOn'),
 ('Payment_Audit_Log','IsActive'),
 ('Login_GBAJurisdiction','loginID'),('Login_GBAJurisdiction','corporationID'),('Login_GBAJurisdiction','gbaZoneID'),
 ('Login_GBAJurisdiction','gbaWardID'),('Login_GBAJurisdiction','isActive'),
 ('Master_GBAWard','gbaWardID'),('Master_GBAWard','gbaZoneID'),('Master_GBAWard','corporationID'),
 ('Master_LegacyGBAWardMapping','legacyWardID'),('Master_LegacyGBAWardMapping','gbaWardID'),
 ('Master_LegacyGBAWardMapping','isPrimary'),('Master_LegacyGBAWardMapping','matchScore'),
 ('Licence_GBALocation','tradeLicenceID'),('Licence_GBALocation','tempTradeLicenceID'),('Licence_GBALocation','gbaWardID'),
 ('Payment_Gateway_Config','CorporationId'),('Payment_Gateway_Config','IsActive')
) v(tbl, col)
ORDER BY status DESC, v.tbl, v.col;

GO
/* 3. Certificate / acknowledgement tables ------------------------------------------- */
-- The API (EF Core, AppDbContext.GeneratedLicenceCertificate) uses table dbo.GeneratedLicenceCertificate.
-- The frontend repo's SqlScript/*.sql (acknowledgement migration) target dbo.LicenceGeneratedCertificate.
SELECT 'certificate table' AS [check], v.name,
       CASE WHEN OBJECT_ID('dbo.' + v.name, 'U') IS NULL THEN 'absent' ELSE 'present' END AS status, v.used_by
FROM (VALUES
 ('GeneratedLicenceCertificate',  'API (EF) - save / download'),
 ('LicenceGeneratedCertificate',  'SqlScript acknowledgement migration (not used by the API)'),
 ('LicenceGeneratedAcknowledgement','SqlScript acknowledgement migration (no API endpoint yet)')
) v(name, used_by);

GO
/* 4. Payment audit consistency (what the new payment checks rely on) ----------------- */
-- SUCCESS without an INITIATED row for the same transaction: would now be refused by payment-success
SELECT 'success without initiated' AS [check], COUNT(*) AS rows_found
FROM dbo.Payment_Audit_Log s
WHERE s.PaymentStage = 'SUCCESS'
  AND NOT EXISTS (SELECT 1 FROM dbo.Payment_Audit_Log i WHERE i.TxnId = s.TxnId AND i.PaymentStage = 'INITIATED');

-- One transaction recorded against more than one application (must be 0)
SELECT 'txn used by >1 application' AS [check], TxnId, COUNT(DISTINCT LicenceApplicationId) AS applications
FROM dbo.Payment_Audit_Log
GROUP BY TxnId
HAVING COUNT(DISTINCT LicenceApplicationId) > 1;

-- SUCCESS amount different from the INITIATED amount (possible past manipulation - investigate)
SELECT 'success amount <> initiated amount' AS [check], s.TxnId, s.LicenceApplicationId,
       i.Amount AS initiated_amount, s.Amount AS success_amount, s.CreatedOn
FROM dbo.Payment_Audit_Log s
CROSS APPLY (SELECT TOP 1 Amount FROM dbo.Payment_Audit_Log i
             WHERE i.TxnId = s.TxnId AND i.PaymentStage = 'INITIATED' ORDER BY i.CreatedOn) i
WHERE s.PaymentStage = 'SUCCESS' AND ROUND(i.Amount, 2) <> ROUND(s.Amount, 2);

-- Paid amount below the fee-master minimum (sum of current trade fees + 500) - possible underpayment.
-- Table names below are the usual ones; if this database uses others, adjust them to what
-- usp_LicenceTradeDetails_CRUD (GET_BY_APP_ID) and usp_GetTradeLicenceFee_ByTradeSubID_Only read.
IF OBJECT_ID('dbo.Licence_TradeDetails_Temp', 'U') IS NOT NULL AND OBJECT_ID('dbo.Trade_LicenceFee', 'U') IS NOT NULL
    EXEC sp_executesql N'
    SELECT TOP 200 ''paid below fee master minimum'' AS [check], s.TxnId, s.LicenceApplicationId, s.Amount AS paid, m.minimum
    FROM dbo.Payment_Audit_Log s
    CROSS APPLY (
        SELECT 500 + ISNULL(SUM(f.tradeLicenceFee), 0) AS minimum
        FROM dbo.Licence_TradeDetails_Temp t
        OUTER APPLY (SELECT TOP 1 tf.tradeLicenceFee FROM dbo.Trade_LicenceFee tf WHERE tf.tradeSubID = t.tradeSubID) f
        WHERE t.tempLicenceApplicationID = s.LicenceApplicationId
    ) m
    WHERE s.PaymentStage = ''SUCCESS'' AND s.Amount < m.minimum;';
ELSE
    SELECT 'paid below fee master minimum' AS [check], 'SKIPPED: adjust table names' AS status;

GO
/* 5. Ownership relationships ----------------------------------------------------------- */
SELECT 'drafts without owner' AS [check], COUNT(*) AS rows_found
FROM dbo.Licence_Application_Temp WHERE loginID IS NULL OR loginID = 0;

SELECT 'legacy rows (loginID = -1, matched by mobile)' AS [check],
       (SELECT COUNT(*) FROM dbo.Licence_Application_Temp WHERE loginID = -1) AS drafts,
       (SELECT COUNT(*) FROM dbo.Licence_Application WHERE loginID = -1) AS submitted;

SELECT 'documents without application' AS [check], COUNT(*) AS rows_found
FROM dbo.Licence_Application_Document d
WHERE NOT EXISTS (SELECT 1 FROM dbo.Licence_Application_Temp t WHERE t.licenceApplicationID = d.LicenceApplicationID)
  AND NOT EXISTS (SELECT 1 FROM dbo.Licence_Application a WHERE a.licenceApplicationID = d.LicenceApplicationID);

GO
/* 6. Jurisdiction / corporation mapping ------------------------------------------------ */
SELECT 'officers with GBA assignment' AS [check], COUNT(DISTINCT loginID) AS officers
FROM dbo.Login_GBAJurisdiction WHERE isActive = 1;

-- Frontend templates match corporations by name (Central / North / South / East / West)
SELECT 'corporation names' AS [check], corporationID, corporationName,
       CASE WHEN corporationName LIKE '%Central%' OR corporationName LIKE '%North%' OR corporationName LIKE '%South%'
                 OR corporationName LIKE '%East%' OR corporationName LIKE '%West%' THEN 'ok' ELSE 'PROBLEM' END AS status
FROM dbo.Master_Corporation;

SELECT 'corporations without gateway config' AS [check], c.corporationID, c.corporationName
FROM dbo.Master_Corporation c
WHERE NOT EXISTS (SELECT 1 FROM dbo.Payment_Gateway_Config g WHERE g.CorporationId = c.corporationID AND g.IsActive = 1);

GO
/* 7. Document storage paths (check on the server that these folders exist / are readable) */
SELECT TOP 20 'document path roots' AS [check], LEFT(FilePath, 60) AS path_prefix, COUNT(*) AS files
FROM dbo.Licence_Application_Document
WHERE ISNULL(IsActive, 1) = 1
GROUP BY LEFT(FilePath, 60)
ORDER BY files DESC;
GO

/* 8. Declared power / generator HP (GBA_PowerHP_Migration.sql) */
SELECT 'power HP table' AS [check],
       CASE WHEN OBJECT_ID('dbo.Licence_Application_PowerHP', 'U') IS NULL THEN 'MISSING - run GBA_PowerHP_Migration.sql' ELSE 'ok' END AS status;
GO

/* 9. Owner of draft licence records (GBA_LicenceOwner_Migration.sql, D6) */
SELECT 'draft owner table' AS [check],
       CASE WHEN OBJECT_ID('dbo.Licence_Master_Temp_Owner', 'U') IS NULL THEN 'MISSING - run GBA_LicenceOwner_Migration.sql (after approval)' ELSE 'ok' END AS status;
