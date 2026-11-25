# Production deployment runbook (Windows Server + IIS)

Audience: the person deploying to the GBA production server. No secret values in this file.
Status: **not yet deployable** - see "Blocking items" at the end.

## 1. Server prerequisites
- Windows Server with IIS, **ASP.NET Core 8 Hosting Bundle** (the API is framework-dependent:
  `web.config` starts `dotnet .\Gba.TradeLicense.Api.dll`, `hostingModel="inprocess"`). Restart IIS
  (`iisreset`) after installing the bundle.
- **URL Rewrite** module for the Angular site (deep links such as `/gba/trader/...`).
- WebDAV must not intercept PUT/DELETE: the API's `web.config` removes `WebDAVModule` (keep it).
- HTTPS certificate bound to the site; the API sends HSTS and redirects HTTP to HTTPS.

## 2. Layout (as used today)
| IIS item | Path | Content |
|---|---|---|
| Site (HTTPS) | e.g. `https://gbatrade.karnataka.gov.in` | |
| Application `/api` | API folder | output of `dotnet publish -c Release` |
| Application or virtual dir `/gba` | SPA folder | `npm run build:deploy` output (`dist/my-trade-licenses-app/browser`) |

**Frontend build: use `npm run build:deploy` only.** The plain production build (`ng build`) writes
`<base href="/">` and no `index.html` (SSR output mode), so it does not work under `/gba`. The
`deploy` configuration sets `/gba/` and the script copies `index.csr.html` to `index.html`
(verified 2026-11-25: `<base href="/gba/">`, `index.html` present). The Node SSR server
(`server.mjs`) is not used on IIS.
- `public/web.config` (SPA): its 404 `<error ... path="index.html">` uses a relative path; check on the
  server (IIS usually needs `/gba/index.html`). Add `Cache-Control: no-cache` for `index.html` so a new
  release is picked up.
- Google Maps browser key (in the bundle by design): restrict it in Google Cloud to the production
  HTTP referrer and the Maps APIs used.
- CAPTCHA is disabled in the build (`captchaToken: 'test'`) and the API still accepts `'test'`;
  enabling CAPTCHA needs a production site key in the frontend and removal of the `'test'` bypass
  in `AuthController` (owner decision - it changes the login flow).

The SPA calls `/api/api/...` (IIS app `/api` + controller route `api/...`). The Easebuzz return URL
is built from the request (`/api/api/payment/payment-success`) unless `Easebuzz:SuccessUrl` is set
on the same host.

## 3. Application pool (API)
- .NET CLR version: **No Managed Code**; pipeline: Integrated; one worker process (in-memory OTP
  limits, one-time login tokens and the generated payment-redirect key are per process - a web
  garden or several servers need shared configuration of `PaymentRedirect:*` and will apply OTP
  limits per server).
- Identity: a dedicated app-pool identity. It needs **Modify** on:
  - `<api>\Uploads\LicenceDocuments\` (uploaded application documents)
  - `<api>\wwwroot\uploads\licence-certificates\` (saved licence certificates)
  - `<api>\logs\` if stdout logging is enabled
  and **Read** on the rest of the API folder. No access to other sites.
- These two upload folders hold customer data: they are **not** in the published package (excluded
  in `Gba.TradeLicense.Api.csproj`) and must be backed up with the server, never copied into source
  control. Deploy with "delete existing files" **off** so they are not wiped.

## 4. Configuration (environment, never files in source control)
See `SECRETS_AND_DEPLOYMENT.md` for the full list. Minimum: `ConnectionStrings__Default`,
`Jwt__Key` (>= 32 chars), `Jwt__Issuer`, `Jwt__Audience`, `Sms__Password`, `Sms__SecureKey`,
`ReCaptcha__SecretKey`, `PaymentRedirect__PublicKeyPath` / `PrivateKeyPath`.
`ASPNETCORE_ENVIRONMENT` must be `Production` (or unset); `ReCaptcha:BypassValidation` must not be true.
The API refuses to start without the connection string and JWT settings.

Database connection: the old string used `Encrypt=False` to a public IP. Use `Encrypt=True` with a
trusted server certificate (or a private network / VPN) and a least-privilege SQL login that can only
EXECUTE the application procedures and access the application tables.

## 5. Logging and errors
- The API logs to the console only. Under IIS that output is lost unless stdout logging is enabled
  (`stdoutLogEnabled="true"`, `stdoutLogFile=".\logs\stdout"`, folder writable, rotate/clean it) or a
  file / event-log provider is added (requires approval - code change).
- Unhandled exceptions in Production return a plain 500 (no details). The `/error` path configured
  in `Program.cs` has no endpoint; .NET 8 then rethrows the original exception, so the response is 500.

## 6. Health check
There is no health-check endpoint. Monitoring can use `GET /api/api/gba/corporations` and expect
**401** (app up, auth working) until a dedicated endpoint is approved and added.

## 7. Release artifacts
Build from the committed, reviewed source - not from the old `Deploy/` folders (removed):
- API: `dotnet publish src/Gba.TradeLicense.Api/Gba.TradeLicense.Api.csproj -c Release -o <out>`
- SPA: `npm ci` then `npm run build:deploy` in `my-trade-licenses-app`
Latest local build (from the uncommitted working tree, for verification only):
`D:\GBA-release\20261125-phase5-v3` (scanned: none of the 15 historical secret values, no keys,
no customer files; SPA base href `/gba/`). The earlier `20261124-*` folders are superseded.

## 8. Order on deployment day
1. Credentials rotated (INCIDENT_RESPONSE.md section 3).
2. Database: run `Database/Staging_Validation_ReadOnly.sql`; run `Database/GBA_PowerHP_Migration.sql`
   (approved, additive) - on staging first.
3. Set environment variables on the server; back up the current API and SPA folders and the two
   upload folders.
4. Copy the new API and SPA (do not delete existing files), recycle the app pool, check the start-up
   log line `Payment redirect keys:` and that the site loads.
5. Smoke test (staging first): trader OTP login, draft save, payment on the Easebuzz test merchant,
   officer queue, approval, certificate download.

## Blocking items
- Credentials not yet rotated; repositories public; customer documents in Git history.
- No staging validation of database compatibility or end-to-end workflows yet.
- Logging to a persistent destination not configured.
