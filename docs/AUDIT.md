# Focused audit and disposition

The findings below retain the earlier focused-audit context. Their dispositions describe implemented mitigations; they do not certify production readiness or erase historical exposure.

| Priority | Finding | Disposition |
| --- | --- | --- |
| Critical | Identity/screening documents under public static paths | Private storage, authorized downloads, public-path block, offline legacy-copy procedure |
| High | Existing client could be linked by national-ID knowledge | Registration refuses existing records; verified invitation flow deferred |
| High | Sensitive audit values and unsafe administrator bootstrap | Audit values redacted; explicit bootstrap password only; historical data review required |
| High | Cross-client/duplicate loan and closed-screening mutations | Ownership/state checks, unique session index, regression coverage |
| High | Clean SQL migration attempted to index nvarchar(max) | Additive alignment migration; fresh database and simulated repaired-database upgrade checked |
| Medium | Weekly/biweekly installment dates did not advance correctly | Calendar-day intervals and regression tests |
| Medium | Request validation, CSRF and upload UI defects | Global antiforgery, constrained answer inputs, safe server-rendered upload refresh |
| Medium | Incorrect CI paths and unreproducible dependencies | Root-relative workflow, npm/NuGet locks and EF tool manifest |
| Portfolio | Missing/overstated documentation | English README, real screenshots, explicit limitations and publication instructions |

## Current disposition — October 27, 2026

The earlier SEC, PLAT, DEP, FIN, VAL, and CI maintenance phases are merged. At main `58e3a9c539f7d400905560a0831c095b624a30e1`, [run 40380933455](https://github.com/moaahmad01/agricultural-loan-screening-system/actions/runs/40380933455) passed on October 26, 2026 (UTC):

- SEC-01: questionnaire creation/overposting protections are covered by automated tests and 42 HTTP security checks, including Admin and Employee roles.
- PLAT/DEP: .NET 10/SDK 10.0.402 is in use. The run's npm installation reported zero vulnerabilities; the historical seven-advisory count is no longer the current result.
- FIN/VAL: repayment rows and totals reconcile at two-decimal precision, with regression coverage for residuals; MVC principal bounds and precision are enforced. Extreme repayment policies and internal service-validation boundaries remain documented limitations.
- CI: Node.js 24-based actions passed; 161 .NET tests, 40 standard HTTP checks, Docker/SQL startup, private-volume writes, soak, and disposable cleanup succeeded. Full Compose runtime verification is completed for this disposable environment.

## Historical evidence and accepted limitations

The October 22 snapshot recorded seven build-tool advisories and an inability to run the full Linux Compose stack; PLAT-01's October 24 snapshot recorded 76 automated tests and its own local upgrade experiments. Those results remain in [the historical verification record](../PORTFOLIO_READINESS.md#historical-publication-snapshot-october-22-2026), without being presented as current defects or newly repeated experiments.

Still outstanding: supported servicing and future dependency reassessment; questionnaire/rate-band and extreme-repayment policy review; historical questionnaire versioning; malware scanning and storage lifecycle; key encryption and hosting configuration; account recovery; automated SQL concurrency and comprehensive browser coverage. `punycode`/`url.parse()` and Browserslist notices are informational tooling/dependency notices. See [architecture](ARCHITECTURE.md#financial-limitations), [security](SECURITY.md#remaining-limitations), and [current evidence](../PORTFOLIO_READINESS.md#current-status). No production-readiness claim is made.
