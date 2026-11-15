# TourEd Project Context

## Purpose

TourEd is a small .NET 10 application for stamping points and hiking tours from multiple providers.

It stores Touringen, Harzer Wandernadel (HWN), Malerweg (MW), Schluchtensteig (SST), Heidschnuckenweg (HNW), Harzer Klosterwanderweg (HKW), Bliessteig (BS), and Kellerwaldsteig (KWS) stamping points, hiking tours, tour-to-point relationships, users, and user visits in a SQLite database. The main user-facing feature is showing stamping points on a map and distinguishing visited from unvisited points for a known user.

Stamping points are anchored by `StampingProvider` and belong to a provider-scoped `StampingSeries`. A series supplies the number namespace, so equally numbered points from different editions remain distinct. Per-user `UserStampingProvider` entitlements determine which providers an authenticated account may access. Existing users received every provider present when the entitlement migration ran; new users and providers receive no automatic grants. `User.DefaultStampingProviderId` is nullable and is used only when it references an entitled provider.

## Reference documents

This file holds what applies to every change. The details live in `docs/` and must be read before touching the matching area, and updated in the same change when that behavior changes:

| Area | Read first |
|---|---|
| Bundled map, PWA, service worker, offline snapshot and sync queue, legal pages | `docs/frontend.md` |
| Layering, repositories and managers, transactions, error handling, authentication, registration, audit, runtime configuration | `docs/architecture.md` |
| Touringen, Harzer Wandernadel, seeded trail providers, CSV visit import | `docs/data-import.md` |
| Endpoints, query behavior, DTO and visit-state semantics | `docs/api.md` |
| Server setup, release workflow, secrets, admin `curl` calls | `docs/deployment.md` |

## Rules that always apply

Frontend:

- Plain HTML, CSS and vanilla JavaScript (native ES modules, OpenLayers vendored locally) under `Api/wwwroot`; no bundler, no build step, no separate frontend project, no external library host.
- Every imported module must be listed in the service worker's precache list (enforced by the contract test `EveryImportedFrontendModuleIsPrecachedByTheServiceWorker`). Increment the service-worker cache version whenever a cached asset changes.
- The page starts fail-closed and sends no provider or point request without a confirmed session. It never reads or writes user ids, Google subjects, tokens, identity headers, local storage or session storage.
- Keep the privacy notice (`Api/wwwroot/datenschutz/index.html`) synchronized whenever data flows or retention rules change. Never publish placeholder operator details.

Backend:

- Controllers handle HTTP shape only. Managers hold rules, validation, audit decisions and unit-of-work boundaries and map to DTOs. Repositories contain only EF Core queries and persistence, return entities or their own read models, and never depend on `Api.Managers` or `Api.Dto`.
- Controllers pass the acting user explicitly; managers and repositories do not read the HTTP context, and `IHttpContextAccessor` is not registered. Every asynchronous path accepts and forwards a `CancellationToken`.
- Transactions use one pattern: `await using var unitOfWork = await unitOfWorkFactory.BeginAsync(cancellationToken);` then `await unitOfWork.CommitAsync(cancellationToken);`. Never call `Database.BeginTransaction*` directly or open transactions in constructors.
- Expected failures are domain exceptions from `Api.ErrorHandling`, translated centrally (`400`/`403`/`404`/`409`). Controllers do not catch them; framework exceptions are never mapped and stay `500`.
- Entities are configured only through their nested fluent `Configuration` class, never with mapping attributes. Changing entities or seeds requires a migration; pure restructurings must leave `dotnet ef migrations has-pending-model-changes --project Api` clean. Provider and series slugs are stored lowercase.
- Interfaces exist only where tests substitute implementations.

Identity and data:

