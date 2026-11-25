# Data-protection considerations: customer documents in a public repository

Audience: the system owner and whoever advises them on legal / regulatory duties.
This document collects facts and questions. **It is not legal advice and makes no notification
decision** - that decision belongs to the owner (and their legal / DPO advisers).

## 1. What was exposed (facts from the repository)
- Repository `Rupiv/TradeLicenses` was publicly readable (still on 2026-11-25).
- In the current source: 25 uploaded licence-application documents
  (`src/Gba.TradeLicense.Api/Uploads/LicenceDocuments/2026/10/**`: PDFs and photos) and 7 generated
  licence certificates (`src/Gba.TradeLicense.Api/wwwroot/uploads/licence-certificates/**`).
- In Git history: 63 distinct customer files (the above plus copies in the old `Deploy/` snapshots).
- Published packages before 2026-11-24 also contained the certificate files (the `wwwroot` folder was
  packaged); fixed in source, but earlier server copies / zips may contain them.
- In addition: database credentials. Anyone who used them could have read the whole database
  (applicant names, mobile numbers, addresses, trade and payment details).
- Exposure start: the earliest commit of these files (owner to confirm with `git log --diff-filter=A`
  on the listed paths). End: when the repositories are made private.

The document *contents* were not opened during this review; their categories (identity / address /
property documents, trade details, photographs) follow from the application's document types.

## 2. Questions for the owner and advisers
1. Which applicants are affected? (The file names are random ids; map them to applications via
   `Licence_Application_Document.FilePath` on the database - read only.)
2. Is there evidence of access? GitHub Insights > Traffic (clones / views, 14 days), forks, secret-scanning
   alerts, and the database log review in `INCIDENT_RESPONSE.md` section 4.
3. Which obligations apply to the GBA / corporations as data fiduciaries, and to the developer as a
   processor, under applicable Indian law and government IT policy - for example the Digital Personal
   Data Protection Act, 2023 (intimation of a personal-data breach to the Data Protection Board and to
   affected persons) and CERT-In directions on reporting cyber-security incidents (short reporting
   deadlines)? Which deadlines run from which date?
4. Are there contractual reporting duties (e.g. to the corporations or the state IT department)?
5. Communication to affected applicants: content, channel (SMS / letter), support contact.

## 3. Containment and remediation (technical, independent of the legal decision)
| Step | Status |
|---|---|
| Make both repositories private | **open** |
| Remove customer files from source and future packages | done in code (excluded from publish, `.gitignore`); files still tracked until the history rewrite |
| Remove customer files from Git history | prepared and verified by dry run (`HISTORY_CLEANUP_VERIFICATION.md`); not executed |
| Ask GitHub Support to purge cached views; check / remove forks | open (after rewrite) |
| Rotate database and other credentials | open (`INCIDENT_RESPONSE.md` section 3) |
| Delete old packages / zips that contain certificates (servers, shared drives) | owner to check |
| Keep an incident record (timeline, decisions, evidence) | owner |

## 4. Record keeping
Keep: the dates above, screenshots of GitHub traffic / visibility changes, the rotation log, the log
reviews, the decision on notification and its reasons, and any communication sent.
