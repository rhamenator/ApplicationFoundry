# Application Foundry release scope

Application Foundry is a local-first reverse recruiter for the applicant. A
stable release must support a complete path from existing records and job
discovery through tailored documents, application forms, and follow-up. The
applicant chooses how much submission automation to authorize.

| Release gate | Acceptance evidence | Current state |
| --- | --- | --- |
| Document import | Import and review existing TXT, DOCX, and PDF résumés, cover letters, and source material; map useful facts into the profile and evidence library. Import the owner's legacy Access data through a previewed, reversible mapping. | TXT/DOCX/text-PDF extraction and saved text are implemented. Profile mapping and Access migration remain. Scanned PDF OCR remains. |
| Document export | Export the approved exact version of a tailored résumé and cover letter as usable DOCX and PDF from the Windows desktop app. | Web endpoints and desktop save dialog are implemented; clean Windows workflow verification remains. |
| Application form assistance | Reuse verified profile facts and prepared answers for employer-specific forms. Offer applicant-selected modes: review and submit personally, approve each submission, or automatic submission within applicant-defined rules. Record what was entered and sent. | Not implemented. |
| Broader job discovery | Search more than one source, including roles beyond remote-only listings; support applicant filters, deduplication, source attribution, and local fit ranking. | Remote OK feed only. |
| Full workflow acceptance | On a clean Windows VM clone, install, import synthetic documents, create a profile, discover and evaluate jobs, tailor and export documents, exercise each form-assistance mode with fake data, record follow-up, restart and verify persistence, then uninstall. | Installer and first launch tested. Full workflow not tested. |

Do not call the product release-ready until every gate has passing evidence and
the independent review described in [RELEASE-READINESS-REVIEW.md](RELEASE-READINESS-REVIEW.md)
is complete. The old Access records belong to the owner; do not commit them or
include them in test fixtures or release packages.
