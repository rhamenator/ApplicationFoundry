# Application Foundry release scope

Application Foundry is a local-first reverse recruiter for the applicant. A
stable release must support a complete path from existing records and job
discovery through tailored documents, application forms, and follow-up. The
applicant chooses how much submission automation to authorize.

| Release gate | Acceptance evidence | Current state |
| --- | --- | --- |
| Reusable document services | Share neutral extraction and document handling with ATS through a separately versioned repository/package that both projects can build and test independently. Keep applicant records and workflow permissions in their owning apps. | `document-extraction-dotnet` already exists as a private reusable repository. Application Foundry currently uses the same public parsing libraries locally because its public CI cannot fetch that private source; packaging/access and broader management reuse remain. |
| Document import | Import and review existing TXT, DOCX, and PDF résumés, cover letters, and source material; map useful facts into the profile and evidence library. Import the owner's legacy Access data through a previewed, reversible mapping. | TXT/DOCX/text-PDF extraction and saved text are implemented. A reviewed mapping step now suggests contact details, summary, and bullet claims for the profile and evidence library; Windows acceptance of DOCX/PDF mapping remains. Access migration and scanned PDF OCR remain. |
| Document export | Export the approved exact version of a tailored résumé and cover letter as usable DOCX and PDF from the Windows desktop app. | Web endpoints and desktop save dialog are implemented. A synthetic résumé was approved and saved as DOCX and PDF on an independent Windows 11 clone; document quality and the cover-letter path still need acceptance. |
| Application form assistance | Reuse verified profile facts and prepared answers for employer-specific forms. Offer applicant-selected modes: review and submit personally, approve each submission, or automatic submission within applicant-defined rules. Record what was entered and sent. | Not implemented. |
| Broader job discovery | Search more than one source, including roles beyond remote-only listings; support applicant filters, deduplication, source attribution, and local fit ranking. | Remote OK feed only. |
| Full workflow acceptance | On a clean Windows VM clone, install, import synthetic documents, create a profile, discover and evaluate jobs, tailor and export documents, exercise each form-assistance mode with fake data, record follow-up, restart and verify persistence, then uninstall. | Installer, synthetic TXT import, profile/evidence creation, manual job analysis, approval, résumé DOCX/PDF save, and persistence after an upgrade were checked on an independent clone. Discovery, application forms, follow-up, document quality, and uninstall remain. |

Do not call the product release-ready until every gate has passing evidence and
the independent review described in [RELEASE-READINESS-REVIEW.md](RELEASE-READINESS-REVIEW.md)
is complete. The old Access records belong to the owner; do not commit them or
include them in test fixtures or release packages.