- Browser requests authenticate only through the `toured-session` cookie; admin and import routes only through the CLI bearer token. Headers and query parameters never establish an identity. Protected endpoints answer `401`/`403`, never a redirect or HTML. CORS stays disabled. Do not introduce ASP.NET Identity or local passwords.
- Catalogs, points, tours, visits, search data and GeoJSON are always restricted by the user's provider entitlements; there is no fallback provider. Removing an entitlement hides visits without deleting them.
- Imports and point upserts update in place and keep internal ids and user visits. Imports validate completely before writing and persist in one transaction.
- Unknown Google logins never create users; they create a registration request. `GoogleLoginService` never creates users directly.
- EF Core sensitive data logging stays disabled. Audit entries, logs and notification mails never contain tokens, email addresses or Google subjects.

Operations:

- Administration is terminal/API driven (`curl` or the separate `TourEd.Admin` client over HTTPS); no browser admin UI is expected or desired.
- Deployment is manual through the GitHub workflow from `master` and is never triggered without the owner's explicit confirmation of that run.

## Intended Usage

Normal user flow:

1. User opens the HTML map served by the TourEd application.
2. Browser checks the TourEd session and offers Google login or logout as appropriate.
3. Browser loads the complete provider catalog with the authenticated user's entitlement and progress aggregates, then requests enabled-provider visited and unvisited points separately.
4. Backend returns only entitled points, provider info, optional tour summaries, and user visit state.
5. Map renders markers and shows a responsive detail dialog on selection, with optional pointer hover on suitable desktop devices.

Maintenance/admin flow:

1. Admin uses `curl` or the separately maintained local `TourEd.Admin` terminal client.
2. The client calls only the CLI-protected HTTPS admin endpoints; it never accesses the database directly.
3. No browser-based admin UI is expected or desired for normal use.

Admin/import operations are intentionally terminal/API driven.

## Solution Structure

The solution has two projects (the former `Toured.Lib` was merged into `Api` on 30 September 2026):

- `Api`
  - ASP.NET Core REST API; namespaces follow the folders.
  - `Controllers`, `Managers`, `Repositories` (including the unit of work), `Dto`, `Migrations`.
  - `Entities`: EF Core entity types (`User`, `StampingPoint`, `StampingProvider`, `StampingSeries`, `UserVisit`, …) with their nested EF configuration; seed data in `Entities/Seeds`.
  - `Imports`: Touringen/HWN source import services, HTML parsing, raw import models and their JSON converters.
  - `Authentication`: cookie/CLI/Google authentication, `GoogleLoginService`, claim names and `ClaimsPrincipal` extensions.
  - `ErrorHandling`: domain exceptions and the central exception handler.
  - `Options`, `Services` (hosted background services), `Extensions` (service registration).
  - Static HTML map and image assets under `wwwroot`.
- `TourEd.Tests`
  - xUnit test project.
  - Covers provider-aware persistence/import behavior, readiness, Google account binding, and backend authentication integration.

## Development Notes

The frontend intentionally remains a plain static page under `Api/wwwroot`; there is no separate frontend project or build process.

TourEd source code is licensed under `AGPL-3.0-only`. Separate commercial licenses are available from the copyright holder for organizations that do not want to comply with the AGPL. The AGPL does not grant trademark rights in the TourEd name or branding. Third-party software and provider data retain their own licenses; imported OpenStreetMap data remains subject to ODbL 1.0 where identified.

The API uses:

- .NET 10
- ASP.NET Core
- EF Core
- SQLite
- Swagger in development

Do not introduce ASP.NET Identity or local-password dependencies.

The configured database connection is:

- `Data Source=toured.db`

Browser regression tests for concurrent snapshot initialization are documented in `TourEd.Tests/Browser/README.md`; they use two real Chromium tabs sharing one browser context and IndexedDB.

Browser regressions for delayed synchronization confirmations and lease takeover are documented in `TourEd.Tests/Browser/sync-confirmations.md`. They use real Chromium tabs with shared IndexedDB and controlled HTTP responses.

PWA release-coherence browser regressions are documented in `TourEd.Tests/Browser/app-shell-updates.md`. They exercise real service-worker installation, waiting/activation, offline navigation and OAuth exclusions at both `/` and `/toured/`.

