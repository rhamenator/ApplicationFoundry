# Application Foundry

Application Foundry is being built as a local-first reverse recruiter. Its goal
is to help an applicant discover relevant jobs, compare them with a durable
record of experience, prepare tailored applications, and track each employer
through interviews and follow-up. The applicant controls what is sent to an
employer.

The current draft is an early slice of that workflow. It has a candidate
profile, evidence library, local fit signals, versioned resume and cover letter
drafts, approval before export, and an activity log. Job discovery currently
covers Remote OK, [Arbeitnow](https://www.arbeitnow.com/blog/job-board-api) Europe and UK, plus applicant-selected Greenhouse and Lever employer sites (a board or posting URL can fill in the site details). Local filters and ranking apply. A combined search can query the public feeds and selected boards together. It accepts TXT, DOCX, and text-based PDF source
documents for review, and supports reviewed, reversible import from one local
Access `.mdb` or `.accdb` table or a UTF-8, UTF-16 little-endian, or Windows-1252 CSV export. Broader discovery and the full workflow remain in
development.

Application form assistance is part of the intended workflow, reflecting the
original Access databases. The app should reuse verified profile details and
saved answers to prepare employer-specific form entries. Each applicant should
be able to choose the level of assistance: review and submit personally,
approve each submission, or authorize automatic submission within rules they
set. The current prototype can fill supported browser form fields only after an
exact-form approval and applicant review. It does not submit external forms.

Name data uses these explicit fields: title, first name, middle initial(s), last
name, suffix, and preferred display name.

## Run locally

```powershell
dotnet restore --force-evaluate
dotnet run --project src/ApplicationFoundry
```

The app creates or updates its SQLite database on startup. By default it stores
`app.db` in the current user's local application data directory under
`ApplicationFoundry`, outside the installation directory. Set
`ApplicationFoundry__DataDirectory` to an absolute path to choose another local
data directory, or configure `ConnectionStrings__DefaultConnection` to use a
different SQLite connection string. Back up `app.db` before upgrading an
existing installation.

For a Windows desktop trial, open the latest **Release installers** check on the
draft pull request, download the `application-foundry-windows` artifact, extract
its ZIP, and run the setup executable. It installs for the current user without
administrator rights. Its Start menu shortcut opens the desktop window without
a server console. This installer remains a draft until the release gates pass.

Approved desktop drafts can be saved with a Windows file
dialog as DOCX or PDF. The desktop app can help with an employer form:
the applicant captures its field structure in a browser, reviews prepared
answers in the app, explicitly authorizes a fill attempt, and inspects the
entered values before recording them. The browser helper never submits the
form. This workflow has passed against a synthetic local HTTPS form on Windows;
compatibility with employer sites remains unverified.

The desktop installer uses the current user's account and can be removed from
Windows Settings or its Start menu entry. Uninstall removes the app and its
shortcut but preserves the local applicant database at
`%LOCALAPPDATA%\ApplicationFoundry\app.db`. Delete that file yourself only if
you also want to erase the saved profile, jobs, documents, and application
history. On a fresh Windows installation, the first launch may take longer
than later launches while Windows finishes setting up the user profile.

## Bring Access records over

Choose a local `.mdb` or `.accdb` file up to 8 MB in the studio, then select
one table to preview. Jet 4 `.mdb` and ACE 12 `.accdb` synthetic files have
passed parser checks; Jet 3 and other ACE versions still need acceptance.
Password-protected databases may need a CSV export. To export, use Access's
**Export → Text File**, choose a comma delimiter, and include field names in
the first row. [Microsoft's Access export guide](https://support.microsoft.com/en-us/access/export-data-to-a-text-file)
shows the wizard. The studio accepts a comma-delimited `.csv` or `.txt` file up
to 8 MB. Select UTF-8, UTF-16 little-endian, or Windows-1252 for an older
Western European export before uploading. Use a one-record table or export one
applicant record for profile fields, or up to 5,000 records for evidence
claims. Review the mapping and proposed changes,
then confirm the import. Each imported batch can be undone from the studio.
The app does not save the source bytes in its database; it stores selected
values plus the file's name and hash locally.

The web host runs a small ONNX logistic model, while the Windows desktop host
uses a managed equivalent. Both score locally. Their four visible inputs measure keyword evidence, overall profile
coverage, role-title alignment, and evidence depth. It is a drafting aid, not a
hiring prediction. Users see an explanation and unmatched terms.

DOCX and PDF exports are blocked until the applicant approves that exact
document version. The browser helper captures form structure without entered
values, then fills approved values only after an exact-form authorization. It
does not bypass CAPTCHA or submit external forms. The applicant inspects the
result and records submission only after completing it manually.

The default no-op email sender is suitable only for local development. Configure
a real sender, durable data-protection keys, HTTPS termination, and production
secret storage before hosting for other users.

See [Architecture](docs/ARCHITECTURE.md) and
[Clean-room boundary](docs/CLEAN_ROOM_BOUNDARY.md). The remaining stable-release
gates are tracked in [Release scope](docs/RELEASE-SCOPE.md).
