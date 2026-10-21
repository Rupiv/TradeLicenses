/* Rollback for GBA_SeniorApprovalRule_Fix.sql: restores the exact previous
   usp_Licence_SubmitAction (taken from the live DB on 15-Oct-2026). */
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
ALTER PROCEDURE [dbo].[usp_Licence_SubmitAction]
(
    @Mode VARCHAR(10),
    @LicenceApplicationID INT = NULL,
    @LoginID INT = NULL,
    @LicenceProcessID INT = NULL,
    @CurrentStatus VARCHAR(50) = NULL,   -- not used for DB storage
    @Remarks NVARCHAR(1000) = NULL,
    @ActionReasonIds VARCHAR(200) = NULL
)
AS
BEGIN
    SET NOCOUNT ON;

    /* =====================================================
       GET FULL FLOW / TIMELINE
       ===================================================== */
    IF @Mode = 'GET'
    BEGIN
        IF @LicenceApplicationID IS NULL
        BEGIN
            SELECT 0 AS Success,
                   'LicenceApplicationID is required for GET mode.' AS Message;
            RETURN;
        END

        SELECT
            lf.licenceFlowID,
            lf.licenceApplicationID,
            lf.loginID,
            lm.login AS UpdatedByUser,
            mp.licenceProcessName,
            las.licenceApplicationStatusName AS StatusName,
            lf.remarks,
            lf.ActionReasonIds,
            lf.entryDate
        FROM Licence_Flow lf
        INNER JOIN Master_LicenceProcess mp
            ON lf.licenceProcessID = mp.licenceProcessID
        INNER JOIN Licence_ApplicationStatus las
            ON lf.Status = las.licenceApplicationStatusID
        INNER JOIN Login_Master lm
            ON lf.loginID = lm.loginID
        WHERE lf.licenceApplicationID = @LicenceApplicationID
        ORDER BY lf.entryDate ASC, lf.licenceFlowID ASC;

        RETURN;
    END


    /* =====================================================
       SUBMIT ACTION
       ===================================================== */
    IF @Mode = 'SUBMIT'
    BEGIN
        BEGIN TRY
            BEGIN TRANSACTION;

            /* ================= VALIDATIONS ================= */

            IF @LicenceApplicationID IS NULL
               OR @LoginID IS NULL
               OR @LicenceProcessID IS NULL
            BEGIN
                RAISERROR('Missing mandatory parameters for SUBMIT action.',16,1);
            END

            IF NOT EXISTS (
                SELECT 1
                FROM Licence_Application
                WHERE licenceApplicationID = @LicenceApplicationID
            )
            BEGIN
                RAISERROR('Invalid LicenceApplicationID.',16,1);
            END

            IF NOT EXISTS (
                SELECT 1
                FROM Master_LicenceProcess
                WHERE licenceProcessID = @LicenceProcessID
                  AND isActive = 'Y'
            )
            BEGIN
                RAISERROR('Invalid or inactive LicenceProcessID.',16,1);
            END


            /* ================= APPROVAL RULE ================= */
            IF @LicenceProcessID = 3
               AND NOT EXISTS (
                    SELECT 1
                    FROM Licence_Flow
                    WHERE licenceApplicationID = @LicenceApplicationID
                      AND licenceProcessID = 7
               )
            BEGIN
                RAISERROR('Inspection not completed. Approval not allowed.',16,1);
            END


            /* ================= PREVENT DUPLICATE ACTION ================= */
            IF EXISTS
            (
                SELECT 1
                FROM (
                    SELECT TOP (1) licenceProcessID
                    FROM Licence_Flow
                    WHERE licenceApplicationID = @LicenceApplicationID
                    ORDER BY entryDate DESC, licenceFlowID DESC
                ) x
                WHERE x.licenceProcessID = @LicenceProcessID
            )
            BEGIN
                RAISERROR('This action is already the latest status for the application.',16,1);
            END


            /* =====================================================
               1️⃣ INSERT INTO LICENCE FLOW
               ===================================================== */
            INSERT INTO Licence_Flow
            (
                licenceApplicationID,
                loginID,
                entryDate,
                licenceProcessID,
                remarks,
                Status,
                ActionReasonIds
            )
            VALUES
            (
                @LicenceApplicationID,
                @LoginID,
                GETDATE(),
                @LicenceProcessID,
                @Remarks,
                @LicenceProcessID,
                @ActionReasonIds
            );


            /* =====================================================
               2️⃣ INSERT STATUS HISTORY
               ===================================================== */
            INSERT INTO Licence_ApplicationStatusDetails
            (
                licenceApplicationID,
                licenceApplicationStatusID,
                currentStatus,
                entryDate,
                Remarks
            )
            VALUES
            (
                @LicenceApplicationID,
                @LicenceProcessID,
                @LicenceProcessID,  -- INT ONLY
                GETDATE(),
                @Remarks
            );


            /* =====================================================
               3️⃣ UPDATE LIVE STATUS
               ===================================================== */
            UPDATE Licence_Application
            SET
                licenceApplicationStatusID = @LicenceProcessID,
                currentStatus = @LicenceProcessID, -- INT ONLY
                ChangesDate = GETDATE(),
                ChangesRemarks = @Remarks
            WHERE licenceApplicationID = @LicenceApplicationID;


            COMMIT TRANSACTION;

            SELECT
                1 AS Success,
                'Action submitted successfully' AS Message,
                @LicenceApplicationID AS LicenceApplicationID,
                @LicenceProcessID AS LicenceProcessID;

        END TRY
        BEGIN CATCH
            IF @@TRANCOUNT > 0
                ROLLBACK TRANSACTION;

            SELECT
                0 AS Success,
                ERROR_MESSAGE() AS Message;
        END CATCH

        RETURN;
    END

    SELECT 0 AS Success, 'Invalid @Mode. Use GET or SUBMIT.' AS Message;
END
GO
