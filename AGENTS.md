# TourEd Project Context

## Purpose

TourEd is a small .NET 10 application for Touringen stamping points and hiking tours.

It stores Touringen stamping points, hiking tours, tour-to-point relationships, users, and user visits in a SQLite database. The main user-facing feature is showing stamping points on a map and distinguishing visited from unvisited points for a known user.

Stamping points and future provider-specific data are anchored by `StampingProvider`. The initial provider is `touringen`, and users store a `DefaultStampingProviderId` that currently defaults to Touringen.

## Frontend

The repository contains one user-facing frontend at `Api/wwwroot/index.html`. It is part of the ASP.NET Core application and is published and deployed together with the API.

It is a plain HTML page using jQuery and OpenLayers. It loads OpenStreetMap tiles and displays stamping points as map markers.

The page calls the backend with relative URLs:

- `GET api/points`
  - Used when no user id is present.
  - Uses the anonymous default provider, currently `touringen`.
  - Treats all returned points as unvisited.
- `GET api/points?vis=false`
  - Used when `?userid=...` is present in the page URL.
  - Sends header `toured-user`.
  - Returns unvisited points for that user.
- `GET api/points?vis=true`
  - Used when `?userid=...` is present in the page URL.
  - Sends header `toured-user`.
  - Returns visited points for that user.

Optional `provider` query behavior on `GET api/points`:

- Omitted: uses the authenticated user's default provider, or `touringen` for anonymous requests.
- Provider slug, for example `provider=touringen`: returns points from that provider.
- `provider=all`: returns points from all providers.

The map uses the red and green pin image assets stored in `Api/wwwroot/img`:

- `img/pin_icon_red.png`
- `img/pin_icon_green.png`

The current static map does not use the tours endpoint or admin/import endpoints.

## Intended Usage

Normal user flow:

1. User opens the HTML map served by the TourEd application.
2. Browser requests stamping points from the backend.
3. Backend returns points, provider info, optional tour summaries, and user visit state depending on query/header.
4. Map renders markers and shows a small info card on hover/click.

Maintenance/admin flow:

1. Admin uses `curl` or similar tooling.
2. Admin calls import endpoints directly.
3. No admin UI is expected or desired for normal use.

Admin/import operations are intentionally terminal/API driven.

## Solution Structure

The solution has three projects:

- `Api`
  - ASP.NET Core REST API.
  - Static HTML map and image assets under `wwwroot`.
  - Controllers for points, tours, and imports.
  - EF Core SQLite persistence.
  - Repository and manager classes.
  - Database migrations.
  - DTOs for HTTP responses.
- `Toured.Lib`
  - Domain and raw import models.
  - Shared abstractions and interfaces.
  - Import services.
  - HTML parsing service.
  - Header-based authentication handler.
  - Utility extensions and JSON converters.
- `TourEd.Tests`
  - xUnit test project.
  - Covers provider-aware stamping point persistence and import behavior.

## Architecture

The backend follows a simple layered structure:

- Controllers handle HTTP shape and routing.
- Managers contain application-level orchestration.
- `TouredRepository` contains EF Core queries and persistence operations.
- `DataContext` defines SQLite-backed EF Core mappings.
- `Toured.Lib` contains reusable domain/import/auth pieces used by the API.

Provider data is represented by `StampingProvider`. Existing users and newly created users default to the Touringen provider through `User.DefaultStampingProviderId`.

Users can optionally store a unique Google subject identifier. `GoogleLoginService` resolves an existing binding by subject or atomically binds the first verified Google login to an existing user by normalized email. It never creates users, and no Google login endpoint is exposed yet.

The main runtime composition happens in `Api/Program.cs`.

`Api/Program.cs` enables default and static files, so `Api/wwwroot/index.html` and its assets are served by the same application as the API.

Authentication is custom and header-based:

- Header name: `TouredUser` / `toured-user`
- The header value is provided by the bundled static map when a user id is present.
- The authentication handler looks up the user and creates claims for user id and email.

## Data Import

Touringen data import:

- Fetches `https://www.touringen.de/stempelstellen`.
- Extracts an embedded JavaScript `dmos` JSON string.
- Deserializes raw areas, tours, and stamp points.
- Normalizes multiple Touringen source ids for the same provider-scoped stamping point number to one database-generated internal id.
- Stores the highest source id as the point's current external id and maps every source alias in the import payload to the normalized point.
- Maps all hiking-tour relationships for those source aliases to the normalized stamping point id.
- Records import metadata.

