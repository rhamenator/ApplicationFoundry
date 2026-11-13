# Application Foundry

Application Foundry is being built as a local-first reverse recruiter. Its goal
is to help an applicant discover relevant jobs, compare them with a durable
record of experience, prepare tailored applications, and track each employer
through interviews and follow-up. The applicant controls what is sent to an
employer.

The current draft is an early slice of that workflow. It has a candidate
profile, evidence library, local fit signals, versioned resume and cover letter
drafts, approval before export, and an activity log. Job discovery currently
covers Remote OK plus applicant-selected Greenhouse and Lever employer sites,
with local filters and ranking. It accepts TXT, DOCX, and text-based PDF source
documents for review, and supports reviewed, reversible import from one Access
table exported as UTF-8 CSV. Broader discovery and the full workflow remain in
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

For a Windows desktop trial, download the **Application Foundry Desktop
Prototype** installer from the draft pull request's build artifacts. It installs
for the current user without administrator rights. Its Start menu shortcut opens
a desktop window without a server console. The separately named **Application
Foundry** installer is the older local-server build; its shortcut opens a browser
and keeps a console window running. Choose the Desktop Prototype for a desktop
trial. In the pull request, open the latest **Release installers** check, download
the `application-foundry-windows-desktop-prototype` artifact, extract its ZIP,
and run the setup executable. Neither build is a stable release yet.

Approved desktop drafts can be saved with a Windows file
dialog as DOCX or PDF. The desktop prototype can help with an employer form:
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
