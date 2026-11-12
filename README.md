# Application Foundry

Application Foundry is being built as a local-first reverse recruiter. Its goal
is to help an applicant discover relevant jobs, compare them with a durable
record of experience, prepare tailored applications, and track each employer
through interviews and follow-up. The applicant controls what is sent to an
employer.

The current release is an early slice of that workflow. It has a candidate
profile, evidence library, local fit signals, versioned resume and cover letter
drafts, approval before export, and an activity log. The experimental desktop
branch adds a Remote OK feed whose listings are ranked locally; broader job
discovery, application form assistance, and the full follow-up workflow remain
in development. It also accepts TXT, DOCX, and text-based PDF source documents
for review; imported text can be copied into the profile and evidence library.

Application form assistance is part of the intended workflow, reflecting the
original Access databases. The app should reuse verified profile details and
saved answers to prepare employer-specific form entries. Each applicant should
be able to choose the level of assistance: review and submit personally,
approve each submission, or authorize automatic submission within rules they
set. The current prototype does not fill or submit external forms.

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

On Windows, the installer places Application Foundry in the current user's
applications folder without requesting administrator rights. The Start menu
shortcut starts the local server and opens the app in the default browser.
Keep the Application Foundry console window open while using the app; closing
it stops the local server. The setup wizard can launch the app when installation
finishes.

Pull request builds also produce a separate **Application Foundry Desktop
Prototype** installer. Its Start menu shortcut opens a desktop window without
the server console. Approved desktop drafts can be saved with a Windows file
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

The fit scorer runs a small ONNX logistic model entirely in the application
process. Its four visible inputs measure keyword evidence, overall profile
coverage, role-title alignment, and evidence depth. It is a drafting aid, not a
hiring prediction. Users see an explanation and unmatched terms.

DOCX and PDF exports are blocked until the signed-in user approves that exact
document version. The included browser-assist bookmarklet only copies a page
title, URL, and selected text. The current app does not bypass CAPTCHA, submit
external forms, or send unattended batches. A user records submission only
after completing it manually.

The default no-op email sender is suitable only for local development. Configure
a real sender, durable data-protection keys, HTTPS termination, and production
secret storage before hosting for other users.

See [Architecture](docs/ARCHITECTURE.md) and
[Clean-room boundary](docs/CLEAN_ROOM_BOUNDARY.md). The remaining stable-release
gates are tracked in [Release scope](docs/RELEASE-SCOPE.md).
