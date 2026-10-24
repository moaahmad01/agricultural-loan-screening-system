# Focused audit and disposition

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

Outstanding: seven build-time npm advisories; ongoing .NET servicing; full Compose runtime verification; questionnaire/rate-band policy and rounding review; historical questionnaire versioning; malware scanning and storage lifecycle; automated SQL concurrency/browser coverage. No claim of production readiness. See [final evidence](../PORTFOLIO_READINESS.md).
