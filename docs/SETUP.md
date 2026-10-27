# Setup and operations

## Repository root

The repository root contains `AgriculturalLoanSystem.sln`, `Dockerfile`, and `.github/`. Run setup and validation commands from this directory.

## Prerequisites

For native development, use the .NET 10 SDK selected by `global.json` (10.0.402 with `latestFeature` roll-forward), Node.js 24/npm, and an accessible SQL Server instance. Windows LocalDB is an option on Windows only. Python 3 is needed only for the optional HTTP scripts, which use the standard library.

For the Docker route, use Docker Engine or Docker Desktop configured for Linux containers, with the Compose v2 plugin (`docker compose`). The Dockerfile builds Node/.NET assets inside its build stages; host .NET/Node installations are not needed for that route. Keep loopback ports 8080 and 1433 available.

## Local development

Use an isolated database such as `AgriculturalLoanPortfolio`; never point demonstration runs at production. Windows LocalDB was used for the historical native verification.

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

On a fresh database, startup seeds 5 sections, 24 questions, 12 rate bands, and Admin/Employee/Citizen roles. Reference-data seeding runs in both Development and Production, adding the initial collections when empty; it does not reset an edited questionnaire. It creates no clients.

`INITIAL_ADMIN_PASSWORD` opts into the first `admin` account only when that account does not exist. Choose a password satisfying Identity's policy: at least eight characters, with uppercase, lowercase, digit, and non-alphanumeric characters. No password is generated or logged. Empty means no bootstrap account is created; it does not remove an existing one. Remove the environment setting after bootstrap; it does not reset an existing password. Account recovery/password reset is not implemented.

Register a separate synthetic applicant for the citizen workflow. The bootstrapped administrator can use Manage Users to grant Employee access; only an administrator can grant or remove that role. Staff sign-in opens client search, while citizen sign-in opens the linked client profile.

### Development versus Production

Both supplied launch profiles (`http` and `https`) explicitly select **Development**, regardless of Release build configuration. Development startup applies migrations before seeding. Production startup seeds reference data but does **not** apply migrations.

