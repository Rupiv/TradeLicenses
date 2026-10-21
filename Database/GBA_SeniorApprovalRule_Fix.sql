/* =====================================================================
   CHANGE: Senior approver may approve applications the inspector FORWARDED
   ---------------------------------------------------------------------
   usp_Licence_SubmitAction refused approval (process 3) unless the application
   had an INSPECTED (7) step. Business rule (15-Oct-2026): the inspecting officer
   may forward an application directly to the higher authority (6), and that is
   enough for approval. This also unblocks old-system applications, which went
   ENTERED -> FORWARD with no INSPECTED step (5,544 waiting on 15-Oct-2026).
   Change: only the APPROVAL RULE (7 -> 7 or 6). No table data is changed.
   Rollback: GBA_SeniorApprovalRule_Rollback.sql (exact previous version).
   ===================================================================== */
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
            -- GBA rule: approval needs the inspecting officer's action first - either INSPECTED (7)
            -- or FORWARD TO HIGHER AUTHORITY (6). The inspector may forward directly to the higher
            -- authority; old-system applications also reached the senior approver via FORWARD only.
            IF @LicenceProcessID = 3
               AND NOT EXISTS (
                    SELECT 1
                    FROM Licence_Flow
                    WHERE licenceApplicationID = @LicenceApplicationID
                      AND licenceProcessID IN (7, 6)
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
               1ï¸âƒ£ INSERT INTO LICENCE FLOW
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
               2ï¸âƒ£ INSERT STATUS HISTORY
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
               3ï¸âƒ£ UPDATE LIVE STATUS
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
