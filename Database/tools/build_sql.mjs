// Generates the GBA corporation/ward migration + rollback SQL from the mapping CSVs.
// Usage: node build_sql.mjs <scratchDir> <outDir>
import fs from 'node:fs';
import path from 'node:path';

const [dir, outDir] = process.argv.slice(2);

function parseCsv(text) {
  text = text.replace(/^﻿/, '');
  const rows = []; let row = []; let cur = ''; let q = false;
  for (let i = 0; i < text.length; i++) {
    const c = text[i];
    if (q) { if (c === '"' && text[i + 1] === '"') { cur += '"'; i++; } else if (c === '"') q = false; else cur += c; }
    else if (c === '"') q = true;
    else if (c === ',') { row.push(cur); cur = ''; }
    else if (c === '\n' || c === '\r') { if (c === '\r' && text[i + 1] === '\n') i++; row.push(cur); cur = ''; if (row.some(v => v !== '')) rows.push(row); row = []; }
    else cur += c;
  }
  if (cur !== '' || row.length) { row.push(cur); rows.push(row); }
  const [h, ...data] = rows;
  return data.map(r => Object.fromEntries(h.map((k, i) => [k.trim(), (r[i] ?? '').trim()])));
}
const read = f => parseCsv(fs.readFileSync(path.join(dir, f), 'utf8'));

const gba = read('gba_ward_master.csv');
const map = read('legacy_to_gba_ward_mapping.csv');
const legacy = read('legacy_wards.csv');
const moh = read('legacy_moh.csv');

const s = v => `'${String(v).replace(/'/g, "''")}'`;
const n = v => `N'${String(v).replace(/'/g, "''")}'`;
const int = v => (v === '' || v == null ? 'NULL' : String(Number(v)));
const wardCodeById = new Map(legacy.map(l => [l.wardID, l.wardCode]));
const mohCodeById = new Map(moh.map(m => [m.mohID, m.mohCode]));

const corps = [...new Map(gba.map(g => [g.corporationId, g])).values()].sort((a, b) => a.corporationId - b.corporationId);
const zones = [...new Map(gba.map(g => [g.corporation + '|' + g.zone_name, g])).values()];

const chunk = (arr, size) => Array.from({ length: Math.ceil(arr.length / size) }, (_, i) => arr.slice(i * size, i * size + size));

const wardRows = gba.map(g =>
  `(${int(g.corporationId)}, ${s(g.zone_name)}, ${int(g.wardNo)}, ${s(g.ward_name)}, ${n(g.ward_name_kn)}, ${int(g.ac_no)}, ${s(g.ac)}, ${n(g.ac_kn)}, ${int(wardCodeById.get(g.legacyWardID) ?? '')}, ${int(g.mohID)}, ${s(g.matchMethod)})`);
const mapRows = map.map(m =>
  `(${int(wardCodeById.get(m.legacyWardID))}, ${int(m.corporationId)}, ${int(m.wardNo)}, ${int(m.isPrimary)}, ${s(m.matchMethod)}, ${m.matchScore})`);

const insertChunks = (table, cols, rows) => chunk(rows, 500).map(c =>
  `INSERT INTO ${table} (${cols}) VALUES\n    ${c.join(',\n    ')};`).join('\n');

