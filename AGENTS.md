# TourEd Project Context

## Purpose

TourEd is a small .NET 10 backend for Touringen stamping points and hiking tours.

It stores Touringen stamping points, hiking tours, tour-to-point relationships, users, and user visits in a SQLite database. The main user-facing feature is showing stamping points on a map and distinguishing visited from unvisited points for a known user.

Stamping points and future provider-specific data are anchored by `StampingProvider`. The initial provider is `touringen`, and users store a `DefaultStampingProviderId` that currently defaults to Touringen.

## External Consumer

The user-facing consumer is intentionally not part of this repository.

Current static map:
https://baelgun.de/toured/index.html

It is a plain HTML page using jQuery and OpenLayers. It loads OpenStreetMap tiles and displays stamping points as map markers.

The page calls the backend with relative URLs:

- `GET api/points`
  - Used when no user id is present.
  - Treats all returned points as unvisited.
- `GET api/points?vis=false`
  - Used when `?userid=...` is present in the page URL.
  - Sends header `toured-user`.
  - Returns unvisited points for that user.
- `GET api/points?vis=true`
  - Used when `?userid=...` is present in the page URL.
  - Sends header `toured-user`.
  - Returns visited points for that user.

The map expects red and green pin image assets outside this repo, currently:

- `img/pin_icon_red.png`
- `img/pin_icon_green.png`

The current static map does not use the tours endpoint or admin/import endpoints.

## Intended Usage

Normal user flow:

1. User opens the external HTML map.
2. Browser requests stamping points from the backend.
3. Backend returns points, optional tour summaries, and user visit state depending on query/header.
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
  - Currently minimal; test discovery finds no actual tests.

## Architecture

The backend follows a simple layered structure:

- Controllers handle HTTP shape and routing.
- Managers contain application-level orchestration.
- `TouredRepository` contains EF Core queries and persistence operations.
- `DataContext` defines SQLite-backed EF Core mappings.
- `Toured.Lib` contains reusable domain/import/auth pieces used by the API.

Provider data is represented by `StampingProvider`. Existing users and newly created users default to the Touringen provider through `User.DefaultStampingProviderId`.

The main runtime composition happens in `Api/Program.cs`.

Authentication is custom and header-based:

- Header name: `TouredUser` / `toured-user`
- The header value is provided by the external static map when a user id is present.
- The authentication handler looks up the user and creates claims for user id and email.

## Data Import

Touringen data import:

- Fetches `https://www.touringen.de/stempelstellen`.
- Extracts an embedded JavaScript `dmos` JSON string.
- Deserializes raw areas, tours, and stamp points.
- Saves normalized stamping points and hiking tours.
- Records import metadata.

User data import:

- Accepts uploaded CSV-like data.
- Parses stamping point numbers and optional visit timestamps.
- Maps numbers to stored stamping points.
- Creates user visit records for the authenticated user.

## Important API Context

Main consumer endpoint:

- `GET /api/points`

Useful query behavior:

- `vis=true` returns visited points for the authenticated user.
- `vis=false` returns unvisited points for the authenticated user.
- Geo filtering exists via query parameters and is used server-side.

Other endpoints:

- `GET /api/tours`
  - Exists for hiking tour queries.
  - Not currently used by the external HTML map.
- `POST /api/admin/imports/touringen`
  - Imports Touringen source data.
  - Intended for manual/admin use.
- `POST /api/admin/imports`
  - Imports user visit data.
  - Intended for manual/admin use.

## Development Notes

The repository currently has no frontend project and no static HTML consumer. This is intentional.

The API uses:

- .NET 10
- ASP.NET Core
- EF Core
- SQLite
- Swagger in development

The configured database connection is:

- `Data Source=toured.db`

Current verification baseline:

- `dotnet build TourEd.sln --no-restore` succeeds after a fresh restore with the .NET 10 SDK.
- `dotnet test --no-restore` runs after a fresh restore with the .NET 10 SDK.

## Working Preferences

When changing the project, preserve the split between backend and external static map unless explicitly asked otherwise.

Do not assume an admin UI is missing; admin workflows are intentionally handled via `curl`.

When changing API contracts, consider the external HTML page as the primary consumer, especially the shape of `GET api/points` responses and the `toured-user` header behavior.

Prefer small, pragmatic backend changes over introducing large framework or frontend structure.

## Agent Maintenance Rule

When an agent changes project behavior, architecture, API contracts, data flow, operational workflows, external consumer assumptions, or development/testing conventions, it must update this `AGENTS.md` file in the same change.

Keep updates concise and factual. Do not rewrite the whole file unless the project shape changed substantially.

Examples of changes that require updating this file:

- New or changed API endpoint behavior.
- Changed response shapes consumed by the external HTML map.
- Changed authentication/header behavior.
- New persistence model, migration pattern, or database dependency.
- New frontend/static consumer location or changed consumer assumptions.
- Changed admin/import workflow.
- Changed build, test, deployment, or verification baseline.

Examples of changes that usually do not require updating this file:

- Internal refactoring with no observable behavior or architecture change.
- Bug fixes that restore documented behavior.
- Formatting-only changes.
- Adding tests without changing project conventions.