There is no native Production launch profile. For a native Production-mode launch, explicitly configure `ASPNETCORE_ENVIRONMENT=Production`, `ConnectionStrings__DefaultConnection`, HTTP/HTTPS endpoints, and persistent private storage, and launch without a profile (`--no-launch-profile`) or run the published application. Apply reviewed migrations first. This describes startup behavior, not a complete production deployment procedure; see [deployment prerequisites](#deployment-prerequisites).

## Migrations and existing databases

The .NET 10 maintenance upgrade preserves all five migrations and requires no schema migration. Keep the existing database, document storage, and Data Protection keys. Preserve the content root/application identity when retaining authentication cookies. The Ubuntu-based Docker image keeps UID/GID 1000 so existing private-data volumes remain accessible; do not recreate volumes for this upgrade.

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

Copy `.env.example` to `.env`. Set a strong unique `DB_SA_PASSWORD` and optionally `INITIAL_ADMIN_PASSWORD`. Single-quote values in the environment file if they contain `$`. Choose a demo database password without connection-string delimiters such as semicolons: Compose interpolates it directly into a SQL connection string, and `.env` quoting alone does not escape that string. Keep `.env` private.

```sh
docker compose config --quiet
docker compose up --build -d
docker compose ps --all
docker compose logs migrator web
```

The startup order is **db → migrator → web**. Expect SQL to become healthy, the migrator to exit with code 0 after applying EF migrations, and the web service to start. Open [localhost:8080](http://localhost:8080) once HTTP is ready; the login page should render an antiforgery token. Container startup alone does not establish HTTP readiness.

Normal shutdown:

```sh
docker compose down
```

SQL health gates the migrator, which gates the web service. SQL Server Express data and private documents/Data Protection keys use named volumes. `down` preserves volumes; **do not use `down -v` on data you need**. SQL/HTTP ports are loopback-only. The non-root web user owns private storage in the image. Image tags receive vendor updates; exact digests are not pinned.

[Main CI run 40380933455](https://github.com/moaahmad01/agricultural-loan-screening-system/actions/runs/40380933455), on October 26, 2026 (UTC), verified the full disposable stack: Docker builds, healthy SQL, migrator exit 0, 11 application tables, five applied migrations, five seeded sections, HTTP/antiforgery readiness, UID/GID 1000:1000, and writable document/key directories. Both HTTP suites and the subsequent soak passed; cleanup removed the disposable resources. These results do not guarantee every host or existing volume will behave identically. Details: [current verification record](../PORTFOLIO_READINESS.md#current-status).

## Disposable HTTP verification

**Both scripts mutate their target database. Run them only against a fresh, isolated application/database containing synthetic data, never production, a valuable local demo, or existing customer documents.** Use a separately provisioned disposable database/Compose project and private volumes. Do not run another stack on the same bound ports. The default Compose project can contain data you want to keep; do not treat it as disposable automatically.

Run Python from the repository root. Both scripts accept `--base` and `--allow-synthetic-data`; always supply the base URL explicitly. They reject non-loopback hostnames and require explicit synthetic-data consent. Their default port is 5136; the following example targets a separately prepared disposable Docker application on port 8080.

### Standard HTTP smoke

`scripts/smoke_test.py` creates two synthetic applicants, a screening, private test documents, and a loan application. It leaves those records for inspection and does not change questionnaire configuration. With `DEMO_ADMIN_PASSWORD` set to this application's admin password, it runs **40 checks: 32 citizen/anonymous and eight admin/staff**. Without the optional credential, only the 32 non-admin checks execute.

### SEC-01 security HTTP suite

`scripts/sec01_http_test.py` requires a nonempty `DEMO_ADMIN_PASSWORD`. It expects exactly **five seeded sections and 24 questions**. Its **42 checks** cover seed validation, anonymous/citizen denial, CSRF and overposting behavior for both Admin and Employee, invalid references, weight limits, and preservation of existing questionnaire rows. Setup assertions additionally verify registration, login, promotion, and response parsing.

SEC-01 registers a synthetic applicant, promotes it to Employee, and adds **four sections with zero weight and four questions**. These mutations are retained. Re-running SEC-01 on that database will fail its initial seed-count check. Use a newly provisioned disposable environment for each complete sequence.

### Safe execution order

1. Provision a fresh isolated application and seeded database, with an explicitly bootstrapped admin; wait for readiness.
2. Run standard smoke and require success before continuing.
3. Run SEC-01 against the same disposable application.

PowerShell example, after the disposable application is already running:

```powershell
$env:DEMO_ADMIN_PASSWORD = [System.Net.NetworkCredential]::new('', (Read-Host 'Disposable application admin password' -AsSecureString)).Password
try {
    if ([string]::IsNullOrEmpty($env:DEMO_ADMIN_PASSWORD)) { throw 'Admin password is required for the full HTTP suites.' }
    python scripts/smoke_test.py --base http://127.0.0.1:8080 --allow-synthetic-data
    if ($LASTEXITCODE -ne 0) { throw 'Standard HTTP smoke failed; stop here.' }
    python scripts/sec01_http_test.py --base http://127.0.0.1:8080 --allow-synthetic-data
    if ($LASTEXITCODE -ne 0) { throw 'SEC-01 HTTP checks failed.' }
}
finally {
    Remove-Item Env:DEMO_ADMIN_PASSWORD -ErrorAction SilentlyContinue
}
```

Supply the admin password for this target application; Compose's `.env` is not automatically imported into the Python process. Credentials stay out of command-line arguments. Cookies remain in memory; result files are written to ignored `.local/` and are not workflow artifacts. Assertion/request failures exit nonzero.

GitHub CI creates a uniquely named disposable Compose project and removes its containers, network, and named volumes afterward. **Volume removal destroys the database, documents, and keys** and is appropriate only for those disposable resources. Normal local shutdown remains `docker compose down`. The current CI cleanup command tolerates teardown errors; successful removal is confirmed in the cited run, not guaranteed by that command alone.

## Additional verification

```sh
dotnet list AgriculturalLoanSystem.sln package --vulnerable --include-transitive
npm audit --prefix AgriculturalLoanSystem
```

Dependency findings are time-specific. Consult [current CI evidence](../PORTFOLIO_READINESS.md#current-status) rather than treating an older audit count as the present state.

## Deployment prerequisites

No live deployment is included. A small deployment could use an ASP.NET-compatible host with SQL Server and private persistent storage; Docker is another option on a maintained host. Cost, licensing, host choice, and public exposure need the owner's approval.

Before deployment: maintain supported .NET servicing patches and reassess current dependency findings; validate domain policy; use a least-privilege database user; configure TLS/Secure cookies and trusted proxy forwarding; restrict AllowedHosts; encrypt/protect Data Protection keys; arrange encrypted document/database backups; add malware scanning, quotas, retention, monitoring, and account recovery. Do not publish the local Compose stack unchanged.
