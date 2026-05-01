<!--
SYNC IMPACT REPORT
==================
Version change: (none) → 1.0.0
Bump rationale: Initial ratification of the project constitution. All placeholders
in the template have been replaced with concrete principles, governance, and
metadata for the Document OCR Processor solution.

Modified principles:
- (template) [PRINCIPLE_1_NAME] → I. Code Quality & Maintainability
- (template) [PRINCIPLE_2_NAME] → II. Testing Standards (NON-NEGOTIABLE)
- (template) [PRINCIPLE_3_NAME] → III. User Experience Consistency
- (template) [PRINCIPLE_4_NAME] → IV. Performance & Reliability
- (template) [PRINCIPLE_5_NAME] → V. Security & Secure-by-Default

Added sections:
- Engineering Constraints & Technology Standards (replaces SECTION_2)
- Development Workflow & Quality Gates (replaces SECTION_3)
- Governance

Removed sections: None (template scaffolding only).

Templates requiring updates:
- ✅ .specify/templates/plan-template.md — "Constitution Check" gate references
  this file generically; no content edits required. Constitution Check should be
  populated per-feature using principles I–V at plan time.
- ✅ .specify/templates/spec-template.md — No constitution-specific edits
  required; UX and security mandates surface through the planning gate.
- ✅ .specify/templates/tasks-template.md — No edits required; testing and
  observability task categories already align with principles II and IV.
- ✅ .specify/templates/checklist-template.md — No edits required; checklists
  may now reference principles I–V by name.

Follow-up TODOs:
- TODO(RATIFICATION_DATE): Confirm with project owner whether the original
  adoption date of governance for this repository predates today (2026-05-01).
  If a verifiable earlier date exists, update the metadata line and bump PATCH.
-->

# Document OCR Processor Constitution

## Core Principles

### I. Code Quality & Maintainability

All production code MUST be readable, consistently styled, and reviewed before merge.

- The solution MUST build cleanly: `dotnet build` for `DocumentOcr.sln` MUST
  succeed with zero errors and zero new warnings introduced by the change.
- Public APIs (Functions, services, controllers) MUST be exposed through
  interfaces in `DocumentOcr.Common/Interfaces` and registered via DI in
  `Program.cs`; one-off concrete dependencies in callers are prohibited.
- Each service MUST have a single responsibility; files SHOULD stay under ~300
  lines and methods under ~50 lines. Larger units MUST be justified in review.
- Nullable reference types MUST remain enabled; `!` (null-forgiving) is allowed
  only with an inline comment explaining the invariant.
- No commented-out code, no dead code, and no `TODO` without an issue link MAY
  be merged.

Rationale: This is a multi-project Azure solution (`Processor`, `WebApp`,
`Common`) with shared contracts; consistent structure and DI discipline are the
only sustainable way to keep the Function host, Blazor app, and shared services
from drifting apart.

### II. Testing Standards (NON-NEGOTIABLE)

Behavioral changes MUST be accompanied by automated tests in the `tests/`
project, and the suite MUST be green on every PR.

- Every new model, service, or controller MUST have unit tests under
  `tests/Models/` or `tests/Services/` mirroring the source folder layout.
- External dependencies (Azure Blob Storage, Cosmos DB, Document Intelligence,
  Entra ID) MUST be abstracted behind interfaces and mocked in tests; tests
  MUST NOT call live Azure services.
- Bug fixes MUST first add a failing regression test that reproduces the bug,
  then make it pass (red → green).
- Tests MUST be deterministic: no real time, no real network, no random data
  without a fixed seed, no shared mutable state between tests.
- `dotnet test` MUST pass locally before requesting review; PRs that reduce the
  number of passing tests without explicit justification MUST be rejected.

Rationale: The processing pipeline (queue → OCR → aggregation → persistence)
has many failure modes that cannot be observed reliably in production; fast,
isolated tests at the service boundary are the only practical safety net for a
POC that is hardening toward production.

### III. User Experience Consistency

The Blazor `DocumentOcr.WebApp` and the Operations API MUST present a single,
predictable experience to reviewers and API consumers.

- All Blazor pages MUST use the shared layout, theme, and component library
  under `DocumentOcr.WebApp/Components`; ad-hoc inline styles are prohibited
  except for one-off layout fixes documented in the component.
- Confidence indicators, status badges, and review states MUST use the color
  and label vocabulary defined in `docs/REVIEW-PAGE-UX.md`. New states MUST
  update that document in the same PR.
- All HTTP endpoints in the Operations API MUST follow the asynchronous
  request-reply contract documented in `docs/OPERATIONS-API.md`, including
  consistent status values (`Running`, `Succeeded`, `Failed`, `Cancelled`),
  problem-details error responses, and `Location` / `Retry-After` headers
  where applicable.
- User-facing copy (errors, status messages, button labels) MUST be reviewed
  for clarity; raw exception messages MUST NOT be surfaced to end users.
- Keyboard navigation and screen-reader labels MUST be preserved on every
  change to review UI components.

Rationale: Reviewers correct OCR output under time pressure; inconsistent
controls or hidden state changes directly degrade data quality and trust in
the system.

### IV. Performance & Reliability

The system MUST process documents within predictable time budgets and MUST
fail safely when downstream services degrade.

- The Function pipeline MUST process a typical 10-page PDF end-to-end (queue
  dequeue → Cosmos write) in under 60 seconds at p95 under nominal load.
  Changes that regress this budget MUST be flagged in the PR description.
