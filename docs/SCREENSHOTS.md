# Application screenshots

The five JPEG images in [screenshots](screenshots/) were captured from the actual locally running application during the October 21, 2026 verification, using synthetic demonstration data:

- [Landing page](screenshots/landing.jpg): anonymous English introduction.
- [Administrator dashboard](screenshots/dashboard.jpg): local seeded administration view.
- [Screening](screenshots/screening.jpg): questionnaire with the existing visual design.
- [Results](screenshots/results.jpg): stored synthetic screening result.
- [Calculator](screenshots/calculator.jpg): repayment preview.

These are viewport captures, not fabricated mockups or evidence of public hosting. Captures were taken at different stages of the demo database, so counts can differ. An all-Yes synthetic result illustrates the workflow, not a validated lending decision. Minor subsequent validation/formatting fixes may differ from the images.

To reproduce: follow [setup](SETUP.md), register a synthetic applicant, complete a screening, open its report and calculator, and use a locally bootstrapped administrator for the dashboard. Never use real IDs or documents. A 390-pixel viewport was checked for horizontal overflow; this is not a comprehensive accessibility or browser compatibility audit.
