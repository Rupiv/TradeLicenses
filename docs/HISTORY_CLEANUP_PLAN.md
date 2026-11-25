# Git history cleanup plan (TradeLicenses) - FOR REVIEW, NOT EXECUTED

Audience: repository owner and every developer with a clone. No secret values appear here.

## 1. Situation (checked 2026-11-24)

- **Visibility:** `github.com/Rupiv/TradeLicenses` and `github.com/Rupiv/my-trade-licenses-app` both
  answer the public GitHub API without authentication (HTTP 200), so both are **public**.
  Collaborators could not be listed: that needs an authenticated GitHub session (owner: Settings >
  Collaborators). Forks also need checking (repository page > Forks).
- **TradeLicenses:** 16 commits across all branches add or change secret-bearing files; the first is
  248cf2e (2025-12-13). 30 distinct paths are affected: `src/Gba.TradeLicense.Api/appsettings.json`,
  `src/Gba.TradeLicense.Infrastructure/Security/Keys/*.pem`, and the copies under `Deploy/**` and
  `artifacts/**`. The tips of `origin/master` and `origin/Dev_Shlok` still contain 4 of these files, and
  `origin/Dev_Rupendra` contains 29. No tags.
  Branches: `master`, `Dev_Rupendra`, `Dev_Shlok`.
- **my-trade-licenses-app:** no secret-bearing files anywhere in its history. **No rewrite needed.**
  (The reCAPTCHA *site* key in `environment.ts` is public by design.)
- **Exposed values:** the database connection string including its password (4 historical
  versions), the JWT signing key (2 versions), the OTP secret, the reCAPTCHA secret, the SMS password,
  the SMS secure key, and the RSA payment-redirect key pair. Via the database credentials, the
  Easebuzz merchant key and salt in `Payment_Gateway_Config` must also be treated as exposed.

Because the repositories are public, the history rewrite removes the secrets from GitHub going
forward, but anyone who already cloned or forked still has them. **Rotation is the real fix; the
rewrite is hygiene.**

## 2. Preconditions (all must be true before step 3)

1. All exposed credentials rotated, and the new values deployed through environment
   configuration (see `SECRETS_AND_DEPLOYMENT.md`). The old values no longer work anywhere.
2. The current fixes are committed and pushed on `Dev_Rupendra` (so the rewrite includes them).
3. Every developer has pushed their work and has been told the rewrite date (they must re-clone after).
4. Backups exist: full-history bundles in `D:\GBA-secure-backup` (created 2026-11-24 20:34, verified
   with `git bundle verify`, folder readable only by the owner account and SYSTEM). Make a fresh bundle
   immediately before the rewrite.
5. Optional but recommended: make the repository private while the rewrite is prepared.

## 3. Procedure

### 3.1 Dry run done locally (2026-11-24) - nothing pushed
Script: `D:\GBA-secure-backup\rewrite\rewrite_history.py` (outside the repository).
1. `git clone --mirror --no-local D:\GBA\TradeLicenses D:\GBA-secure-backup\rewrite\TradeLicenses-rewritten.git`
2. Collected every secret value ever present in a tracked `appsettings*.json` (74 commits, all refs):
   **14 distinct values** + **1 private key**, written to a replacement file in the secured folder
   (deleted after the run; the script recreates it from history).
3. `git filter-repo --force --replace-text <file> --invert-paths --path-glob '*.pem' --path-glob '*Uploads/LicenceDocuments/*' --path-glob '*wwwroot/uploads/*'`
   - secret values replaced by `***REMOVED***` in every commit (files keep their structure;
     `appsettings.Development.json` untouched);
   - `.pem` files removed from every commit;
   - **customer data removed from every commit** (re-run 2026-11-25): uploaded licence-application
     documents (`src/.../Uploads/LicenceDocuments/**`, PDFs and photos) and generated licence
     certificates (`**/wwwroot/uploads/licence-certificates/**`) - 63 distinct files in history.
4. Scan of all **1,209 objects** of the rewritten mirror: **0** occurrences of any of the 14 values,
   the private key or a `PRIVATE KEY-----` header; **0** `.pem` paths and **0** customer files in history.
   Branches: `Dev_Rupendra` 74 commits (101 .cs files, as before), `master` 68, `Dev_Shlok` 68.
   The rewritten mirror has no remote (filter-repo removes it), so it cannot be pushed by accident.

The dry run used the **local** copy of the branches. The real run must start from a fresh mirror of
GitHub (other developers may have pushed) and after the current fixes are committed and pushed.

### 3.2 Real run (owner, after section 2 preconditions)
```powershell
# fresh mirror of what is on GitHub now
git clone --mirror https://github.com/Rupiv/TradeLicenses.git D:\GBA-secure-backup\rewrite\TradeLicenses-final.git
cd D:\GBA-secure-backup\rewrite\TradeLicenses-final.git
git bundle create ..\..\TradeLicenses-before-rewrite-final.bundle --all      # last backup

# same rewrite as the dry run (point the script's SRC / MIRROR at this mirror, or:)
python -m git_filter_repo --force --replace-text ..\replacements.txt --invert-paths --path-glob '*.pem' `
  --path-glob '*Uploads/LicenceDocuments/*' --path-glob '*wwwroot/uploads/*'
git reflog expire --expire=now --all; git gc --prune=now
# re-run the scan part of rewrite_history.py: it must report 0 hits

# publish (THE irreversible step - only with the owner's written go-ahead)
git remote add origin https://github.com/Rupiv/TradeLicenses.git
git push --force --mirror origin
del ..\replacements.txt                                                    # contains the old values
```
`--mirror` pushes every branch and deletes remote branches that no longer exist in the mirror (none
expected). Pull-request refs (`refs/pull/*`) are read-only on GitHub and are rejected by the push;
that is normal - ask GitHub Support to purge them and cached views.

## 4. Consequences for other developers

- All commit hashes change. Existing clones, open pull requests and branches based on the old
  history become incompatible: **every developer must re-clone** (or hard-reset to the new branches)
  and must not push old branches again (that would re-introduce the secrets).
- Local, uncommitted work must be saved as patches before re-cloning.
- GitHub may keep the old commits reachable for a while (cached views, pull-request refs, forks).
  Ask GitHub Support to purge cached views and to remove unreferenced objects; forks must be
  deleted/re-created by their owners.
- CI/CD or deployments that reference old commit hashes must be updated (none found in this repository).

## 5. Approval checklist

- [ ] Rotation of every value in section 1 completed and verified
- [ ] Fixes committed and pushed; developers informed of the date
- [ ] Fresh backup bundle taken
- [ ] Owner approves running section 3
