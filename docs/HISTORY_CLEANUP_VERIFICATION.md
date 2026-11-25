# History cleanup verification (dry run) - TradeLicenses

Audience: repository owner deciding on the history rewrite. No secret values in this file.
**Status: dry run only. Nothing rewritten on GitHub, nothing force-pushed.**

## Run of 2026-11-25
| Item | Result |
|---|---|
| Source | local clone, all branches (`Dev_Rupendra` incl. commit d760256, `master`, `Dev_Shlok`) |
| Mirror | `D:\GBA-secure-backup\rewrite\TradeLicenses-rewritten.git` (no remote configured) |
| Commits scanned for secrets | 75 |
| Secret values collected from history | 14 distinct (connection strings / DB passwords, JWT keys, OTP secret, reCAPTCHA secret, SMS password, SMS secure key) + 1 RSA private key |
| Rewrite | values replaced by `***REMOVED***`; `*.pem`, `*Uploads/LicenceDocuments/*`, `*wwwroot/uploads/*` removed from every commit |
| Objects scanned after rewrite | 1,301 |
| Secret values found | **0** |
| Private keys found | **0** (one textual match of the phrase "PRIVATE KEY-----" in `docs/HISTORY_CLEANUP_PLAN.md`, the sentence describing this scan - not key material) |
| `.pem` paths in history | **0** |
| Customer documents / certificates in history | **0** (63 distinct files before) |
| Branches after rewrite | Dev_Rupendra 75, Dev_Shlok 68, master 68 commits |
| Backups (untouched) | `D:\GBA-secure-backup\TradeLicenses-20261124-2034.bundle`, `...my-trade-licenses-app-20261124-2034.bundle` (verified) |

my-trade-licenses-app: no secrets, keys or customer files in its history - **no rewrite needed**.

## Preconditions for the real run (none confirmed yet)
| Precondition | Status 2026-11-25 |
|---|---|
| Both repositories private | **NOT done** - both answered the public GitHub API with HTTP 200 |
| Database credentials rotated | not confirmed |
| Easebuzz merchant key / salt rotated (each corporation) | not confirmed |
| JWT signing key rotated | not confirmed |
| SMS password / secure key rotated | not confirmed |
| reCAPTCHA key pair replaced | not confirmed |
| OTP secret rotated | not confirmed |
| RSA payment-redirect key pair replaced | not confirmed |
| Latest work committed and pushed (so the rewrite includes it) | Dev_Rupendra has unpushed / uncommitted work |
| Developers informed (re-clone afterwards) | not confirmed |

## Real run
Follow `HISTORY_CLEANUP_PLAN.md` section 3.2 (fresh mirror of GitHub, same filter, scan, then
`git push --force --mirror` **only with the owner's explicit approval**). Afterwards ask GitHub Support
to purge cached views / pull-request refs, and check forks.
