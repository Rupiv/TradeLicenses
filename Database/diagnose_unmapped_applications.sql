/* =====================================================================
   diagnose_unmapped_applications.sql  —  READ-ONLY. Changes nothing.
   Shows which applications lack a GBA corporation/ward mapping and why.
   ===================================================================== */
SET NOCOUNT ON;

/* ---------- 1. Overall counts by location status ---------- */
PRINT '=== 1. Applications by GBA location status ===';
SELECT
    v.locationStatus,
    COUNT(DISTINCT la.licenceApplicationID) AS applications,
    COUNT(DISTINCT la.tradeLicenceID)       AS licences
FROM dbo.Licence_Application la
JOIN dbo.vw_LicenceCorporation v ON v.tradeLicenceID = la.tradeLicenceID
WHERE la.isActive = 'Y'
GROUP BY v.locationStatus
ORDER BY v.locationStatus;

/* ---------- 2. Applications with NO corporation at all ---------- */
PRINT '';
PRINT '=== 2. Active applications with NO corporation mapping (will show old BBMP template) ===';
SELECT
    la.licenceApplicationID,
    la.applicationNumber,
    la.licenceApplicationStatusID,
    lm.wardID AS legacyWardID,
    w.wardName AS legacyWardName,
    w.wardCode AS legacyWardCode,
    lm.mohID,
    v.locationStatus
FROM dbo.Licence_Application la
JOIN dbo.Licence_Master lm ON lm.tradeLicenceID = la.tradeLicenceID
JOIN dbo.vw_LicenceCorporation v ON v.tradeLicenceID = la.tradeLicenceID
LEFT JOIN dbo.Master_BBMPWard w ON w.wardID = lm.wardID
WHERE la.isActive = 'Y'
  AND v.locationStatus = 'UNMAPPED'
ORDER BY la.licenceApplicationStatusID, la.applicationNumber;

/* ---------- 3. Legacy wards that have NO mapping to any GBA ward ---------- */
PRINT '';
PRINT '=== 3. Legacy wards with NO GBA mapping (these cause UNMAPPED status) ===';
SELECT
    w.wardID,
    w.wardCode,
    w.wardName,
    COUNT(DISTINCT la.licenceApplicationID) AS activeApplications
FROM dbo.Master_BBMPWard w
LEFT JOIN dbo.Master_LegacyGBAWardMapping m ON m.legacyWardID = w.wardID
JOIN dbo.Licence_Master lm ON lm.wardID = w.wardID
JOIN dbo.Licence_Application la ON la.tradeLicenceID = lm.tradeLicenceID AND la.isActive = 'Y'
WHERE m.mappingID IS NULL
GROUP BY w.wardID, w.wardCode, w.wardName
ORDER BY activeApplications DESC;

/* ---------- 4. Summary of GBA ward coverage ---------- */
PRINT '';
PRINT '=== 4. GBA ward coverage summary ===';
SELECT
    (SELECT COUNT(*) FROM dbo.Master_Corporation)                     AS totalCorporations,
    (SELECT COUNT(*) FROM dbo.Master_GBAZone)                         AS totalZones,
    (SELECT COUNT(*) FROM dbo.Master_GBAWard)                         AS totalGBAWards,
    (SELECT COUNT(*) FROM dbo.Master_GBAWard WHERE legacyWardID IS NOT NULL) AS gbaWardsWithLegacyMapping,
    (SELECT COUNT(*) FROM dbo.Master_GBAWard WHERE legacyWardID IS NULL)     AS gbaWardsWithoutLegacyMapping,
    (SELECT COUNT(DISTINCT legacyWardID) FROM dbo.Master_LegacyGBAWardMapping) AS legacyWardsMapped,
    (SELECT COUNT(*) FROM dbo.Master_BBMPWard w
     WHERE NOT EXISTS (SELECT 1 FROM dbo.Master_LegacyGBAWardMapping m WHERE m.legacyWardID = w.wardID))
                                                                      AS legacyWardsUnmapped;

/* ---------- 5. Corporation-wise application count ---------- */
PRINT '';
PRINT '=== 5. Applications by corporation (CONFIRMED + DERIVED only) ===';
SELECT
    c.corporationName,
    v.locationStatus,
    COUNT(DISTINCT la.licenceApplicationID) AS applications
FROM dbo.Licence_Application la
JOIN dbo.vw_LicenceCorporation v ON v.tradeLicenceID = la.tradeLicenceID
LEFT JOIN dbo.Master_Corporation c ON c.corporationID = v.corporationID
WHERE la.isActive = 'Y'
  AND v.locationStatus IN ('CONFIRMED', 'DERIVED')
GROUP BY c.corporationName, v.locationStatus
ORDER BY c.corporationName, v.locationStatus;

/* ---------- 6. Approved applications with no GBA ward (certificate will show BBMP) ---------- */
PRINT '';
PRINT '=== 6. APPROVED applications with no GBA mapping (certificate shows old BBMP template) ===';
SELECT
    la.licenceApplicationID,
    la.applicationNumber,
    lm.wardID AS legacyWardID,
    w.wardName AS legacyWardName,
    w.wardCode AS legacyWardCode,
    v.locationStatus
FROM dbo.Licence_Application la
JOIN dbo.Licence_Master lm ON lm.tradeLicenceID = la.tradeLicenceID
JOIN dbo.vw_LicenceCorporation v ON v.tradeLicenceID = la.tradeLicenceID
LEFT JOIN dbo.Master_BBMPWard w ON w.wardID = lm.wardID
WHERE la.isActive = 'Y'
  AND la.licenceApplicationStatusID = 3   -- APPROVED
  AND v.locationStatus = 'UNMAPPED'
ORDER BY la.applicationNumber;

PRINT '';
PRINT '=== Done. If Section 3 shows legacy wards, they need mappings in Master_LegacyGBAWardMapping ===';
