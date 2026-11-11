# Application Foundry

Application Foundry is a local-first career studio built on .NET 10, ASP.NET
Core, Blazor, Identity, and SQLite. It keeps a standardized candidate profile,
an evidence library, imported job descriptions, versioned resume and cover
letter drafts, explainable local fit signals, and an application activity log.

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

The fit scorer runs a small ONNX logistic model entirely in the application
process. Its four visible inputs measure keyword evidence, overall profile
coverage, role-title alignment, and evidence depth. It is a drafting aid, not a
hiring prediction. Users see an explanation and unmatched terms.

DOCX and PDF exports are blocked until the signed-in user approves that exact
document version. The included browser-assist bookmarklet only copies a page
title, URL, and selected text. Application Foundry never bypasses CAPTCHA,
submits external forms, or sends unattended batches. A user records submission
only after completing it manually.

The default no-op email sender is suitable only for local development. Configure
a real sender, durable data-protection keys, HTTPS termination, and production
secret storage before hosting for other users.

See [Architecture](docs/ARCHITECTURE.md) and
[Clean-room boundary](docs/CLEAN_ROOM_BOUNDARY.md).
