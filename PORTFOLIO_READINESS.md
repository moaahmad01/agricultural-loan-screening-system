# Portfolio verification

## Current status

Documentation status date: **October 27, 2026**. Verified application/workflow baseline:

- Tested main baseline SHA: `58e3a9c539f7d400905560a0831c095b624a30e1`.
- Successful main push run: [40380933455](https://github.com/moaahmad01/agricultural-loan-screening-system/actions/runs/40380933455), October 26, 2026 (UTC).
- ASP.NET Core MVC/Razor on .NET 10; SDK 10.0.402; EF Core/Identity 10.0.13; SQL Server; Tailwind CSS 3.4.19; Node.js 24.

| Verified check | Result in the referenced run |
| --- | --- |
| .NET automated tests | 161 passed; zero failed/skipped |
| Standard HTTP smoke | 40 PASS: 32 citizen/anonymous + 8 admin/staff |
| SEC-01 HTTP security | 42 PASS: seed 1, anonymous/citizen access 4, Admin 18, Employee 18, final preservation 1 |
| Locked restore, npm install, CSS build | Passed |
| Release build | Zero warnings and zero errors |
| EF model consistency and Compose configuration | Passed |
| Docker application/migrator image builds | Passed |
| SQL Server and migrator | Healthy; migrator exit code 0 |
| Database observations before HTTP suites | 11 application tables, 5 applied migrations, 5 seeded sections |
| HTTP readiness and login antiforgery | Passed |
| Runtime identity and private storage | UID/GID 1000:1000; document/key-directory write checks passed |
| Stability soak after both HTTP suites | Web stayed running with zero restarts; SQL stayed healthy |
| Disposable cleanup | Three containers, two named volumes, and the network removed |
| Artifact upload | TRX test-results artifact uploaded successfully |
| Node.js 24-based actions | checkout v5 in both jobs; setup-dotnet v5; setup-node v5; upload-artifact v6 passed on runner 2.338.0 |
| Node.js 20 action-runtime notices | None observed |
| npm installation audit output | Zero vulnerabilities reported at the time of this run |

These are **243 checks across three distinct suites**, not 243 .NET unit tests. The database migration assertion currently requires **at least one** applied migration; this run observed five. The cleanup command tolerates teardown errors; actual removal succeeded in this run. Neither behavior is an unconditional guarantee for another run or installation.

Remaining notices were Browserslist data age (`caniuse-lite`) and JavaScript `punycode`/`url.parse()` deprecations. They are separate from compiler warnings and vulnerability-advisory counts. No dependency scan result guarantees future security.

### Current limitations

This is a source-code portfolio and screening aid, without a verified public demo URL, production deployment validation, external penetration testing/certification, load testing, or exhaustive business-rule coverage. Domain policy, questionnaire versioning, small-principal/long-duration repayment suitability, and the browser/server preview difference remain limitations. Saved schedules are not automatically rewritten. See [financial behavior](docs/ARCHITECTURE.md#financial-limitations).

Keys/documents are persistent but key encryption at rest, production TLS/reverse-proxy configuration, least-privilege SQL access, backups, malware scanning, quotas/retention, account recovery, and comprehensive SQL concurrency/browser coverage still need operational work. Documentation corrections do not implement those capabilities. See [security](docs/SECURITY.md#remaining-limitations) and [setup](docs/SETUP.md#deployment-prerequisites).

The historical snapshots below are retained as originally recorded evidence, not independently rerun during this documentation update. The evidence above applies to the cited tested baseline; documentation changes do not establish additional runtime verification.

## CV project entry

**Agricultural Loan Screening System — C#, ASP.NET Core MVC, .NET 10, EF Core, SQL Server**

Developed a bilingual agricultural loan screening system using ASP.NET Core MVC, SQL Server, EF Core, and Identity, with role-based access, ownership-authorized private documents, weighted questionnaires, and financial repayment scheduling. Added validation and regression coverage verified by 161 .NET automated tests, 40 standard HTTP checks, and 42 security HTTP checks in disposable Docker/SQL CI.

## Historical publication snapshot: October 22, 2026

The owner confirmed the required rights to publish the project materials on October 22, 2026. No application license has been assigned. This is a source-code portfolio, not a production deployment or security certification.

### Publication review — historical

- Git was already initialized on `main`, with no commits or remote at the start of publication preparation.
- The source, tests, migrations, lockfiles, build configuration, documentation, third-party notices, and five application screenshots were reviewed for inclusion.
- Common token/private-key scans found no matches. Credential-related matches were synthetic test markers, randomly generated local smoke-test passwords, and package names. Configuration contains no real database password or bootstrap credential.
- Screenshots were visually inspected and show demonstration data, including the clearly named Synthetic Orchard Demo client. Documentation image/file links resolve.
- Ignore rules exclude environment files, local tools/reports, private uploads, App_Data, database files/exports, certificates/keys, archives, dependency caches, test output, and .NET build/publish output. Served frontend assets and upstream notices are intentionally included.
- An unrelated terminal-help capture was moved to ignored local storage. First-party trailing whitespace was cleaned; upstream vendor content is preserved.

### Local verification — historical October 22 snapshot

| Check | Result |
| --- | --- |
| Locked NuGet restore | Passed |
| Clean npm install and frontend build | Passed |
| Release build | Passed: zero warnings and errors |
| Automated tests | 64 passed, zero failures or skips |
| EF migration snapshot | No pending model changes |
| Docker Compose configuration | Passed with a validation-only value; no real credential used |
| Production npm dependencies | Audit reported zero vulnerabilities |
| Documentation links and candidate file safety | Passed |

The downloaded EF tool manifest initially carried a Windows downloaded-file block; unblocking the inspected manifest allowed tool restore and the snapshot check to pass. Local .NET validation used `MSBuildEnableWorkloadResolver=false` for this machine's SDK setup. Neither workaround is required in the Ubuntu GitHub Actions workflow.

### Earlier verification — historical October 21 snapshot

The October 21 preparation recorded 40 local HTTP smoke checks using a synthetic SQL Server database, fresh migration application, a simulated repaired-database upgrade, and SQL uniqueness/index checks. These database/HTTP scenarios were not rerun during publication. See [setup](docs/SETUP.md), [architecture](docs/ARCHITECTURE.md), and [focused audit](docs/AUDIT.md) for behavior and limitations.

### Limitations recorded on October 22 — historical

- Seven npm build-tool advisories remain: two moderate and five high. The production-only audit found none. Browserslist data also reports that it is outdated.
- .NET 9 support ends November 10, 2026; plan a tested framework upgrade before ongoing deployment beyond support.
- Docker configuration validation does not prove image execution, container SQL health, volume permissions, or persistence. The prior preparation could not run the Linux engine.
- Unit tests use EF InMemory. SQL concurrency and comprehensive browser/accessibility CI coverage remain future work.
- Domain-policy review, historical questionnaire versioning, financial rounding/rate-band review, malware scanning, storage quotas/retention, and account recovery remain incomplete.
- Legacy public documents and historical audit data require the offline procedure in [security](docs/SECURITY.md). No real customer data should be used in demonstrations.

### GitHub verification description — historical October 22 snapshot

The workflow in `.github/workflows/build-and-test.yml` restores locked dependencies, builds frontend assets and the Release solution, runs tests, checks EF model consistency, and validates Compose configuration. The authenticated account is `moaahmad01`; the intended public repository is [agricultural-loan-screening-system](https://github.com/moaahmad01/agricultural-loan-screening-system). The [Actions page](https://github.com/moaahmad01/agricultural-loan-screening-system/actions/workflows/build-and-test.yml) and README badge provide the hosted verification result. A local successful build alone does not establish a successful hosted run.

## Historical PLAT-01 local verification — October 24, 2026

Candidate based on approved SEC-01 commit `64c3967`; pending independent review, with no commit, push, or hosted CI run during this phase.

- Both projects target .NET 10; SDK 10.0.402, ASP.NET Core/runtime 10.0.13, EF Core/Identity and EF tooling 10.0.13. Existing test-tool and npm versions are unchanged.
- SqlClient's transitive PKCS 9.0.11 dependency produced security advisories. An explicit 10.0.13 reference selects the patched servicing version; the final NuGet vulnerability query reported no affected packages.
- Tool restore, deliberate lockfile regeneration, locked restore, and Release build passed. Build: zero warnings/errors. All 76 automated tests passed; EF reported no pending model changes. All five historical migrations remain unchanged.
- A populated synthetic .NET 9 database was opened by .NET 10 without changes to any of 19 tables' data hashes or the SQL schema hash. Existing authentication cookies, antiforgery tokens, and private identity documents remained usable with the same content root and key storage.
- Both fresh .NET 10 and upgraded synthetic LocalDB databases passed the existing 40 HTTP checks and 42 SEC-01 HTTP checks each. The baseline .NET 9 fixture also passed 40 checks before upgrade.
- Docker application/migrator builds and the container EF model check passed. Ubuntu lacks `adduser`; renaming its UID/GID 1000 account preserves existing volume ownership. The final app user has no supplementary groups. A volume populated by .NET 9 retained readable document/key fixtures and allowed .NET 10 writes as non-root.
- Full SQL Server container/Compose startup and hosted GitHub Actions were not run. Existing frontend warnings and previously disclosed business-policy/rounding limitations remain outside PLAT-01.
