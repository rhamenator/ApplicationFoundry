<p align="center">
  <img src=".github/assets/logo.png" alt="ApplicationFoundry logo" width="220">
</p>

# Application Foundry

Application Foundry is a local-first career studio built on .NET 10, ASP.NET
Core, Blazor, Identity, and SQLite. It keeps a standardized candidate profile,
an evidence library, imported job descriptions, versioned resume and cover
letter drafts, explainable local fit signals, and an application activity log.

Name data uses these explicit fields: title, first name, middle initial(s), last
name, suffix, and preferred display name.

## Run locally

```powershell
dotnet restore --locked-mode
dotnet ef database update --project src/ApplicationFoundry --startup-project src/ApplicationFoundry
dotnet run --project src/ApplicationFoundry
```

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