const migration = `/* =====================================================================
   GBA: 5 corporations / 10 zones / 369 wards  +  legacy (198 ward) mapping
   ---------------------------------------------------------------------
   ADDITIVE ONLY. Creates new tables + a view. Does NOT alter, update or
   delete anything in existing tables (Licence_Master, Licence_Application,
   Master_BBMPWard, Master_BBMPZone, Master_MOH, ...).

   Safe to re-run: every object is created only if missing, seed rows are
   inserted only if missing.

   Legacy wards and ACs are resolved by CODE (wardCode, constituencyCode);
   MOH by mohID, the value Licence_Master.mohID and the routing SPs use.

   Run on a TEST copy first. Rollback: GBA_Corporation_Ward_Rollback.sql
   Generated from ward_369_master.xlsx + mapping draft (see Database/seed).
   ===================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;          -- required for the filtered unique indexes
SET QUOTED_IDENTIFIER ON;   -- (sqlcmd defaults this to OFF)
BEGIN TRANSACTION;

/* ---------- 1. Corporations (IDs = Payment_Gateway_Config.CorporationId) ---------- */
IF OBJECT_ID('dbo.Master_Corporation', 'U') IS NULL
CREATE TABLE dbo.Master_Corporation (
    corporationID         INT           NOT NULL CONSTRAINT PK_Master_Corporation PRIMARY KEY,
    corporationName       VARCHAR(64)   NOT NULL CONSTRAINT UQ_Master_Corporation_Name UNIQUE,
    corporationNativeName NVARCHAR(128) NOT NULL CONSTRAINT DF_Master_Corporation_Native DEFAULT (N''),
    isActive              BIT           NOT NULL CONSTRAINT DF_Master_Corporation_IsActive DEFAULT (1),
    entryDate             DATETIME      NOT NULL CONSTRAINT DF_Master_Corporation_EntryDate DEFAULT (GETDATE())
);

/* ---------- 2. Zones inside each corporation ---------- */
IF OBJECT_ID('dbo.Master_GBAZone', 'U') IS NULL
CREATE TABLE dbo.Master_GBAZone (
    gbaZoneID      INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Master_GBAZone PRIMARY KEY,
    corporationID  INT           NOT NULL CONSTRAINT FK_Master_GBAZone_Corporation REFERENCES dbo.Master_Corporation(corporationID),
    zoneName       VARCHAR(64)   NOT NULL,
    zoneNativeName NVARCHAR(128) NOT NULL CONSTRAINT DF_Master_GBAZone_Native DEFAULT (N''),
    isActive       BIT           NOT NULL CONSTRAINT DF_Master_GBAZone_IsActive DEFAULT (1),
    entryDate      DATETIME      NOT NULL CONSTRAINT DF_Master_GBAZone_EntryDate DEFAULT (GETDATE()),
    CONSTRAINT UQ_Master_GBAZone_Corp_Name UNIQUE (corporationID, zoneName)
);

/* ---------- 3. GBA wards (ward numbers restart per corporation) ----------
   legacyWardID / mohID are what new applications store in the existing
   NOT NULL columns Licence_Master.wardID and *.mohID, so every existing
   report, FK and stored procedure keeps working unchanged.               */
IF OBJECT_ID('dbo.Master_GBAWard', 'U') IS NULL
CREATE TABLE dbo.Master_GBAWard (
    gbaWardID          INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Master_GBAWard PRIMARY KEY,
    corporationID      INT           NOT NULL CONSTRAINT FK_Master_GBAWard_Corporation REFERENCES dbo.Master_Corporation(corporationID),
    gbaZoneID          INT           NOT NULL CONSTRAINT FK_Master_GBAWard_Zone REFERENCES dbo.Master_GBAZone(gbaZoneID),
    wardNo             INT           NOT NULL,
    wardName           VARCHAR(100)  NOT NULL,
    wardNativeName     NVARCHAR(200) NOT NULL CONSTRAINT DF_Master_GBAWard_Native DEFAULT (N''),
    assemblyNo         INT           NOT NULL,
    assemblyName       VARCHAR(64)   NOT NULL,
    assemblyNativeName NVARCHAR(128) NOT NULL CONSTRAINT DF_Master_GBAWard_AcNative DEFAULT (N''),
    constituencyID     INT           NULL CONSTRAINT FK_Master_GBAWard_Constituency REFERENCES dbo.Master_AssemblyConstituency(constituencyID),
    legacyWardID       INT           NULL CONSTRAINT FK_Master_GBAWard_LegacyWard REFERENCES dbo.Master_BBMPWard(wardID),
    mohID              INT           NULL CONSTRAINT FK_Master_GBAWard_MOH REFERENCES dbo.Master_MOH(mohID),
    matchMethod        VARCHAR(20)   NULL,      -- NAME_EXACT / NAME_FUZZY / AC_DEFAULT / NO_MATCH (draft)
    mappingVerified    BIT           NOT NULL CONSTRAINT DF_Master_GBAWard_Verified DEFAULT (0),
    isActive           BIT           NOT NULL CONSTRAINT DF_Master_GBAWard_IsActive DEFAULT (1),
    entryDate          DATETIME      NOT NULL CONSTRAINT DF_Master_GBAWard_EntryDate DEFAULT (GETDATE()),
    CONSTRAINT UQ_Master_GBAWard_Corp_WardNo UNIQUE (corporationID, wardNo)
);

/* ---------- 4. Legacy ward -> candidate GBA wards (existing licences) ---------- */
IF OBJECT_ID('dbo.Master_LegacyGBAWardMapping', 'U') IS NULL
CREATE TABLE dbo.Master_LegacyGBAWardMapping (
    mappingID    INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Master_LegacyGBAWardMapping PRIMARY KEY,
    legacyWardID INT          NOT NULL CONSTRAINT FK_LegacyGBAWardMapping_LegacyWard REFERENCES dbo.Master_BBMPWard(wardID),
    gbaWardID    INT          NOT NULL CONSTRAINT FK_LegacyGBAWardMapping_GBAWard REFERENCES dbo.Master_GBAWard(gbaWardID),
    isPrimary    BIT          NOT NULL CONSTRAINT DF_LegacyGBAWardMapping_IsPrimary DEFAULT (0),
    matchMethod  VARCHAR(20)  NULL,             -- PRIMARY_OF_GBA / AC_ONLY (draft)
    matchScore   DECIMAL(4,2) NULL,
    isVerified   BIT          NOT NULL CONSTRAINT DF_LegacyGBAWardMapping_Verified DEFAULT (0),
    entryDate    DATETIME     NOT NULL CONSTRAINT DF_LegacyGBAWardMapping_EntryDate DEFAULT (GETDATE()),
    CONSTRAINT UQ_LegacyGBAWardMapping UNIQUE (legacyWardID, gbaWardID)
);

/* ---------- 5. Confirmed GBA location per licence (side table) ----------
   A new application is a draft in Licence_Master_Temp (tempTradeLicenceID) until
   FINAL_SUBMIT copies it into Licence_Master under a NEW id; the API then fills
   tradeLicenceID on the same row. Renewals of existing licences set tradeLicenceID
   directly. No FK to Licence_Master_Temp so draft deletes keep working.
   Kept out of Licence_Master on purpose so its triggers / SELECT * code are untouched. */
IF OBJECT_ID('dbo.Licence_GBALocation', 'U') IS NULL
CREATE TABLE dbo.Licence_GBALocation (
    locationID         INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Licence_GBALocation PRIMARY KEY,
    tempTradeLicenceID BIGINT      NULL,   -- Licence_Master_Temp.tradeLicenceID (draft)
    tradeLicenceID     BIGINT      NULL CONSTRAINT FK_Licence_GBALocation_Licence REFERENCES dbo.Licence_Master(tradeLicenceID),
    corporationID      INT         NOT NULL CONSTRAINT FK_Licence_GBALocation_Corporation REFERENCES dbo.Master_Corporation(corporationID),
    gbaWardID          INT         NOT NULL CONSTRAINT FK_Licence_GBALocation_GBAWard REFERENCES dbo.Master_GBAWard(gbaWardID),
    source             VARCHAR(16) NOT NULL,       -- NEW_APPLICATION / RENEWAL / ADMIN
    updatedBy          INT         NULL,
    updatedDate        DATETIME    NOT NULL CONSTRAINT DF_Licence_GBALocation_UpdatedDate DEFAULT (GETDATE())
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Licence_GBALocation_Temp' AND object_id = OBJECT_ID('dbo.Licence_GBALocation'))
    CREATE UNIQUE INDEX UX_Licence_GBALocation_Temp ON dbo.Licence_GBALocation (tempTradeLicenceID) WHERE tempTradeLicenceID IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Licence_GBALocation_Licence' AND object_id = OBJECT_ID('dbo.Licence_GBALocation'))
    CREATE UNIQUE INDEX UX_Licence_GBALocation_Licence ON dbo.Licence_GBALocation (tradeLicenceID) WHERE tradeLicenceID IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Licence_GBALocation_Corporation' AND object_id = OBJECT_ID('dbo.Licence_GBALocation'))
    CREATE INDEX IX_Licence_GBALocation_Corporation ON dbo.Licence_GBALocation (corporationID, gbaWardID);

/* ---------- 6. Seed corporations ---------- */
INSERT INTO dbo.Master_Corporation (corporationID, corporationName, corporationNativeName)
SELECT v.id, v.name, v.nativeName
FROM (VALUES
    ${corps.map(c => `(${int(c.corporationId)}, ${s(c.corporation)}, ${n(c.corporation_kn)})`).join(',\n    ')}
) v(id, name, nativeName)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Master_Corporation c WHERE c.corporationID = v.id);

/* ---------- 7. Seed zones ---------- */
INSERT INTO dbo.Master_GBAZone (corporationID, zoneName)
SELECT v.corporationID, v.zoneName
FROM (VALUES
    ${zones.map(z => `(${int(z.corporationId)}, ${s(z.zone_name)})`).join(',\n    ')}
) v(corporationID, zoneName)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Master_GBAZone z WHERE z.corporationID = v.corporationID AND z.zoneName = v.zoneName);

/* ---------- 8. Seed 369 wards (legacy ward / MOH looked up by code) ---------- */
-- Temp tables live in tempdb (server collation); COLLATE DATABASE_DEFAULT avoids
-- "Cannot resolve the collation conflict" when the DB uses a different collation.
CREATE TABLE #gbaWard (
    corporationID INT, zoneName VARCHAR(64) COLLATE DATABASE_DEFAULT, wardNo INT,
    wardName VARCHAR(100) COLLATE DATABASE_DEFAULT, wardNativeName NVARCHAR(200) COLLATE DATABASE_DEFAULT,
    assemblyNo INT, assemblyName VARCHAR(64) COLLATE DATABASE_DEFAULT, assemblyNativeName NVARCHAR(128) COLLATE DATABASE_DEFAULT,
    legacyWardCode INT, mohID INT, matchMethod VARCHAR(20) COLLATE DATABASE_DEFAULT
);
${insertChunks('#gbaWard', 'corporationID, zoneName, wardNo, wardName, wardNativeName, assemblyNo, assemblyName, assemblyNativeName, legacyWardCode, mohID, matchMethod', wardRows)}

INSERT INTO dbo.Master_GBAWard (corporationID, gbaZoneID, wardNo, wardName, wardNativeName, assemblyNo, assemblyName,
                                assemblyNativeName, constituencyID, legacyWardID, mohID, matchMethod)
SELECT t.corporationID, z.gbaZoneID, t.wardNo, t.wardName, t.wardNativeName, t.assemblyNo, t.assemblyName,
       t.assemblyNativeName, ac.constituencyID, lw.wardID, m.mohID, t.matchMethod
FROM #gbaWard t
JOIN dbo.Master_GBAZone z ON z.corporationID = t.corporationID AND z.zoneName = t.zoneName
OUTER APPLY (SELECT TOP 1 constituencyID FROM dbo.Master_AssemblyConstituency WHERE constituencyCode = t.assemblyNo ORDER BY constituencyID) ac
LEFT JOIN dbo.Master_BBMPWard lw ON lw.wardCode = t.legacyWardCode
LEFT JOIN dbo.Master_MOH m ON m.mohID = t.mohID   -- mohID (not mohCode): routing SPs join Licence_Master.mohID = moh.mohcd
WHERE NOT EXISTS (SELECT 1 FROM dbo.Master_GBAWard w WHERE w.corporationID = t.corporationID AND w.wardNo = t.wardNo);

/* ---------- 9. Seed legacy -> GBA mapping (draft) ---------- */
CREATE TABLE #map (legacyWardCode INT, corporationID INT, wardNo INT, isPrimary BIT, matchMethod VARCHAR(20) COLLATE DATABASE_DEFAULT, matchScore DECIMAL(4,2));
${insertChunks('#map', 'legacyWardCode, corporationID, wardNo, isPrimary, matchMethod, matchScore', mapRows)}

INSERT INTO dbo.Master_LegacyGBAWardMapping (legacyWardID, gbaWardID, isPrimary, matchMethod, matchScore)
SELECT lw.wardID, gw.gbaWardID, t.isPrimary, t.matchMethod, t.matchScore
FROM #map t
JOIN dbo.Master_BBMPWard lw ON lw.wardCode = t.legacyWardCode
JOIN dbo.Master_GBAWard gw ON gw.corporationID = t.corporationID AND gw.wardNo = t.wardNo
WHERE NOT EXISTS (SELECT 1 FROM dbo.Master_LegacyGBAWardMapping x WHERE x.legacyWardID = lw.wardID AND x.gbaWardID = gw.gbaWardID);

DROP TABLE #gbaWard;
DROP TABLE #map;

COMMIT TRANSACTION;
GO

/* If step 1-9 failed, everything above was rolled back: stop here with one clear message. */
IF OBJECT_ID('dbo.Master_GBAWard', 'U') IS NULL OR OBJECT_ID('dbo.Licence_GBALocation', 'U') IS NULL
BEGIN
    RAISERROR('GBA migration failed in steps 1-9 and was rolled back - nothing was created. Fix the first error above and run the script again.', 16, 1);
    SET NOEXEC ON;
END
GO

/* ---------- 10. Corporation of every licence (confirmed, else derived from legacy ward) ---------- */
IF OBJECT_ID('dbo.vw_LicenceCorporation', 'V') IS NOT NULL
    DROP VIEW dbo.vw_LicenceCorporation;
GO
CREATE VIEW dbo.vw_LicenceCorporation
AS
SELECT
    lm.tradeLicenceID,
    lm.wardID                                      AS legacyWardID,
    COALESCE(loc.corporationID, pgw.corporationID) AS corporationID,
    loc.gbaWardID                                  AS gbaWardID,          -- NULL until confirmed
    pgw.gbaWardID                                  AS suggestedGbaWardID,
    CASE WHEN loc.tradeLicenceID IS NOT NULL THEN 'CONFIRMED'
         WHEN pgw.gbaWardID IS NOT NULL      THEN 'DERIVED'
         ELSE 'UNMAPPED' END                       AS locationStatus
FROM dbo.Licence_Master lm
LEFT JOIN dbo.Licence_GBALocation loc ON loc.tradeLicenceID = lm.tradeLicenceID
OUTER APPLY (
    SELECT TOP 1 gw.gbaWardID, gw.corporationID
    FROM dbo.Master_LegacyGBAWardMapping mp
    JOIN dbo.Master_GBAWard gw ON gw.gbaWardID = mp.gbaWardID
    WHERE mp.legacyWardID = lm.wardID
    ORDER BY mp.isPrimary DESC, mp.matchScore DESC
) pgw;
GO

/* ---------- 11. Verification (read-only) ---------- */
SELECT 'Master_Corporation' AS tbl, COUNT(*) AS rows_ FROM dbo.Master_Corporation            -- expect 5
UNION ALL SELECT 'Master_GBAZone', COUNT(*) FROM dbo.Master_GBAZone                         -- expect 10
UNION ALL SELECT 'Master_GBAWard', COUNT(*) FROM dbo.Master_GBAWard                         -- expect 369
UNION ALL SELECT 'Master_GBAWard without legacy ward/MOH', COUNT(*) FROM dbo.Master_GBAWard WHERE legacyWardID IS NULL OR mohID IS NULL
UNION ALL SELECT 'Master_LegacyGBAWardMapping', COUNT(*) FROM dbo.Master_LegacyGBAWardMapping -- expect ${map.length}
UNION ALL SELECT 'Legacy wards (1-198) with no mapping', COUNT(*) FROM dbo.Master_BBMPWard w
          WHERE w.wardCode BETWEEN 1 AND 198 AND NOT EXISTS (SELECT 1 FROM dbo.Master_LegacyGBAWardMapping m WHERE m.legacyWardID = w.wardID);

SELECT c.corporationName, COUNT(DISTINCT z.gbaZoneID) AS zones, COUNT(w.gbaWardID) AS wards
FROM dbo.Master_Corporation c
LEFT JOIN dbo.Master_GBAZone z ON z.corporationID = c.corporationID
LEFT JOIN dbo.Master_GBAWard w ON w.gbaZoneID = z.gbaZoneID
GROUP BY c.corporationID, c.corporationName
ORDER BY c.corporationID;   -- East 50, South 72, West 112, North 72, Central 63
GO
SET NOEXEC OFF;
GO
`;

