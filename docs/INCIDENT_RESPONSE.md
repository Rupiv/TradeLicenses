# Incident response: credentials exposed in public GitHub repositories

Audience: repository owner, database administrator, payment / SMS account owners.
Status when written (2026-11-24): exposure confirmed, **no credential rotated yet**. This file never
contains secret values; refer to them by name only.

## 0. Facts

- `github.com/Rupiv/TradeLicenses` and `github.com/Rupiv/my-trade-licenses-app` were **public**
  (unauthenticated GitHub API answered HTTP 200 on 2026-11-24).
- Exposed in TradeLicenses history since commit 248cf2e (2025-12-13): see table in section 2.
- my-trade-licenses-app contains no secrets (only the public reCAPTCHA site key).
- Anyone may have copied them; treat every listed credential as compromised.

## 1. Make the repositories private (owner, 5 minutes)

1. GitHub > repository > **Settings** > **General** > *Danger Zone* > **Change repository visibility** >
   *Make private*. Repeat for the second repository.
2. Verify from a machine that is **not** logged in to GitHub:
   `curl -s -o NUL -w "%{http_code}" https://api.github.com/repos/Rupiv/TradeLicenses`
   (and the same for `my-trade-licenses-app`). Expect **404**. 200 means still public.
3. Settings > **Collaborators and teams**: remove anyone who no longer needs access; note who has access.
4. Repository page > **Forks** (Insights > Forks): list public forks. Forks of a public repository do
   not become private; ask their owners to delete them, or contact GitHub Support.
5. Insights > **Traffic** (last 14 days of clones / visitors) and Security > **Secret scanning alerts**:
   save screenshots for the incident record.

Making the repositories private stops new exposure only; it does not un-expose anything.

## 2. Credentials and affected services

| Credential (setting) | Service | Impact of misuse | Rotation by | After rotation |
|---|---|---|---|---|
| `ConnectionStrings:Default` (SQL login + password, 4 historical versions) | SQL Server (all application data) | read / change all data, incl. `Payment_Gateway_Config` | DBA | update API env var; old login disabled |
| Easebuzz merchant key + salt (in `Payment_Gateway_Config`, one per corporation) | Easebuzz | forge payment-response hashes, call refund / payout APIs | corporation finance + Easebuzz | update the table rows; payments in flight fail and are retried |
| `Jwt:Key` (2 historical versions) | API sessions | forge an admin token = full access to every API | owner | all users log in again |
| `Sms:Password`, `Sms:SecureKey` | CDAC MSDG SMS gateway | send SMS under the corporation's sender id, use credits | SMS account owner | update env vars |
| `ReCaptcha:SecretKey` | Google reCAPTCHA | low (CAPTCHA currently bypassed with `'test'`) | owner (Google admin console) | new key pair; site key in frontend |
| `Otp:Secret` | OTP hashing (`usp_SendOtp` path) | low | owner | pending OTPs invalid |
| RSA key pair (`Security/Keys/*.pem`) | payment-success redirect payload | read / forge the redirect payload (the payment itself is still verified by hash + status API) | owner | redirects in flight fail |

## 3. Rotation order (coordinate; one maintenance window)

1. **Database password** first (it also protects the Easebuzz keys): create the new password, update
   the API server's `ConnectionStrings__Default`, restart, confirm the site works, then disable the old
   password / login.
2. **Easebuzz** key + salt per corporation: regenerate in the Easebuzz dashboard, update
   `Payment_Gateway_Config`, run one test payment per corporation.
3. **JWT key**: set a new random value (min. 32 characters) in `Jwt__Key`, restart. Everyone re-logs in.
4. **SMS** password / secure key in the CDAC portal; update env vars; send one OTP to verify.
5. **reCAPTCHA** new key pair (and enable CAPTCHA properly: frontend `captchaToken: 'test'` must go).
6. **OTP secret**, **RSA key pair** (`PaymentRedirect__PrivateKeyPath` / `PublicKeyPath`, outside the web root).
7. Record date / time / who for each item. Only then mark a credential "rotated".

## 4. Review activity logs (read only)

Look for activity between 2025-12-13 and the rotation time that was not made by the team.

### 4.1 SQL Server (DBA)
```sql
-- Successful / failed logins recorded in the error log (needs securityadmin). Look for unknown client IPs.
EXEC sys.xp_readerrorlog 0, 1, N'Login succeeded';
EXEC sys.xp_readerrorlog 0, 1, N'Login failed';
-- Older logs: replace 0 with 1, 2, ... (one per restart / cycle)

-- Current and recent sessions of the application login (replace <app login name>)
SELECT session_id, login_name, host_name, program_name, client_interface_name, login_time
FROM sys.dm_exec_sessions WHERE login_name = N'<app login name>' ORDER BY login_time DESC;
SELECT c.client_net_address, COUNT(*) AS connections
FROM sys.dm_exec_connections c JOIN sys.dm_exec_sessions s ON s.session_id = c.session_id
WHERE s.login_name = N'<app login name>' GROUP BY c.client_net_address;

-- When sensitive tables were last changed (since the last SQL Server restart only)
SELECT OBJECT_NAME(s.object_id) AS table_name, MAX(s.last_user_update) AS last_update
FROM sys.dm_db_index_usage_stats s
WHERE s.database_id = DB_ID()
  AND OBJECT_NAME(s.object_id) IN ('Payment_Gateway_Config', 'Login_Master', 'Login_GBAJurisdiction',
                                   'Trade_LicenceFee', 'Payment_Audit_Log')
GROUP BY s.object_id;

-- If SQL Server Audit or a server-side trace exists, export it for the period; the default trace
-- keeps only a few days: SELECT * FROM sys.fn_trace_gettable(
--   (SELECT path FROM sys.traces WHERE is_default = 1), DEFAULT) WHERE EventClass IN (14, 20, 102, 103, 104, 108, 109, 110, 111);
```
Also run `Database/Staging_Validation_ReadOnly.sql` section 4 on **production** (read only): payments
whose amount differs from the initiated amount, transactions used by more than one application,
SUCCESS rows without an INITIATED row. Every hit needs an explanation.

Staff accounts created outside the normal process: list the most recently created logins
(`SELECT TOP 50 * FROM dbo.Login_Master ORDER BY loginID DESC` - loginID is the identity) and confirm
each with the administrators.

### 4.2 Easebuzz dashboard (per corporation)
- Transactions, **refunds** and **payouts** for the period; any refund not requested by the team.
- Settlement / bank account details and webhook URL: unchanged?
- API key usage / login history of the merchant account, if offered.

### 4.3 API / IIS logs
- `POST /api/auth/login-user` volume and source IPs (account take-over before the OTP fix).
- `POST /api/login-master`, `PUT /api/gba/user-jurisdiction/*`, `POST/PUT /api/trade-licence-fees*`,
  `POST /api/master/licence-process/submit-action`, `POST /api/payment/refund` - by unexpected IPs or at odd
  hours (these were reachable with any trader token before the access fixes).
- Requests carrying tokens that are valid but do not correspond to any login in the log (forged with the
  exposed `Jwt:Key`) can only be found by correlating with the login history.

### 4.4 SMS gateway
- Sent-message report and credit balance for unexplained bulk sends.

## 5. Close-out
- History cleanup (`HISTORY_CLEANUP_PLAN.md`) after section 3 is complete.
- Keep this file, the screenshots and the log exports with the incident record.
