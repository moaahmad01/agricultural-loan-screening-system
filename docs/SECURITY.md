# Security and private data

## Controls implemented

Documents are stored outside the web root in `App_Data/Documents`, with random names, constrained storage keys, a 5 MiB file limit, and extension/signature checks. Download actions require authentication and persisted client ownership or an Admin/Employee role. Responses are attachments with no-store and nosniff headers. `/uploads` is blocked before static-file middleware. Private storage is excluded from Git and publishing.

Unsafe MVC requests use antiforgery validation. Registration cannot claim an existing client by supplying its national ID. Role changes require an administrator. Screening mutations check session state; loan creation checks client/session ownership and uniqueness. Audit field values are redacted. Administrator creation requires an explicitly supplied `INITIAL_ADMIN_PASSWORD`; no production password is bundled.

## Legacy-document migration

1. Take the old application offline. Block `/uploads` at any reverse proxy/CDN too; preserve a protected database and file backup.
2. From the solution root, preview `./scripts/Copy-LegacyDocuments.ps1 -ApplicationRoot ./AgriculturalLoanSystem`.
3. Run the same command with `-Apply`. The script copies supported legacy files to private storage, verifies hashes, and refuses conflicting destinations. It preserves the originals.
4. Review the copies and database references. With the data owner's authorization, move the originals completely outside the web root and remove any publicly cached copies. Do not leave an older deployment serving them.
5. Start the updated application and verify owner/staff downloads, unrelated-user denial, and public-path 404 responses. Legacy database references resolve only to the private directory; there is no public fallback.

EF migrations do not move files. Previously exposed documents or historical audit values require a separate incident/retention review; these changes cannot retract already downloaded information. Never commit backups or migration data.

## Remaining limitations

This is not a security certification. Upload signatures are not malware scanning. Storage quotas, retention, encrypted key management, atomic file/database operations, and SQL concurrency tests remain incomplete. Private orphan files can remain after failed operations. Historical audit entries are not automatically sanitized; audit records are not tamper-proof. Questionnaire policy and historical versioning require further work.

Before public hosting, configure TLS, Secure cookies, trusted proxies, AllowedHosts, least-privilege database access, encrypted backups and keys, account recovery, and operational monitoring. The current CSP allows inline content. No MFA or verified invitation flow for staff-created clients is implemented.

Seven npm build-tool advisories remain (two moderate, five high); the production-only npm audit reported none. Do not treat this as zero dependency risk. .NET 10 LTS is used; keep the SDK, runtime, and EF Core/Identity packages on supported servicing releases.

No private vulnerability-reporting address has been configured. The owner should establish one before publication. Do not post credentials or personal documents in public issues.
