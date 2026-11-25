# Production deployment runbook (Windows Server + IIS)

Audience: the person deploying the GBA Trade Licence system to the production server.
No secret values appear in this file. Status 2026-11-25: **NO-GO** - see section 0.

---------------------------------------------------------------------------------------------
## Part A - PREREQUISITES (must all be true before deployment day)
---------------------------------------------------------------------------------------------

### 0. Go / no-go gates
| Gate | Owner | Status |
|---|---|---|
| Both GitHub repositories private | owner | open |
| All exposed credentials rotated (`INCIDENT_RESPONSE.md` s.3) | owner / DBA / finance | open |
| Data-protection decision taken (`DATA_PROTECTION_INCIDENT.md`) | owner | open |
| Staging: read-only validation + both migrations + 19 workflows passed | DBA + developer | open |
| reCAPTCHA site key registered for the production domain; secret set on the server | owner | open |
| Release built from committed, reviewed source; checksums verified | developer | release v4 built from the working tree (not yet committed) |

### 1. Server
- Windows Server with IIS; **ASP.NET Core 8 Hosting Bundle** (the API is framework-dependent,
  `hostingModel="inprocess"`); `iisreset` after installing it.
- IIS **URL Rewrite** module (Angular deep links).
- TLS certificate for the site; HTTP binding only to redirect.
- Outbound HTTPS from the server to: Easebuzz (`pay.easebuzz.in`, `dashboard.easebuzz.in`,
  test hosts for staging), Google reCAPTCHA (`www.google.com`), the SMS gateway, KGIS.

