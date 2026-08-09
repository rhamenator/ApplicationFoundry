# Architecture

The Blazor Web App uses interactive server rendering and ASP.NET Core Identity.
Long-lived components create short-lived SQLite contexts through
`IDbContextFactory<ApplicationDbContext>`. Ownership checks use the authenticated
user identifier both in UI queries and export endpoints.

The first vertical slice has four feature boundaries:

- profile and evidence: normalized personal naming and user-authored proof;
- opportunities: pasted job content, status, and activity history;
- fit: deterministic feature extraction followed by local ONNX inference and a
  human-readable explanation;
- documents: evidence-ranked drafts, immutable versions, explicit approval, and
  minimal DOCX/PDF exporters.

The ONNX graph is created from a compact embedded protobuf representation, so
inference needs no network service and no candidate text leaves the process.
The model is intentionally small and inspectable. Future trained models must be
documented, evaluated for disparate impact, versioned, and remain advisory.

The browser-assist boundary copies selected content to the clipboard. There is
no external submission endpoint by design.
