-- add_floorNumber_column.sql
-- Adds floorNumber column to Licence_Master_Temp and Licence_Master,
-- then updates usp_TradeLicence_CRUD to accept and store it.
-- Run ONCE on the TradeLicence database.

BEGIN TRANSACTION;

-- 1. Add column to Licence_Master_Temp (draft table)
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'Licence_Master_Temp' AND COLUMN_NAME = 'floorNumber'
)
BEGIN
    ALTER TABLE dbo.Licence_Master_Temp ADD floorNumber VARCHAR(50) NULL;
    PRINT 'Added floorNumber to Licence_Master_Temp';
END
ELSE
    PRINT 'floorNumber already exists in Licence_Master_Temp';

-- 2. Add column to Licence_Master (final table)
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'Licence_Master' AND COLUMN_NAME = 'floorNumber'
)
BEGIN
    ALTER TABLE dbo.Licence_Master ADD floorNumber VARCHAR(50) NULL;
    PRINT 'Added floorNumber to Licence_Master';
END
ELSE
    PRINT 'floorNumber already exists in Licence_Master';

COMMIT;
GO

-- 3. Update the stored procedure to accept @floorNumber
ALTER PROCEDURE [dbo].[usp_TradeLicence_CRUD]
(
    @Action                 VARCHAR(30),
    @tradeLicenceID         BIGINT = NULL,

    @applicantName          VARCHAR(200) = NULL,
    @doorNumber             VARCHAR(50) = NULL,
    @floorNumber            VARCHAR(50) = NULL,
    @address1               VARCHAR(200) = NULL,
    @address2               VARCHAR(200) = NULL,
    @address3               VARCHAR(200) = NULL,
    @pincode                VARCHAR(10) = NULL,
    @landLineNumber         VARCHAR(20) = NULL,
    @mobileNumber           VARCHAR(15) = NULL,
    @emailID                VARCHAR(100) = NULL,
    @tradeName              VARCHAR(200) = NULL,
    @zonalClassificationID  INT = NULL,
    @mohID                  INT = NULL,
    @wardID                 INT = NULL,
    @PropertyID             INT = NULL,
    @PIDNumber              VARCHAR(50) = NULL,
    @khathaNumber           VARCHAR(50) = NULL,
    @surveyNumber           VARCHAR(50) = NULL,
    @street                 VARCHAR(200) = NULL,
    @GISNumber              VARCHAR(50) = NULL,
    @licenceNumber          VARCHAR(50) = NULL,
    @licenceCommencementDate DATE = NULL,
    @licenceStatusID        INT = NULL,
    @oldapplicationNumber   VARCHAR(50) = NULL,
    @newlicenceNumber       VARCHAR(50) = NULL,

    -- Search
    @searchText             VARCHAR(200) = NULL
)
AS
BEGIN
    SET NOCOUNT ON;

    /* ================= INSERT (TEMP) ================= */
    IF @Action = 'INSERT'
    BEGIN
        INSERT INTO Licence_Master_Temp
        (
            applicantName, doorNumber, floorNumber, address1, address2, address3,
            pincode, landLineNumber, mobileNumber, emailID, tradeName,
            zonalClassificationID, mohID, wardID, PropertyID,
            PIDNumber, khathaNumber, surveyNumber, street, GISNumber,
            licenceNumber, licenceCommencementDate, licenceStatusID,
            oldapplicationNumber, newlicenceNumber
        )
        VALUES
        (
            @applicantName, @doorNumber, @floorNumber, @address1, @address2, @address3,
            @pincode, @landLineNumber, @mobileNumber, @emailID, @tradeName,
            @zonalClassificationID, @mohID, @wardID, @PropertyID,
            @PIDNumber, @khathaNumber, @surveyNumber, @street, @GISNumber,
            @licenceNumber, @licenceCommencementDate, @licenceStatusID,
            @oldapplicationNumber, @newlicenceNumber
        );

        SELECT SCOPE_IDENTITY() AS tradeLicenceID;
        RETURN;
    END

    /* ================= UPDATE (TEMP) ================= */
    IF @Action = 'UPDATE'
    BEGIN
        UPDATE Licence_Master_Temp
        SET
            applicantName = @applicantName,
            doorNumber = @doorNumber,
            floorNumber = @floorNumber,
            address1 = @address1,
            address2 = @address2,
            address3 = @address3,
            pincode = @pincode,
            landLineNumber = @landLineNumber,
            mobileNumber = @mobileNumber,
            emailID = @emailID,
            tradeName = @tradeName,
            zonalClassificationID = @zonalClassificationID,
            mohID = @mohID,
            wardID = @wardID,
            PropertyID = @PropertyID,
            PIDNumber = @PIDNumber,
            khathaNumber = @khathaNumber,
            surveyNumber = @surveyNumber,
            street = @street,
            GISNumber = @GISNumber,
            licenceNumber = @licenceNumber,
            licenceCommencementDate = @licenceCommencementDate,
            licenceStatusID = @licenceStatusID,
            oldapplicationNumber = @oldapplicationNumber,
            newlicenceNumber = @newlicenceNumber
        WHERE tradeLicenceID = @tradeLicenceID;

        SELECT 1 AS Updated;
        RETURN;
    END

    /* ================= GET BY ID ================= */
    IF @Action = 'GET_BY_ID'
    BEGIN
        SELECT * FROM Licence_Master_Temp
        WHERE tradeLicenceID = @tradeLicenceID;
        RETURN;
    END

    /* ================= SEARCH ================= */
    IF @Action = 'SEARCH'
    BEGIN
        SELECT TOP 100 *
        FROM Licence_Master_Temp
        WHERE (@searchText IS NULL
               OR applicantName LIKE '%' + @searchText + '%'
               OR tradeName LIKE '%' + @searchText + '%'
               OR mobileNumber LIKE '%' + @searchText + '%');
        RETURN;
    END

    /* ================= DELETE (TEMP) ================= */
    IF @Action = 'DELETE_TEMP'
    BEGIN
        DELETE FROM Licence_Master_Temp WHERE tradeLicenceID = @tradeLicenceID;
        SELECT 1 AS Deleted;
        RETURN;
    END

    /* ================= FINAL_SUBMIT ================= */
    IF @Action = 'FINAL_SUBMIT'
    BEGIN
        INSERT INTO Licence_Master
        (
            applicantName, doorNumber, floorNumber, address1, address2, address3,
            pincode, landLineNumber, mobileNumber, emailID, tradeName,
            zonalClassificationID, mohID, wardID, PropertyID,
            PIDNumber, khathaNumber, surveyNumber, street, GISNumber,
            licenceNumber, licenceCommencementDate, licenceStatusID,
            oldapplicationNumber, newlicenceNumber
        )
        SELECT
            applicantName, doorNumber, floorNumber, address1, address2, address3,
            pincode, landLineNumber, mobileNumber, emailID, tradeName,
            zonalClassificationID, mohID, wardID, PropertyID,
            PIDNumber, khathaNumber, surveyNumber, street, GISNumber,
            licenceNumber, licenceCommencementDate, licenceStatusID,
            oldapplicationNumber, newlicenceNumber
        FROM Licence_Master_Temp
        WHERE tradeLicenceID = @tradeLicenceID;

        DECLARE @newID BIGINT = SCOPE_IDENTITY();

        SELECT @newID AS tradeLicenceID, 1 AS Submitted;
        RETURN;
    END
END
GO

PRINT 'usp_TradeLicence_CRUD updated with floorNumber support.';
GO