### 2. IIS layout
| IIS item | Physical folder (example) | Content |
|---|---|---|
| Site (HTTPS) `gbatrade.karnataka.gov.in` | `D:\inetpub\gba-site` | |
| Application `/api` | `D:\inetpub\gba-site\api` | API release (`api\`) |
| Application / virtual dir `/gba` | `D:\inetpub\gba-site\gba` | SPA release (`frontend\browser\`) |

The SPA calls `/api/api/...` (IIS app `/api` + controller route `api/...`).

### 3. Application pool (API)
- No Managed Code; Integrated pipeline; **one worker process** (OTP limits, one-time login tokens and an
  in-memory payment-redirect key are per process).
- Dedicated identity (`IIS AppPool\GbaApi` or a service account). Permissions:
  | Folder | Right |
  |---|---|
  | `<api>\` | Read & execute |
  | `<api>\Uploads\LicenceDocuments\` | Modify (uploaded application documents) |
  | `<api>\wwwroot\uploads\licence-certificates\` | Modify (saved licence certificates) |
  | `<api>\logs\` (or `Logging__File__Path` folder) | Modify |
  | payment-redirect key files (outside the web root) | Read |
- The two upload folders hold **customer data**: they are not in the release package and must never
  be deleted or overwritten by a deployment; back them up with the server.

### 4. Environment variables (secrets only here - never in files in source control)
Set on the app pool / machine, or in the server's own `web.config` `<environmentVariables>` (that file
then stays on the server). Full list: `SECRETS_AND_DEPLOYMENT.md`.
| Variable | Required | Note |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | yes | `Production` |
| `ConnectionStrings__Default` | yes | rotated least-privilege SQL login; `Encrypt=True` (trusted cert) or private network |
| `Jwt__Key` (>= 32 chars), `Jwt__Issuer`, `Jwt__Audience` | yes | API will not start without them |
| `ReCaptcha__SecretKey` | yes | without it every login fails (fails closed) |
| `ReCaptcha__AllowedHostnames` | recommended | e.g. `gbatrade.karnataka.gov.in` |
| `Sms__Password`, `Sms__SecureKey` | yes | |
| `Otp__Secret` | recommended | |
| `PaymentRedirect__PublicKeyPath`, `PaymentRedirect__PrivateKeyPath` | recommended | new key pair, outside the web root |
| `Logging__File__Path` | optional | default `<api>\logs\api-.json` |
| `Logging__File__RetainedDays` | optional | default 30 |
| `Swagger__Enabled` | **must not be set** in production | |
| `ReCaptcha__BypassValidation` | **must not be set** | ignored outside Development anyway |

### 5. Database (DBA, staging first)
1. `Database/Retrieve_Definitions_ReadOnly.sql` - export procedure definitions (read only).
2. `Database/Staging_Validation_ReadOnly.sql` - every row `ok`.
3. Approved additive migrations, **staging first**, then production in the maintenance window:
   - `Database/GBA_PowerHP_Migration.sql` (approved) - declared HP for the exact fee check.
   - `Database/GBA_LicenceOwner_Migration.sql` (**needs approval**) - owner of draft licence records (D6).
   Rollbacks: `GBA_PowerHP_Rollback.sql`, `GBA_LicenceOwner_Rollback.sql`.
4. Least-privilege API login: EXECUTE on the application procedures; SELECT/INSERT/UPDATE on the
   tables the API uses inline (see the staging validation script); SELECT/INSERT on the two new tables.
5. Do **not** run `my-trade-licenses-app/SqlScript/*.sql` (guarded; target the wrong certificate table).

### 6. Logging, health, monitoring
- Logs: JSON lines (Serilog compact format), one file per day, 30 days, 50 MB per file, in
  `Logging__File__Path`. Each line carries `CorrelationId` (also returned as `X-Correlation-ID`).
  Not logged: request bodies, headers, query strings, tokens, passwords, OTPs (masked), connection
  strings, keys. Access: administrators only (file ACL); retention per the owner's policy.
- Health: `GET /api/health/live` -> `Healthy` (process up); `GET /api/health/ready` -> `Healthy` /
  `Unhealthy` (database reachable). Plain text, no details. Point the monitor at `/ready`.
- Swagger is off in Production.
- Unhandled errors: HTTP 500 `{ message, correlationId }`; the exception is in the log under that id.

---------------------------------------------------------------------------------------------
## Part B - DEPLOYMENT ACTIONS (maintenance window)
---------------------------------------------------------------------------------------------

### 7. Before
1. Verify the release checksums: `Get-FileHash` each file against `RELEASE-MANIFEST.json`.
2. Back up: current `<site>\api` and `<site>\gba` folders (excluding nothing), the two upload folders,
   the server `web.config` and the database (full backup by the DBA).
3. Announce the window; stop the app pool `GbaApi` (offline page optional: `app_offline.htm`).

### 8. Backend
1. Copy the release `api\` folder over `<site>\api` **without deleting existing files** (robocopy
   without `/MIR` / `/PURGE`); never touch `Uploads\` and `wwwroot\uploads\`.
   `robocopy <release>\api <site>\api /E /XD Uploads uploads logs /XF web.config`
2. Keep the server `web.config` (it holds the environment variables); compare it with the release
   `web.config` and merge only the `<aspNetCore>` / handler lines if they changed.
3. Run the approved SQL migrations (section 5) if not already done.
4. Start the app pool; check the log for `Payment redirect keys: configuration (key files)` and no errors;
   `GET /api/health/ready` = `Healthy`.

### 9. Frontend
1. Copy the release `frontend\browser\*` to `<site>\gba` (same: no purge).
2. `index.html` must contain `<base href="/gba/">`; keep the SPA `web.config` (rewrite rules).
   Check that its 404 error path is absolute (`/gba/index.html`).
3. Add `Cache-Control: no-cache` for `index.html` (IIS output headers) so clients load the new build.
4. Google Maps browser key: restricted to the production referrer in Google Cloud.

### 10. Smoke tests (production, read-mostly; no test payments)
| # | Check | Expected |
|---|---|---|
| 1 | `/api/health/live`, `/api/health/ready` | `Healthy` |
| 2 | `/api/swagger/index.html` | 404 |
| 3 | `https://<site>/gba/` loads, no console errors | home page |
| 4 | Trader OTP login (real mobile of a tester) | CAPTCHA passes, dashboard opens |
| 5 | Officer login, queue loads | own queue only |
| 6 | Admin opens users, reports | allowed |
| 7 | Officer opens a report | message "available to administrators only" |
| 8 | Open an existing approved licence certificate | PDF downloads |
| 9 | Log file written, contains `CorrelationId`, no tokens | yes |
End-to-end payment checks belong to staging (Easebuzz test merchant), not production.

### 11. Rollback
1. Stop the app pool.
2. Restore the backed-up `<site>\api` and `<site>\gba` folders (upload folders were not touched).
3. If a migration was run and must be undone: the matching `*_Rollback.sql` (DBA). The new tables are
   additive; leaving them in place is safe for the previous build.
4. Start the app pool; health checks; record the reason.
Note: the previous build still contains the closed security holes and the `'test'` CAPTCHA bypass;
roll back only as a short-term measure.

### 12. After
- Keep the release folder and manifest; record version, date, operator, checks done.
- Watch the log for 401/403 spikes (expected after the stricter rules) and 5xx with correlation ids.