const rollback = `/* =====================================================================
   ROLLBACK for GBA_Corporation_Ward_Migration.sql
   Drops ONLY the objects that migration created. Existing tables are
   not touched.

   WARNING: Licence_GBALocation holds the corporation/ward confirmed by
   applicants after go-live. This script refuses to run if it has rows;
   back it up first if you really need to roll back after go-live.
   ===================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID('dbo.Licence_GBALocation', 'U') IS NOT NULL
   AND EXISTS (SELECT 1 FROM dbo.Licence_GBALocation)
BEGIN
    RAISERROR('Licence_GBALocation contains data - back it up and empty it before rolling back.', 16, 1);
    RETURN;
END

BEGIN TRANSACTION;
IF OBJECT_ID('dbo.vw_LicenceCorporation', 'V') IS NOT NULL      DROP VIEW  dbo.vw_LicenceCorporation;
IF OBJECT_ID('dbo.Licence_GBALocation', 'U') IS NOT NULL        DROP TABLE dbo.Licence_GBALocation;
IF OBJECT_ID('dbo.Master_LegacyGBAWardMapping', 'U') IS NOT NULL DROP TABLE dbo.Master_LegacyGBAWardMapping;
IF OBJECT_ID('dbo.Master_GBAWard', 'U') IS NOT NULL             DROP TABLE dbo.Master_GBAWard;
IF OBJECT_ID('dbo.Master_GBAZone', 'U') IS NOT NULL             DROP TABLE dbo.Master_GBAZone;
IF OBJECT_ID('dbo.Master_Corporation', 'U') IS NOT NULL         DROP TABLE dbo.Master_Corporation;
COMMIT TRANSACTION;
`;

fs.mkdirSync(path.join(outDir, 'seed'), { recursive: true });
// UTF-8 with BOM so SSMS keeps the Kannada N'' literals intact
fs.writeFileSync(path.join(outDir, 'GBA_Corporation_Ward_Migration.sql'), '﻿' + migration.replace(/\n/g, '\r\n'), 'utf8');
fs.writeFileSync(path.join(outDir, 'GBA_Corporation_Ward_Rollback.sql'), '﻿' + rollback.replace(/\n/g, '\r\n'), 'utf8');
for (const f of ['ward_369_master_utf8.csv', 'gba_ward_master.csv', 'legacy_to_gba_ward_mapping.csv'])
  fs.copyFileSync(path.join(dir, f), path.join(outDir, 'seed', f));
console.log('wrote migration (', wardRows.length, 'wards,', mapRows.length, 'mapping rows ), rollback, seed CSVs');
