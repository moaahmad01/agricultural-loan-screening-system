# Setup and operations

## Repository root

The repository root contains `AgriculturalLoanSystem.sln`, `Dockerfile`, and `.github/`. Run setup and validation commands from this directory.

## Local development

Install .NET 9 SDK (`global.json` accepts newer .NET 9 feature bands), Node.js 24, and SQL Server. Windows LocalDB was used for verification. Use an isolated database such as `AgriculturalLoanPortfolio`; never point demonstration runs at production.

```powershell
dotnet tool restore
dotnet restore AgriculturalLoanSystem.sln --locked-mode
npm ci --prefix AgriculturalLoanSystem
npm run build:css --prefix AgriculturalLoanSystem
dotnet build AgriculturalLoanSystem.sln -c Release --no-restore
dotnet test AgriculturalLoanSystem.sln -c Release --no-build
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ConnectionStrings__DefaultConnection = 'Server=(localdb)\mssqllocaldb;Database=AgriculturalLoanPortfolio;Trusted_Connection=True;TrustServerCertificate=True'
$env:INITIAL_ADMIN_PASSWORD = [System.Net.NetworkCredential]::new('', (Read-Host 'Choose initial admin password' -AsSecureString)).Password
dotnet run --project AgriculturalLoanSystem -c Release --no-build --launch-profile http
```

The HTTP profile uses port 5136. For local HTTPS, trust the development certificate with `dotnet dev-certs https --trust` and use `--launch-profile https` (port 7252). This changes local certificate trust; perform it only on your own development machine.

For Linux/macOS, set `ConnectionStrings__DefaultConnection` to an accessible SQL Server instance; LocalDB is Windows-only. The app does not load `.env` when launched with `dotnet run`; Compose does.

First startup seeds 5 sections, 24 questions, 12 rate bands, and Admin/Employee/Citizen roles. It creates no clients. `INITIAL_ADMIN_PASSWORD` opts into the first `admin` account, subject to Identity's password policy. No password is generated or logged. Empty means no admin account. Remove the setting after bootstrap; it does not reset an existing account. Account recovery/password reset is not implemented.

## Migrations and existing databases

Back up and test on a restored copy before upgrading a real installation. Keep existing migrations/history. Development applies migrations on startup; Production does not.

```sh
dotnet tool restore
dotnet ef migrations has-pending-model-changes --project AgriculturalLoanSystem --configuration Release
dotnet ef migrations script --idempotent --project AgriculturalLoanSystem --output migration.sql
dotnet ef database update --project AgriculturalLoanSystem
```

Do not commit scripts containing environment-specific data or connection strings. The tool uses configuration/environment; avoid credentials in command arguments.

`20260717090000_AlignAuditStorage` intentionally sorts before the pre-existing audit-index migration. The original schema used `nvarchar(max)` for `AuditLogs.UserId`, which SQL Server cannot index. The correction narrows it to `nvarchar(450)`, refuses overlength values, and preserves an existing UserId index when upgrading a previously repaired deployment. It also removes the redundant single-column answer/session index. Original migrations remain intact.

`UniqueLoanPerScreening` adds a filtered unique index. It refuses duplicate non-null SessionIds with an explicit error; it never deletes duplicates automatically. Preflight queries:

```sql
SELECT SessionId, COUNT(*) AS LoanCount
FROM LoanCalculations WHERE SessionId IS NOT NULL
GROUP BY SessionId HAVING COUNT(*) > 1;
SELECT Id FROM AuditLogs WHERE LEN(UserId) > 450;
SELECT SessionId, QuestionId, COUNT(*) AS AnswerCount
FROM ScreeningAnswers GROUP BY SessionId, QuestionId HAVING COUNT(*) > 1;
```

Resolve conflicts through authorized data review, preserving legitimate history. The original screening-integrity migration also requires unique answers. Existing string enum values must match current enum names; historical noncanonical category values need a separately reviewed data conversion.

Use [the offline security procedure](SECURITY.md#legacy-document-migration) for existing documents. EF migrations do not move files or erase historical audit secrets.

## Docker local demo

Copy `.env.example` to `.env`. Set a strong unique `DB_SA_PASSWORD` and optionally `INITIAL_ADMIN_PASSWORD`. Single-quote values in the environment file if they contain `$`; connection-string metacharacters such as semicolons require escaping. Keep `.env` private.

```sh
docker compose config --quiet
docker compose up --build -d
docker compose logs migrator web
docker compose down
```

SQL health gates the migrator, which gates the web service. SQL Server Express data and private documents/Data Protection keys use named volumes. `down` preserves volumes; **do not use `down -v` on data you need**. SQL/HTTP ports are loopback-only. The non-root web user owns private storage in the image. Image tags receive vendor updates; exact digests are not pinned.

Compose syntax was verified. Image building, SQL container health, non-root volume permissions, and container persistence remain unverified because the Docker engine was unavailable.

## Additional verification

```sh
python scripts/smoke_test.py --allow-synthetic-data
dotnet list AgriculturalLoanSystem.sln package --vulnerable --include-transitive
npm audit --prefix AgriculturalLoanSystem
```

The smoke script uses real cookies, antiforgery tokens, uploads, and controllers. It allows loopback URLs only, creates synthetic records, and leaves them for inspection. Start with a fresh seeded database and the original five sections. Optional `DEMO_ADMIN_PASSWORD` adds staff checks. Never expose this test environment publicly. Results are written to ignored `.local/`.

## Deployment prerequisites

No live deployment is included. A small deployment could use an ASP.NET-compatible host with SQL Server and private persistent storage; Docker is another option on a maintained host. Cost, licensing, host choice, and public exposure need the owner's approval.

Before deployment: upgrade to a supported framework/current servicing patches; resolve dependency findings; validate domain policy; use a least-privilege database user; configure TLS/Secure cookies and trusted proxy forwarding; restrict AllowedHosts; encrypt/protect Data Protection keys; arrange encrypted document/database backups; add malware scanning, quotas, retention, monitoring, and account recovery. Do not publish the local Compose stack unchanged.
