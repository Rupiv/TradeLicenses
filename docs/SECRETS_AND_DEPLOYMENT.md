# Secrets and deployment (GBA Trade Licence API)

Audience: whoever deploys or operates the API. No secret values appear in this file.

## What changed

`src/Gba.TradeLicense.Api/appsettings.json` no longer holds secrets. The values below are empty
in source control and must be supplied by the environment. The API refuses to start (and names the
missing setting) when `ConnectionStrings:Default`, `Jwt:Key` (min. 32 characters), `Jwt:Issuer` or
`Jwt:Audience` is missing.

| Setting | Environment variable | Used by |
|---|---|---|
| `ConnectionStrings:Default` | `ConnectionStrings__Default` | every controller / service (SQL Server) |
| `Jwt:Key` | `Jwt__Key` | `Program.cs` (token validation), `JwtTokenService` (signing) |
| `Otp:Secret` | `Otp__Secret` | `AuthService` OTP hashing (falls back to `Jwt:Key`) |
| `ReCaptcha:SecretKey` | `ReCaptcha__SecretKey` | `AuthController.IsCaptchaValid` |
| `Sms:Password`, `Sms:SecureKey` | `Sms__Password`, `Sms__SecureKey` | `SmsService` / `SMSHttpPostClient` (CDAC MSDG gateway) |
| `PaymentRedirect:PrivateKeyPath` + `PublicKeyPath` (or `...Pem`) | `PaymentRedirect__PrivateKeyPath`, ... | `CryptoHelper` (payment-success redirect payload) |

### Complete list of settings the code reads (checked against the source, 2026-11-24)

| Setting | Secret? | Required | Notes |
|---|---|---|---|
| `ConnectionStrings:Default` | yes | yes (start-up check) | |
| `Jwt:Key` | yes | yes, min. 32 chars (start-up check) | |
| `Jwt:Issuer`, `Jwt:Audience` | no | yes (start-up check) | in `appsettings.json` |
| `Jwt:AccessTokenMinutes` | no | no | session length; in `appsettings.json` |
| `Otp:Secret` | yes | recommended | falls back to `Jwt:Key` |
| `ReCaptcha:SecretKey` | yes | when CAPTCHA is enabled | |
| `ReCaptcha:BypassValidation` | no | **must be unset/false in production** | dev only |
| `Sms:Username`, `Sms:SenderId` | no | yes for SMS | in `appsettings.json` |
| `Sms:Password`, `Sms:SecureKey` | yes | yes for SMS | |
| `Sms:CorporationName` | no | no | text in OTP SMS (default "BBMP") |
| `Easebuzz:SuccessUrl`, `Easebuzz:FailureUrl`, `Easebuzz:FrontendBaseUrl` | no | no | used only when on the request's own domain, otherwise built from the request |
| `PaymentRedirect:PublicKeyPem` + `PrivateKeyPem`, or `...Path` | yes | no (see below) | |
| `KarnatakaSms:*` | yes | no | `KarnatakaSmsService` is registered but not called anywhere |

Easebuzz merchant key / salt are not in configuration: they are rows in `dbo.Payment_Gateway_Config`
(one per corporation) and are therefore protected only by the database credentials.

## Where to put the values

- **IIS server**: application-pool / machine environment variables, or `<environmentVariables>` in the
  server's own `web.config` (`<aspNetCore ...><environmentVariables><environmentVariable name="Jwt__Key" value="..." /></environmentVariables></aspNetCore>`).
  Keep that `web.config` on the server only; do not copy it back into the repository.
- **Local development**: `dotnet user-secrets set "Jwt:Key" "<value>" --project src/Gba.TradeLicense.Api`
  (and the same for the other settings). Use a development database, not production.
- Payment-redirect keys: if `PaymentRedirect:*` is not set the API uses `Security\Keys\*.pem` left by
  an earlier deployment, otherwise it generates a key pair in memory at start-up. The start-up log line
  `Payment redirect keys: <source>` says which (never the key itself). The in-memory option works for a
  single server; for several servers behind a load balancer configure the same key files on all of them.

## Before deploying this build

1. Set the environment values on the server **before** copying the new files (the published
   `appsettings.json` has empty secrets; an older server copy with values is overwritten by a publish).
2. Publish from source (`dotnet publish -c Release`), not from the `Deploy/` folder: every build under
   `Deploy/` and `artifacts/` predates the security fixes and contains the committed secrets.
3. Deploy the API and the Angular build together (trader login needs both: `otpToken`).
4. Run `Database/GBA_PowerHP_Migration.sql` (new table for declared power / generator HP; additive)
   on staging, then production. Until it runs, the API still works and checks payments against the
   minimum amount only.
5. Check the start-up log for `Payment redirect keys:` and run `Database/Staging_Validation_ReadOnly.sql`
   on staging.

## Rotation

All values that were ever committed must be treated as compromised and replaced (see the security
report for the order and the impact of each). Rotation is done by the system owners; this repository
only needs the new values to be supplied through the environment.
