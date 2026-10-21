/* Rollback for GBA_ApproverZoneFilter_Fix.sql: restores the exact previous
   sp_GetTradeLicenceApplications_Approver (taken from the live DB on 13-Oct-2026). */
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
ALTER PROCEDURE [dbo].[sp_GetTradeLicenceApplications_Approver]
(
    @Action VARCHAR(20),
    @LoginID INT,
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

    /* ================= VALIDATION ================= */
    IF @PageNumber < 1 SET @PageNumber = 1;
    IF @PageSize < 1 SET @PageSize = 10;

    DECLARE @ZoneCode INT;
    DECLARE @Offset INT = (@PageNumber - 1) * @PageSize;

    /* ================= GET ZONE ================= */
    SELECT TOP 1
        @ZoneCode = m.majzonecd
    FROM Login_Master lm
    INNER JOIN Master_OfficeDetails od
        ON lm.officeDetailsID = od.officeDetailsID
    INNER JOIN moh m
        ON LTRIM(RTRIM(od.officeName)) = LTRIM(RTRIM(m.mohname))
    WHERE lm.loginID = @LoginID;

    /* ================= FILTER DATA ================= */
    IF OBJECT_ID('tempdb..#Filtered') IS NOT NULL
        DROP TABLE #Filtered;

    SELECT
        la.licenceApplicationID,
        la.tradeLicenceID,
        la.applicationNumber,
        la.applicationSubmitDate,
        la.ChangesDate,
        la.licenceApplicationStatusID
    INTO #Filtered
    FROM Licence_Application la
    INNER JOIN Licence_Master lm2
        ON la.tradeLicenceID = lm2.tradeLicenceID
    WHERE
        la.licenceApplicationStatusID IN (2,4,5)   -- APPLIED, REJECTED, OBJECTION
        AND (@LicenceApplicationID IS NULL 
             OR la.licenceApplicationID = @LicenceApplicationID)
        AND (
                @ApplicationNumber IS NULL
                OR la.applicationNumber LIKE '%' + @ApplicationNumber + '%'
                OR lm2.applicantName LIKE '%' + @ApplicationNumber + '%'
                OR lm2.tradeName LIKE '%' + @ApplicationNumber + '%'
                OR lm2.mobileNumber LIKE '%' + @ApplicationNumber + '%'
            );

    /* ================= APPLY ZONE/MOH/WARD FILTER ================= */
    DELETE f
    FROM #Filtered f
    INNER JOIN Licence_Master_Temp lmt
        ON f.tradeLicenceID = lmt.tradeLicenceID
    INNER JOIN moh m
        ON lmt.mohID = m.mohcd
    INNER JOIN Master_BBMPWard w
        ON lmt.wardID = w.wardID
    WHERE
        (@ZoneCode IS NOT NULL AND m.majzonecd <> @ZoneCode)
        OR (@MohID IS NOT NULL AND m.mohcd <> @MohID)
        OR (@WardID IS NOT NULL AND w.wardID <> @WardID);

    /* ===================================================== */
    /* ================= DASHBOARD MODE ==================== */
    /* ===================================================== */
    IF @Action = 'DASHBOARD'
    BEGIN
        SELECT 
            SUM(CASE WHEN licenceApplicationStatusID = 2 THEN 1 ELSE 0 END) AS TotalApplied,
            SUM(CASE WHEN licenceApplicationStatusID = 5 THEN 1 ELSE 0 END) AS TotalObjection,
            SUM(CASE WHEN licenceApplicationStatusID = 4 THEN 1 ELSE 0 END) AS TotalRejected,
            COUNT(1) AS GrandTotal
        FROM #Filtered;

        RETURN;
    END

    /* ===================================================== */
    /* ================= LIST MODE ========================= */
    /* ===================================================== */
    IF @Action = 'LIST'
    BEGIN
        SELECT @TotalCount = COUNT(1)
        FROM #Filtered;

        SELECT
            f.licenceApplicationID,
            f.applicationNumber,
            f.applicationSubmitDate,
            f.ChangesDate,
            f.licenceApplicationStatusID,
            ISNULL(mp.licenceProcessName,
                   CONCAT('STATUS-', f.licenceApplicationStatusID))
                AS licenceApplicationStatusName,
            lm.tradeLicenceID,
            lm.applicantName,
            lm.tradeName,
            lm.mobileNumber,
            lm.emailID,
            m.mohcd AS mohID,
            m.mohname AS mohName,
            w.wardID,
            w.wardName,
            CAST(geo.Latitude AS DECIMAL(18,6)) AS Latitude,
            CAST(geo.Longitude AS DECIMAL(18,6)) AS Longitude,
            CAST(geo.RoadID AS NVARCHAR(200)) AS RoadID,
            CAST(geo.RoadWidthMtrs AS INT) AS RoadWidthMtrs,
            CAST(geo.RoadCategoryCode AS NVARCHAR(100)) AS RoadCategoryCode,
            CAST(geo.RoadCategory AS NVARCHAR(200)) AS RoadCategory,
            CAST(ISNULL(geo.IsConfirmed,0) AS BIT) AS IsConfirmed,
            geo.EntryDate
        FROM #Filtered f
        INNER JOIN Licence_Master lm
            ON f.tradeLicenceID = lm.tradeLicenceID
        INNER JOIN moh m
            ON lm.mohID = m.mohcd
        INNER JOIN Master_BBMPWard w
            ON lm.wardID = w.wardID
        LEFT JOIN Master_LicenceProcess mp
            ON f.licenceApplicationStatusID = mp.licenceProcessID
        LEFT JOIN Licence_Application_GeoLocation geo
            ON geo.LicenceApplicationID = f.licenceApplicationID
        ORDER BY ISNULL(f.ChangesDate, f.applicationSubmitDate) DESC
        OFFSET @Offset ROWS
        FETCH NEXT @PageSize ROWS ONLY;

        RETURN;
    END

    /* ===================================================== */
    /* ================= LOOKUP MODE ======================= */
    /* ===================================================== */
    IF @Action = 'LOOKUP'
    BEGIN
        IF @ZoneCode IS NULL
        BEGIN
            SELECT CAST(NULL AS INT) AS ZoneID,
                   CAST(NULL AS NVARCHAR(200)) AS ZoneName
            WHERE 1 = 0;

            SELECT CAST(NULL AS INT) AS WardID,
                   CAST(NULL AS NVARCHAR(200)) AS WardName
            WHERE 1 = 0;

            RETURN;
        END

        /* ZONE LIST */
        SELECT 
            z.zoneID AS ZoneID,
            z.zoneName AS ZoneName
        FROM Master_BBMPZone z
        WHERE z.zoneID = @ZoneCode;

        /* WARD LIST */
        SELECT 
            w.wardID AS WardID,
            w.wardName AS WardName
        FROM Master_BBMPWard w
        WHERE w.zoneID = @ZoneCode
        ORDER BY w.wardName;

        RETURN;
    END

END
GO