User data import:

- Accepts uploaded CSV-like data.
- Parses stamping point numbers and optional visit timestamps.
- Maps numbers only to stored stamping points from the authenticated user's default provider.
- Creates user visit records for the authenticated user.

## Important API Context

Main consumer endpoint:

- `GET /api/points`

Useful query behavior:

- `provider=<slug>` returns points for a specific stamping provider.
- `provider=all` returns points for all stamping providers.
- `vis=true` returns visited points for the authenticated user.
- `vis=false` returns unvisited points for the authenticated user.
- Geo filtering exists via query parameters and is used server-side.

Point DTOs include provider info while preserving the existing number, name, position, visited, and tours fields.

Other endpoints:

- `GET /health`
  - Anonymous ASP.NET Core readiness endpoint.
  - Returns healthy only when no EF Core migrations are pending and the seeded Touringen provider exists.
- `GET /api/tours`
  - Exists for hiking tour queries.
  - Not currently used by the bundled HTML map.
- `POST /api/admin/imports/touringen`
  - Imports Touringen source data.
  - Intended for manual/admin use.
- `POST /api/admin/imports`
  - Imports user visit data.
  - Intended for manual/admin use.

## Development Notes

The frontend intentionally remains a plain static page under `Api/wwwroot`; there is no separate frontend project or build process.

The API uses:

- .NET 10
- ASP.NET Core
- EF Core
- SQLite
- Swagger in development

`Toured.Lib` uses the shared `Microsoft.AspNetCore.App` framework for authentication types. Do not reintroduce the obsolete `Microsoft.AspNetCore.Identity` 2.2 package dependency.

The configured database connection is:

- `Data Source=toured.db`

Current verification baseline:

- `dotnet build TourEd.sln --no-restore` succeeds after a fresh restore with the .NET 10 SDK.
- `dotnet test --no-restore` runs provider-aware persistence, import, and readiness health-check tests after a fresh restore with the .NET 10 SDK.

## Deployment

Production deployment is manual through `.github/workflows/deploy.yml` and only accepts runs from `master`.

- GitHub `production` environment variables configure the SSH target, deployment account/home, public URL, and Linux runtime architecture.
- Root-owned `/etc/toured-deploy.conf` configures runtime accounts, application/database/backup paths, service, .NET/listen settings, the public `/health` readiness URL, and retention.
- `deploy/server/toured-deploy.conf.example` records the current production values without embedding them in deployment logic.
- `deploy/server/toured-api.service.template` is rendered by the server setup from that configuration.

The workflow builds and tests the solution, publishes the API together with the bundled frontend, creates the configured Linux EF migration bundle, and uploads a checksummed release. The root-owned server command stops the service, backs up the application and SQLite database, applies migrations as the configured runtime user, restarts the service, waits for `/health`, and checks `/api/points` and `index.html` once as smoke tests. It restores both application and database if deployment, readiness, or smoke checking fails.

Server bootstrap and operational details are documented in `docs/deployment.md`. The GitHub secrets are `TOURED_DEPLOY_SSH_PRIVATE_KEY` and `TOURED_DEPLOY_KNOWN_HOSTS`.

Line endings:

- Keep repository text files on CRLF line endings.
- `.gitattributes` declares CRLF for common project text files so Git does not emit LF-to-CRLF replacement warnings during diffs/status operations.
- Executable and shell-sourced files under `deploy/server` are the exception and use LF for Linux execution.
- When adding new tracked text file types, update `.gitattributes` if Git starts warning about line-ending normalization.

## Working Preferences

Keep the frontend in `Api/wwwroot` and deploy it together with the API unless explicitly asked to introduce a separate frontend project or deployment.

Do not assume an admin UI is missing; admin workflows are intentionally handled via `curl`.

When changing API contracts, consider the bundled HTML page as the primary consumer, especially the shape of `GET api/points` responses and the `toured-user` header behavior.

Prefer small, pragmatic changes over introducing large framework or frontend build structures.

## Agent Maintenance Rule

When an agent changes project behavior, architecture, API contracts, data flow, operational workflows, frontend assumptions, or development/testing conventions, it must update this `AGENTS.md` file in the same change.

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
