/* =====================================================================================
   READ ONLY - export the definitions the security work needs (no data, no changes).
   Run on STAGING (or production, read-only) as a login with VIEW DEFINITION.
   Send the output (definitions only - they contain no customer data) to the developer.

   Why each one is needed
   - usp_Report_ControlSheet, usp_Report_WardWiseLicenses, usp_Report_RevenueCollection:
       to limit the three reports to an officer's GBA corporation / zone / ward. Until then the
       API allows them to admins only.
   - usp_LicenceTradeDetails_CRUD:
       to find which table and column link a draft trade row (licenceTradeDetailsID) to its draft
       application, so update / delete by row id can be limited to the owner.
   - sp_GetTradeLicenceApplications_Approver / _SeniorApprover / _ZoneApprover:
       the legacy Office -> MOH routing of officers WITHOUT a GBA assignment, so the same rule can
       be applied when such an officer opens an application by id.
   - sp_GetTradeLicenceApplications_Admin: to confirm the live version equals
       Database/GBA_AdminSearch_Fix.sql (the API relies on its @CorporationID filter).
   ===================================================================================== */
SET NOCOUNT ON;

SELECT o.name AS procedure_name,
       o.modify_date,
       m.definition
FROM sys.objects o
JOIN sys.sql_modules m ON m.object_id = o.object_id
WHERE o.type = 'P'
  AND o.name IN (
      'usp_Report_ControlSheet',
      'usp_Report_WardWiseLicenses',
      'usp_Report_RevenueCollection',
      'usp_LicenceTradeDetails_CRUD',
      'sp_GetTradeLicenceApplications_Approver',
      'sp_GetTradeLicenceApplications_SeniorApprover',
      'sp_GetTradeLicenceApplications_ZoneApprover',
      'sp_GetTradeLicenceApplications_Admin')
ORDER BY o.name;

-- Columns of every table those procedures read or write (structure only)
SELECT DISTINCT
       OBJECT_NAME(d.referencing_id) AS used_by,
       d.referenced_entity_name     AS table_name,
       c.name                       AS column_name,
       TYPE_NAME(c.user_type_id)    AS data_type,
       c.is_nullable
FROM sys.sql_expression_dependencies d
JOIN sys.objects t ON t.name = d.referenced_entity_name AND t.type = 'U'
JOIN sys.columns c ON c.object_id = t.object_id
WHERE OBJECT_NAME(d.referencing_id) IN (
      'usp_Report_ControlSheet', 'usp_Report_WardWiseLicenses', 'usp_Report_RevenueCollection',
      'usp_LicenceTradeDetails_CRUD')
ORDER BY used_by, table_name, column_name;

-- Keys of the draft tables (needed for the trade-row ownership check and the HP table foreign key)
SELECT t.name AS table_name, i.name AS index_name, i.is_primary_key, c.name AS column_name
FROM sys.tables t
JOIN sys.indexes i ON i.object_id = t.object_id AND (i.is_primary_key = 1 OR i.is_unique = 1)
JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
WHERE t.name IN ('Licence_Application_Temp', 'Licence_TradeDetails', 'Licence_TradeDetails_Temp',
                 'GeneratedLicenceCertificate', 'LicenceGeneratedCertificate')
ORDER BY t.name, i.name;
