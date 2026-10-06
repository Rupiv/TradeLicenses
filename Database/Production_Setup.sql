/* =====================================================================
   GBA TRADE LICENCE - PRODUCTION SETUP & MONITORING QUERIES
   Run in SSMS against the TradeLicence database.
   Run each PART separately (select the block, then F5).
   PART 1 and PART 2 are REQUIRED before deploying the new API.
   ===================================================================== */
USE [TradeLicence];
GO


/* =====================================================================
   PART 1 (REQUIRED) : SMS MESSAGE LOG TABLE
   Every SMS the API sends (OTP, received, approved, rejected, ...)
   is written here - success AND failure. OTP values are masked (******).
   ===================================================================== */
IF OBJECT_ID('dbo.SMS_Message_Log', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.SMS_Message_Log
    (
        SmsLogID          BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_SMS_Message_Log PRIMARY KEY,
        TemplateKey       NVARCHAR(50)   NOT NULL,          -- OTP_PAYMENT, APP_RECEIVED, ...
        TemplateId        NVARCHAR(50)   NULL,              -- DLT template id
        SmsType           NVARCHAR(20)   NULL,              -- SINGLE / OTP / UNICODE ...
        MobileNo          NVARCHAR(15)   NOT NULL,
        ReferenceNo       NVARCHAR(100)  NULL,              -- Application Number (NULL for OTP)
        MessageText       NVARCHAR(1000) NULL,              -- final text sent (OTP masked)
        Status            NVARCHAR(20)   NOT NULL,          -- SENT / FAILED
        GatewayResponse   NVARCHAR(1000) NULL,              -- raw gateway reply ("402,MsgID = ...")
        GatewayMessageId  NVARCHAR(100)  NULL,
        ErrorMessage      NVARCHAR(1000) NULL,
        CreatedOn         DATETIME2(0)   NOT NULL CONSTRAINT DF_SMS_Message_Log_CreatedOn DEFAULT (SYSDATETIME())
    );

    CREATE INDEX IX_SMS_Message_Log_CreatedOn   ON dbo.SMS_Message_Log (CreatedOn DESC);
    CREATE INDEX IX_SMS_Message_Log_MobileNo    ON dbo.SMS_Message_Log (MobileNo, CreatedOn DESC);
    CREATE INDEX IX_SMS_Message_Log_ReferenceNo ON dbo.SMS_Message_Log (ReferenceNo) WHERE ReferenceNo IS NOT NULL;
    CREATE INDEX IX_SMS_Message_Log_Status      ON dbo.SMS_Message_Log (Status, CreatedOn DESC);
END
GO

/* Stored procedure used by the API (SmsService) */
CREATE OR ALTER PROCEDURE dbo.usp_SMS_Message_Log
    @Action           NVARCHAR(20),
    @SmsLogID         BIGINT         = NULL,
    @TemplateKey      NVARCHAR(50)   = NULL,
    @TemplateId       NVARCHAR(50)   = NULL,
    @SmsType          NVARCHAR(20)   = NULL,
    @MobileNo         NVARCHAR(15)   = NULL,
    @ReferenceNo      NVARCHAR(100)  = NULL,
    @MessageText      NVARCHAR(1000) = NULL,
    @Status           NVARCHAR(20)   = NULL,
    @GatewayResponse  NVARCHAR(1000) = NULL,
    @GatewayMessageId NVARCHAR(100)  = NULL,
    @ErrorMessage     NVARCHAR(1000) = NULL,
    @FromDate         DATE           = NULL,
    @ToDate           DATE           = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @Action = 'INSERT'
    BEGIN
        INSERT INTO dbo.SMS_Message_Log
            (TemplateKey, TemplateId, SmsType, MobileNo, ReferenceNo, MessageText,
             Status, GatewayResponse, GatewayMessageId, ErrorMessage)
        VALUES
            (@TemplateKey, @TemplateId, @SmsType, @MobileNo, @ReferenceNo, @MessageText,
             @Status, @GatewayResponse, @GatewayMessageId, @ErrorMessage);

        SELECT CAST(SCOPE_IDENTITY() AS BIGINT) AS SmsLogID;
        RETURN;
    END

    IF @Action = 'SEARCH'
    BEGIN
        SELECT TOP (1000) *
        FROM dbo.SMS_Message_Log
        WHERE (@MobileNo    IS NULL OR MobileNo    = @MobileNo)
          AND (@ReferenceNo IS NULL OR ReferenceNo = @ReferenceNo)
          AND (@TemplateKey IS NULL OR TemplateKey = @TemplateKey)
          AND (@Status      IS NULL OR Status      = @Status)
          AND (@FromDate    IS NULL OR CreatedOn  >= @FromDate)
          AND (@ToDate      IS NULL OR CreatedOn  <  DATEADD(DAY, 1, @ToDate))
        ORDER BY CreatedOn DESC;
        RETURN;
    END
END
GO


/* =====================================================================
   PART 2 (REQUIRED) : SMS TEMPLATES
   Adds any missing template (existing rows are NOT changed),
   then shows all 5 - every row must have IsActive = 'Y'.
   ===================================================================== */
INSERT INTO dbo.SMS_Template_Master (TemplateKey, TemplateId, TemplateText, SmsType, IsActive)
SELECT s.TemplateKey, s.TemplateId, s.TemplateText, s.SmsType, 'Y'
FROM (VALUES
    ('APP_APPROVED',       '1107176836525707980', N'Your Application Number {#var#} for Trade License has been Approved. Please collect the Receipt from {#var#} City Corporation Trade License.-BBMPIT', 'SINGLE'),
    ('APP_RECEIVED',       '1107176836509001943', N'Your Application Number {#var#} for Trade License has been received.-BBMPIT', 'SINGLE'),
    ('APP_REJECTED',       '1107176836523038375', N'Your Application Number {#var#} for Trade License has been rejected.-BBMPIT', 'SINGLE'),
    ('OTP_PAYMENT',        '1107176836515656796', N'OTP IS {#var#} at {#var#} on {#var#} for Trade License Registration. Please do not share Your OTP. {#var#} City Corporation Trade License.-BBMPIT', 'OTP'),
    ('PROVISIONAL_ISSUED', '1107176836520190881', N'Your Application Number {#var#} has been received hence provisional Trade License is hereby issued.-BBMPIT', 'SINGLE')
) AS s (TemplateKey, TemplateId, TemplateText, SmsType)
WHERE NOT EXISTS (SELECT 1 FROM dbo.SMS_Template_Master t WHERE t.TemplateKey = s.TemplateKey);
GO

SELECT TemplateKey, TemplateId, SmsType, IsActive, TemplateText
FROM dbo.SMS_Template_Master
ORDER BY TemplateKey;
GO


/* =====================================================================
   PART 3 : EASEBUZZ PRODUCTION KEY / SALT
   Step 3a - look at the current row first (keep a copy of these values).
   Step 3b - replace the <...> placeholders and run the UPDATE.
   'test' = testpay.easebuzz.in ; anything else (use 'prod') = pay.easebuzz.in
   ===================================================================== */
-- 3a. Current configuration
SELECT CorporationId, MerchantKey, MerchantEmail, Environment, IsActive
FROM dbo.Payment_Gateway_Config;
GO

-- 3b. Switch to production (UNCOMMENT, fill values, run)
/*
BEGIN TRAN;

UPDATE dbo.Payment_Gateway_Config
SET MerchantKey   = '<PRODUCTION_KEY>',
    MerchantSalt  = '<PRODUCTION_SALT>',
    MerchantEmail = '<MERCHANT_EMAIL>',
    Environment   = 'prod'
WHERE CorporationId = 1
  AND IsActive = 1;

-- must show exactly 1 row updated, with the new values
SELECT CorporationId, MerchantKey, MerchantEmail, Environment, IsActive
FROM dbo.Payment_Gateway_Config
WHERE CorporationId = 1;

-- COMMIT;     -- run when the row above is correct
-- ROLLBACK;   -- run instead if anything is wrong
*/
GO


/* =====================================================================
   PART 4 : SMS MONITORING (run any time)
   ===================================================================== */

-- 4.1 Last 100 SMS
SELECT TOP (100) SmsLogID, CreatedOn, TemplateKey, MobileNo, ReferenceNo, Status,
       GatewayMessageId, ErrorMessage, MessageText
FROM dbo.SMS_Message_Log
ORDER BY CreatedOn DESC;

-- 4.2 Today's SMS count by template and status
SELECT TemplateKey, Status, COUNT(*) AS Total
FROM dbo.SMS_Message_Log
WHERE CreatedOn >= CAST(GETDATE() AS DATE)
GROUP BY TemplateKey, Status
ORDER BY TemplateKey, Status;

-- 4.3 Failed SMS in the last 7 days (with gateway reply / error)
SELECT SmsLogID, CreatedOn, TemplateKey, MobileNo, ReferenceNo, GatewayResponse, ErrorMessage
FROM dbo.SMS_Message_Log
WHERE Status = 'FAILED'
  AND CreatedOn >= DATEADD(DAY, -7, GETDATE())
ORDER BY CreatedOn DESC;

-- 4.4 All SMS sent to one mobile number
DECLARE @Mobile NVARCHAR(15) = '9999999999';      -- <- change
SELECT CreatedOn, TemplateKey, ReferenceNo, Status, MessageText, GatewayResponse, ErrorMessage
FROM dbo.SMS_Message_Log
WHERE MobileNo = @Mobile
ORDER BY CreatedOn DESC;

-- 4.5 All SMS for one application number
DECLARE @AppNo NVARCHAR(100) = 'APP-NUMBER-HERE';  -- <- change
SELECT CreatedOn, TemplateKey, MobileNo, Status, MessageText, GatewayResponse, ErrorMessage
FROM dbo.SMS_Message_Log
WHERE ReferenceNo = @AppNo
ORDER BY CreatedOn;

-- 4.6 Daily SMS volume, last 30 days
SELECT CAST(CreatedOn AS DATE) AS [Date],
       SUM(CASE WHEN Status = 'SENT'   THEN 1 ELSE 0 END) AS Sent,
       SUM(CASE WHEN Status = 'FAILED' THEN 1 ELSE 0 END) AS Failed,
       COUNT(*) AS Total
FROM dbo.SMS_Message_Log
WHERE CreatedOn >= DATEADD(DAY, -30, GETDATE())
GROUP BY CAST(CreatedOn AS DATE)
ORDER BY [Date] DESC;

-- 4.7 Same search through the stored procedure (all filters optional)
EXEC dbo.usp_SMS_Message_Log @Action = 'SEARCH', @Status = 'FAILED', @FromDate = NULL, @ToDate = NULL;
GO


/* =====================================================================
   PART 5 : PAYMENT MONITORING
   usp_Payment_Audit_Log writes the payment trail. Run 5.1 once to see
   which table it writes to, then use that table name in 5.2.
   ===================================================================== */
-- 5.1 Find the payment audit table
EXEC sp_helptext 'dbo.usp_Payment_Audit_Log';

-- 5.2 Example (replace <PaymentAuditTable> with the table from 5.1)
/*
SELECT TOP (100) *
FROM dbo.<PaymentAuditTable>
ORDER BY 1 DESC;

-- one application's payment attempts (TxnId looks like GBA-TL-{LicenceApplicationId}-{yyMMddHHmmss})
SELECT *
FROM dbo.<PaymentAuditTable>
WHERE TxnId LIKE 'GBA-TL-12345-%'      -- <- application id
ORDER BY 1;
*/
GO