Current verification baseline:

- `dotnet build TourEd.slnx --no-restore` succeeds after a fresh restore with the .NET 10 SDK.
- `dotnet test --no-restore` runs provider-aware persistence/import, readiness, Google account-binding, browser-session/mobile-visit/frontend-contract, and CLI-authentication integration tests after a fresh restore with the .NET 10 SDK.

## Deployment

Production deployment is manual through `.github/workflows/deploy.yml` and only accepts runs from `master`.

- GitHub `production` environment variables configure the SSH target, deployment account/home, public URL, and Linux runtime architecture.
- Root-owned `/etc/toured-deploy.conf` configures runtime accounts, application/database/backup paths, service, .NET/listen settings, the public `/health` readiness URL, and retention.
- The same deployment configuration points to a root-only runtime environment file and a persistent Data-Protection key directory; setup validates and installs their systemd integration before deployment.
- `deploy/server/toured-deploy.conf.example` records the current production values without embedding them in deployment logic.
- `deploy/server/toured-api.service.template` is rendered by the server setup from that configuration.

The workflow builds and tests the solution, publishes the API together with the bundled frontend, creates the configured Linux EF migration bundle, and uploads a checksummed release. The root-owned server command stops the service, backs up the application and SQLite database, applies migrations as the configured runtime user, restarts the service, waits for `/health`, and checks `/auth/session` and `index.html` once as smoke tests. It restores both application and database if deployment, readiness, or smoke checking fails.

Server bootstrap and operational details are documented in `docs/deployment.md`. The GitHub secrets are `TOURED_DEPLOY_SSH_PRIVATE_KEY` and `TOURED_DEPLOY_KNOWN_HOSTS`.

Line endings:

- Keep repository text files on CRLF line endings.
- `.gitattributes` declares CRLF for common project text files so Git does not emit LF-to-CRLF replacement warnings during diffs/status operations.
- Executable and shell-sourced files under `deploy/server` are the exception and use LF for Linux execution.
- When adding new tracked text file types, update `.gitattributes` if Git starts warning about line-ending normalization.

## Working Preferences

Keep the frontend in `Api/wwwroot` and deploy it together with the API unless explicitly asked to introduce a separate frontend project or deployment.

Do not assume an admin UI is missing; admin workflows are intentionally handled via `curl`.

When changing API contracts, consider the bundled HTML page as the primary consumer, especially the shape of `GET api/points` responses and cookie-session behavior.

Prefer small, pragmatic changes over introducing large framework or frontend build structures.

## Agent Maintenance Rule

When an agent changes project behavior, architecture, API contracts, data flow, operational workflows, frontend assumptions, or development/testing conventions, it must update this `AGENTS.md` file or the matching reference document under `docs/` in the same change. Rules that apply to every change belong here; descriptions of how something works belong in `docs/`. Keep this file short.

Keep updates concise and factual. Do not rewrite the whole file unless the project shape changed substantially.

Examples of changes that require updating this file:

- New or changed API endpoint behavior.
- Changed response shapes consumed by the bundled HTML map.
- Changed authentication/header behavior.
- New persistence model, migration pattern, or database dependency.
- New frontend location or changed frontend assumptions.
- Changed admin/import workflow.
- Changed build, test, deployment, or verification baseline.

Examples of changes that usually do not require updating this file:

- Internal refactoring with no observable behavior or architecture change.
- Bug fixes that restore documented behavior.
- Formatting-only changes.
- Adding tests without changing project conventions.

OpenLayers is vendored from a pinned official upstream commit with provenance and license in `Api/wwwroot/vendor/openlayers/5.3.0`. No frontend build or external library host is required. `TourEd.Tests/Browser/local-openlayers.cjs` checks fresh app-shell installation with foreign hosts blocked, including root and PathBase hosting, anonymous access, authenticated map rendering and offline reload.
