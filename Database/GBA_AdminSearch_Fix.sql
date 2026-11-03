/* =====================================================================
   CHANGE: Admin "Licence Applications" search box matches Mobile / Applicant / Trade too
   ---------------------------------------------------------------------
   The search box placeholder reads "Application No / Applicant / Trade / Mobile / Status",
   but sp_GetTradeLicenceApplications_Admin only ever matched @ApplicationNumber against
   la.applicationNumber - searching by mobile number or applicant/trade name always returned
   zero rows (confirmed 30-Oct-2026 on licenceApplicationID 945317, mobile 9844012314).
   Change: STEP 1's filter now also matches Licence_Master.mobileNumber / applicantName /
   tradeName via a LEFT JOIN. Everything else (zone/MOH/ward filter, GBA corporation filter,
   dashboard mode, paging, sorting, output columns) is unchanged.
   Rollback: GBA_AdminSearch_Rollback.sql (exact previous version).
   Already applied live on 49.50.97.218 on 30-Oct-2026 - run this only against an environment
   that doesn't already have it (e.g. a separate production database).
   ===================================================================== */
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
    @TotalCount INT OUTPUT,

    @CorporationID INT = NULL       -- GBA corporation (confirmed, else derived from legacy ward)
)
AS
BEGIN
    SET NOCOUNT ON;

    IF @PageNumber < 1 SET @PageNumber = 1;
    IF @PageSize < 1 SET @PageSize = 10;

    DECLARE @Offset INT = (@PageNumber - 1) * @PageSize;

    /* =====================================================
       STEP 1 : FILTER FIRST (LIGHT DATASET)
       Search box matches Application Number, Mobile, Applicant Name or Trade Name
       (was Application Number only - everything else below is unchanged).
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
    LEFT JOIN Licence_Master lmSearch
        ON lmSearch.tradeLicenceID = la.tradeLicenceID
    WHERE
        (@LicenceApplicationID IS NULL
         OR la.licenceApplicationID = @LicenceApplicationID)
        AND (
                @ApplicationNumber IS NULL
                OR la.applicationNumber LIKE '%' + @ApplicationNumber + '%'
                OR lmSearch.mobileNumber LIKE '%' + @ApplicationNumber + '%'
                OR lmSearch.applicantName LIKE '%' + @ApplicationNumber + '%'
                OR lmSearch.tradeName LIKE '%' + @ApplicationNumber + '%'
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

    /* STEP 2B : GBA CORPORATION FILTER */
    IF @CorporationID IS NOT NULL
        DELETE f
        FROM #Filtered f
        WHERE NOT EXISTS (
            SELECT 1 FROM dbo.vw_LicenceCorporation v
            WHERE v.tradeLicenceID = f.tradeLicenceID
              AND v.corporationID = @CorporationID);

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

        /* page first, then GBA columns for just that page */
        SELECT
            p.*,
            v.corporationID,
            c.corporationName,
            CASE WHEN gw.gbaWardID IS NULL THEN NULL
                 ELSE CAST(gw.wardNo AS VARCHAR(10)) + ' - ' + gw.wardName END AS gbaWardName,
            v.locationStatus
        FROM (
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
            FETCH NEXT @PageSize ROWS ONLY
        ) p
        LEFT JOIN dbo.vw_LicenceCorporation v
            ON v.tradeLicenceID = p.tradeLicenceID
        LEFT JOIN dbo.Master_Corporation c
            ON c.corporationID = v.corporationID
        LEFT JOIN dbo.Master_GBAWard gw
            ON gw.gbaWardID = v.gbaWardID
        ORDER BY p.applicationSubmitDate DESC;

        RETURN;
    END
END
GO
