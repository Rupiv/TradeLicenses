/* =====================================================================
   00_CHECK_STATUS.sql  -  READ-ONLY. Changes nothing.
   Run on the client's TradeLicence database BEFORE any other script.
   For each step it says: APPLIED (skip) / NOT APPLIED (run the script) /
   DIFFERENT VERSION (do NOT run - send this output to the developer).
   ===================================================================== */
SET NOCOUNT ON;

DECLARE @r TABLE (step INT, script NVARCHAR(80), state NVARCHAR(30), detail NVARCHAR(300));
DECLARE @def NVARCHAR(MAX);

/* 1. Production_Setup.sql (SMS log table) */
INSERT @r SELECT 1, N'Production_Setup.sql (PART 1 + 2)',
    CASE WHEN OBJECT_ID('dbo.SMS_Message_Log','U') IS NOT NULL THEN N'APPLIED' ELSE N'NOT APPLIED' END,
    N'SMS_Message_Log table';

/* 2. GBA corporations / zones / 369 wards / licence location */
INSERT @r SELECT 2, N'GBA_Corporation_Ward_Migration.sql',
    CASE WHEN OBJECT_ID('dbo.Master_GBAWard','U') IS NULL OR OBJECT_ID('dbo.Licence_GBALocation','U') IS NULL
              OR OBJECT_ID('dbo.vw_LicenceCorporation','V') IS NULL THEN N'NOT APPLIED'
         ELSE N'APPLIED' END,
    N'Master_Corporation, Master_GBAZone, Master_GBAWard, Licence_GBALocation, vw_LicenceCorporation (counts shown below)';

/* 3. Admin list with corporation filter */
INSERT @r SELECT 3, N'GBA_StoredProcedure_Updates.sql',
    CASE WHEN OBJECT_ID('dbo.sp_GetTradeLicenceApplications_Admin') IS NULL THEN N'DIFFERENT VERSION'
         WHEN EXISTS (SELECT 1 FROM sys.parameters WHERE object_id = OBJECT_ID('dbo.sp_GetTradeLicenceApplications_Admin') AND name = '@CorporationID') THEN N'APPLIED'
         ELSE N'NOT APPLIED' END,
    N'sp_GetTradeLicenceApplications_Admin has @CorporationID';

/* 4. Officer corporation / ward assignment */
INSERT @r SELECT 4, N'GBA_UserJurisdiction.sql',
    CASE WHEN OBJECT_ID('dbo.Login_GBAJurisdiction','U') IS NOT NULL THEN N'APPLIED' ELSE N'NOT APPLIED' END,
    N'Login_GBAJurisdiction table';

/* 5. Inspector queue zone filter */
SET @def = OBJECT_DEFINITION(OBJECT_ID('dbo.sp_GetTradeLicenceApplications_Approver'));
INSERT @r SELECT 5, N'GBA_ApproverZoneFilter_Fix.sql',
    CASE WHEN @def IS NULL THEN N'DIFFERENT VERSION'
         WHEN @def LIKE N'%INNER JOIN Licence_Master lmz%' THEN N'APPLIED'
         WHEN @def LIKE N'%INNER JOIN Licence_Master_Temp lmt%' AND @def LIKE N'%LOOKUP MODE%' AND @def LIKE N'%#Filtered%' THEN N'NOT APPLIED'
         ELSE N'DIFFERENT VERSION' END,
    N'sp_GetTradeLicenceApplications_Approver (length ' + ISNULL(CAST(LEN(@def) AS NVARCHAR(10)), N'-') + N')';

/* 6. Senior approval rule (Inspected OR Forwarded) */
SET @def = OBJECT_DEFINITION(OBJECT_ID('dbo.usp_Licence_SubmitAction'));
INSERT @r SELECT 6, N'GBA_SeniorApprovalRule_Fix.sql',
    CASE WHEN @def IS NULL THEN N'DIFFERENT VERSION'
         WHEN @def LIKE N'%licenceProcessID IN (7, 6)%' THEN N'APPLIED'
         WHEN @def LIKE N'%Inspection not completed. Approval not allowed.%' AND @def LIKE N'%PREVENT DUPLICATE ACTION%'
              AND @def LIKE N'%AND licenceProcessID = 7%' THEN N'NOT APPLIED'
         ELSE N'DIFFERENT VERSION' END,
    N'usp_Licence_SubmitAction (length ' + ISNULL(CAST(LEN(@def) AS NVARCHAR(10)), N'-') + N')';

SELECT step, script, state, detail FROM @r ORDER BY step;

/* Objects the NEW API reads (must all say OK) */
SELECT obj, CASE WHEN OBJECT_ID(obj) IS NULL THEN N'MISSING - tell the developer' ELSE N'OK' END AS state
FROM (VALUES (N'dbo.Payment_Gateway_Config'), (N'dbo.Payment_Audit_Log'), (N'dbo.usp_Payment_Audit_Log'),
             (N'dbo.GeneratedLicenceCertificate'), (N'dbo.usp_LicenceApplicationTemp_GetFullDetails'),
             (N'dbo.usp_Licence_SubmitAction'), (N'dbo.usp_Tradelicenseno'), (N'dbo.generatenewlicencenumber'),
             (N'dbo.sp_GetApprovedLicenceCertificate_FullTrade'), (N'dbo.Licence_ReceiptSequenceNumber')) v(obj);

/* GBA counts (expected: 5 corporations, 369 wards) */
IF OBJECT_ID('dbo.Master_GBAWard','U') IS NOT NULL
    EXEC(N'SELECT (SELECT COUNT(*) FROM dbo.Master_Corporation) AS corporations,
                  (SELECT COUNT(*) FROM dbo.Master_GBAZone) AS zones,
                  (SELECT COUNT(*) FROM dbo.Master_GBAWard) AS wards');

/* Payment merchants: for the LIVE site every corporation must have Environment = live/prod keys */
IF OBJECT_ID('dbo.Payment_Gateway_Config','U') IS NOT NULL
    EXEC(N'SELECT CorporationId, Environment, IsActive FROM dbo.Payment_Gateway_Config ORDER BY CorporationId');
