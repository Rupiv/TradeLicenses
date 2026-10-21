/* Restores sp_GetTradeLicenceApplications_Admin exactly as it was before GBA_StoredProcedure_Updates.sql
   (definition exported from the TradeLicence DB on 2026-10-13). */
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

ALTER PROCEDURE [dbo].[sp_GetTradeLicenceApplications_Admin]
(
    @Action VARCHAR(20) = 'LIST',   -- NEW

    @ZoneID INT = NULL,
    @MohID INT = NULL,
    @WardID INT = NULL,
    @LicenceApplicationID INT = NULL,
    @ApplicationNumber NVARCHAR(50) = NULL,

    @PageNumber INT = 1,
    @PageSize INT = 10,
    @TotalCount INT OUTPUT
)
AS
BEGIN
    SET NOCOUNT ON;

    IF @PageNumber < 1 SET @PageNumber = 1;
    IF @PageSize < 1 SET @PageSize = 10;

    DECLARE @Offset INT = (@PageNumber - 1) * @PageSize;

    /* =====================================================
       STEP 1 : FILTER FIRST (LIGHT DATASET)
    ===================================================== */
    IF OBJECT_ID('tempdb..#Filtered') IS NOT NULL
        DROP TABLE #Filtered;

    SELECT
        la.licenceApplicationID,
        la.tradeLicenceID,
        la.applicationNumber,
        la.applicationSubmitDate,
        la.licenceApplicationStatusID,
        la.loginID
    INTO #Filtered
    FROM Licence_Application la
    WHERE
        (@LicenceApplicationID IS NULL
         OR la.licenceApplicationID = @LicenceApplicationID)
        AND (
                @ApplicationNumber IS NULL
                OR la.applicationNumber LIKE '%' + @ApplicationNumber + '%'
            );

    /* =====================================================
       STEP 2 : APPLY MASTER FILTERS (ZONE/MOH/WARD)
    ===================================================== */
    DELETE f
    FROM #Filtered f
    INNER JOIN Licence_Master lm
        ON f.tradeLicenceID = lm.tradeLicenceID
    INNER JOIN moh m
        ON lm.mohID = m.mohcd
    INNER JOIN Master_BBMPZone z
        ON m.majzonecd = z.zoneCode
    INNER JOIN Master_BBMPWard w
        ON lm.wardID = w.wardID
    WHERE
        (@ZoneID IS NOT NULL AND z.zoneID <> @ZoneID)
        OR (@MohID IS NOT NULL AND m.mohcd <> @MohID)
        OR (@WardID IS NOT NULL AND w.wardID <> @WardID);

    /* =====================================================
       DASHBOARD MODE
    ===================================================== */
    IF @Action = 'DASHBOARD'
    BEGIN
        SELECT
            SUM(CASE WHEN licenceApplicationStatusID = 1 THEN 1 ELSE 0 END) AS Entered,
            SUM(CASE WHEN licenceApplicationStatusID = 2 THEN 1 ELSE 0 END) AS Applied,
            SUM(CASE WHEN licenceApplicationStatusID = 3 THEN 1 ELSE 0 END) AS Approved,
            SUM(CASE WHEN licenceApplicationStatusID = 4 THEN 1 ELSE 0 END) AS Rejected,
            SUM(CASE WHEN licenceApplicationStatusID = 5 THEN 1 ELSE 0 END) AS Objection,
            SUM(CASE WHEN licenceApplicationStatusID = 6 THEN 1 ELSE 0 END) AS Forwarded,
            SUM(CASE WHEN licenceApplicationStatusID = 7 THEN 1 ELSE 0 END) AS Inspected,
            COUNT(1) AS GrandTotal
        FROM #Filtered;

        RETURN;
    END


    /* =====================================================
       LIST MODE
    ===================================================== */
    IF @Action = 'LIST'
    BEGIN
        SELECT @TotalCount = COUNT(1)
        FROM #Filtered;

        SELECT
            f.licenceApplicationID,
            f.applicationNumber,
            f.applicationSubmitDate,

            f.licenceApplicationStatusID,
            ISNULL(mp.licenceProcessName,
                   CONCAT('STATUS-', f.licenceApplicationStatusID))
                AS licenceApplicationStatusName,

            lm.tradeLicenceID,
            lm.applicantName,
            lm.tradeName,
            lm.mobileNumber,
            lm.emailID,

            z.zoneID,
            z.zoneName,

            m.mohcd AS mohID,
            m.mohname AS mohName,

            w.wardID,
            w.wardName,

            f.loginID

        FROM #Filtered f
        LEFT JOIN Licence_Master lm
            ON f.tradeLicenceID = lm.tradeLicenceID
        LEFT JOIN moh m
            ON lm.mohID = m.mohcd
        LEFT JOIN Master_BBMPZone z
            ON m.majzonecd = z.zoneCode
        LEFT JOIN Master_BBMPWard w
            ON lm.wardID = w.wardID
        LEFT JOIN Master_LicenceProcess mp
            ON f.licenceApplicationStatusID = mp.licenceProcessID

        ORDER BY f.applicationSubmitDate DESC
        OFFSET @Offset ROWS
        FETCH NEXT @PageSize ROWS ONLY;

        RETURN;
    END
END

GO