- Operations API endpoints (excluding the long-running start operation) MUST
  respond in under 500 ms at p95.
- All calls to Azure Document Intelligence, Blob Storage, and Cosmos DB MUST
  use the official SDK retry/backoff policies; bespoke retry loops are
  prohibited unless they wrap a documented gap.
- Errors from a single page or document MUST NOT abort the whole batch; the
  pipeline MUST log the failure, mark the affected unit, and continue.
- Operation status in Cosmos DB MUST be updated on every state transition so
  that the UI and API never report stale `Running` states for completed work.
- New external calls MUST be wrapped with structured logging
  (`ILogger<T>`) including operation ID, blob name, and duration.

Rationale: This is a queue-driven system whose throughput and reviewer trust
both depend on bounded latency and on every operation reaching a terminal
state; silent stalls are the highest-impact defect class.

### V. Security & Secure-by-Default

The solution MUST remain keyless-by-default and MUST minimize the blast radius
of any compromised component.

- Authentication to Azure Storage, Cosmos DB, and Document Intelligence MUST
  use Managed Identity (`DefaultAzureCredential`) in deployed environments.
  Connection strings and account keys MUST NOT be committed and MUST NOT be
  required for production deployment.
- `local.settings.json` and `appsettings.Development.json` MUST remain
  gitignored; only their `.template` counterparts may be committed, and they
  MUST contain placeholder values only.
- The Operations API and WebApp MUST authenticate users via Microsoft Entra ID;
  anonymous endpoints MUST be explicitly justified and limited to health
  probes.
- Authorization checks MUST be performed server-side for every state-changing
  endpoint; UI-only hiding of controls is not sufficient.
- All user-supplied input (uploaded PDFs, query parameters, form fields) MUST
  be validated for type, size, and content before being passed to Azure
  services or rendered back to the UI.
- Dependencies MUST be kept current; any package with a known high or critical
  CVE MUST be upgraded or replaced before release. New dependencies MUST be
  reviewed for license and maintenance status.
- Secrets discovered in code, logs, or telemetry MUST be rotated immediately
  and the incident recorded in the repository's security notes.

Rationale: The system handles potentially sensitive document content and is
deployed to customer Azure subscriptions; keyless auth, least privilege, and
strict input validation are the cheapest insurance against the most common
classes of cloud breach.

## Engineering Constraints & Technology Standards

- Runtime: .NET 8.0 SDK; Functions host MUST remain `dotnet-isolated` on
  Functions v4. Upgrades require a constitution amendment.
- Project layout MUST remain: `src/DocumentOcr.Common` (shared models and
  interfaces), `src/DocumentOcr.Processor` (Functions host), and
  `src/DocumentOcr.WebApp` (Blazor Server). New deployable units require an
  amendment.
- Infrastructure changes MUST be expressed as Bicep under `infra/`, MUST use
  Azure Verified Modules where available, and MUST be deployable via `azd up`
  end-to-end.
- Documentation under `docs/` MUST be updated in the same PR as any change to
  architecture, deployment, API contracts, or UX vocabulary.
- New configuration settings MUST be added to the appropriate
  `*.template` file with a placeholder value and described in the relevant
  doc.

## Development Workflow & Quality Gates

- Work happens on feature branches; direct commits to `main` are prohibited
  except for automated release tooling.
- Every PR MUST: (a) build cleanly, (b) pass `dotnet test`, (c) update or add
  tests for behavioral changes, (d) update docs and `.template` files when
  applicable, and (e) include a brief Constitution Check summarizing how the
  change satisfies principles I–V.
- Plans generated by `/speckit.plan` MUST run the Constitution Check gate
  using principles I–V before Phase 0 research and again after Phase 1 design.
  Violations MUST be recorded in the plan's Complexity Tracking table with
  justification, or the plan MUST be revised.
- Reviewers MUST block merges that violate any NON-NEGOTIABLE principle
  (currently II) regardless of urgency; exceptions require an explicit,
  time-bounded waiver recorded in the PR.
- Performance- or security-sensitive changes (touching the OCR pipeline, auth,
  or external SDK calls) MUST be reviewed by at least one additional engineer.

## Governance

This constitution supersedes ad-hoc conventions and prior informal practices.
When this document conflicts with any other guidance in the repository, this
document wins; the conflicting guidance MUST be updated in the same PR.

- Amendments MUST be proposed as a PR that edits this file, includes the
  updated Sync Impact Report comment, bumps the version per semantic
  versioning, and updates the **Last Amended** date.
- Versioning policy:
  - MAJOR: Backward-incompatible removal or redefinition of a principle or
    governance rule.
  - MINOR: New principle or materially expanded mandatory guidance.
  - PATCH: Clarifications, wording, or non-semantic refinements.
- Compliance reviews: Maintainers MUST review open PRs for constitution
  compliance and MAY request changes solely on those grounds. A lightweight
  audit of `main` SHOULD be performed at least once per release cycle to
  confirm principles I–V remain satisfied in shipped code.
- Runtime development guidance for AI coding agents lives in
  `.github/copilot-instructions.md` and `AGENTS.md` (when present); those
  files MUST be kept consistent with this constitution.

**Version**: 1.0.0 | **Ratified**: 2026-05-01 | **Last Amended**: 2026-05-01
