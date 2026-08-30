# TourEd Project Context

## Purpose

TourEd is a small .NET 10 application for Touringen stamping points and hiking tours.

It stores Touringen stamping points, hiking tours, tour-to-point relationships, users, and user visits in a SQLite database. The main user-facing feature is showing stamping points on a map and distinguishing visited from unvisited points for a known user.

Stamping points and future provider-specific data are anchored by `StampingProvider`. The initial provider is `touringen`, and users store a `DefaultStampingProviderId` that currently defaults to Touringen.

## Frontend

The repository contains one user-facing frontend rooted at `Api/wwwroot/index.html`, with its local CSS and JavaScript in `Api/wwwroot/css/toured.css` and `Api/wwwroot/js/toured.js`. It is part of the ASP.NET Core application and is published and deployed together with the API.

It is a plain HTML, CSS, and vanilla-JavaScript application using OpenLayers. It has no frontend build process. It loads OpenStreetMap tiles and displays stamping points as map markers.

The frontend is mobile-first and fluid. On touch-oriented or narrow displays, stamping-point details appear as a bottom sheet; fine-pointer desktop displays position the same accessible dialog near the selected marker. Selection works by click or touch, the dialog has an explicit close button and Escape handling, interactive controls have touch-sized targets, safe-area insets are respected, and visited markers add a check symbol so visit state is not conveyed by color alone. Pointer hover remains an optional desktop enhancement. A shared top-right control bar contains mutually exclusive provider-filter and account flyouts; their text labels collapse visually on narrow displays while accessible names remain. Provider information opens in a keyboard-accessible modal with a validated optional external link and an independence disclaimer. The TourEd logo appears as its compact round signet on narrow displays and as the full wordmark on desktop displays.

The page calls the backend with relative URLs:

- `GET auth/session`
  - Loaded first to determine whether the browser has a valid TourEd session.
- `GET auth/login`
  - Used by the “Mit Google anmelden” link to start the Google challenge.
- `POST auth/logout`
  - Ends the TourEd cookie session and returns the map to anonymous mode.
- `GET api/providers`
  - Loads the integrated provider catalog before point data.
  - All returned provider slugs are selected internally by default.
- `GET api/points?provider=all`
  - Used when the session is anonymous.
  - Loads points from every integrated provider into the in-memory frontend cache.
  - Renders all returned points with a neutral marker because personal visit state is unavailable.
- `GET api/points?provider=all&vis=false`
  - Used when `auth/session` reports an authenticated cookie session.
  - Loads unvisited points from every integrated provider into the in-memory frontend cache.
- `GET api/points?provider=all&vis=true`
  - Used when `auth/session` reports an authenticated cookie session.
  - Loads visited points from every integrated provider into the in-memory frontend cache.

The frontend keeps provider metadata and point arrays only in memory. Marker rendering is centralized and filters the cached arrays against the currently selected provider slugs; changing the checkbox selection in the provider flyout does not require another point request. All providers are selected after each page initialization, `Alle` and `Keine` provide bulk selection, and an empty selection renders zero points. Provider names are also shown in point details because point numbers are provider-scoped. Login, logout, and reinitialization reload the catalog and point caches, and stale initialization responses are ignored.

The public privacy notice is served at `Api/wwwroot/datenschutz/index.html` and linked permanently from the map. It is available without authentication and carries a `noindex` directive to reduce search-engine discoverability. The notice documents the current Google login, cookies, account/visit storage, hosting logs, OpenStreetMap tiles, external OpenLayers CDN, and the user-initiated navigation to external provider websites. Keep it synchronized whenever these data flows or their retention rules change.

The frontend never reads or writes user ids, Google subjects, tokens, custom identity headers, local storage, or session storage. A `401` while loading authenticated point data returns the UI to anonymous mode without starting a login redirect.

Optional `provider` query behavior on `GET api/points`:

- Omitted: uses the authenticated user's default provider, or `touringen` for anonymous requests.
- Provider slug, for example `provider=touringen`: returns points from that provider.
- `provider=all`: returns points from all providers.

The map uses logo-colored SVG pin assets stored in `Api/wwwroot/img`:

- `img/pin_icon_neutral.svg`
- `img/pin_icon_visited.svg`
- `img/toured-logo-transparent.svg`

The anonymous map hides the visit-state legend. Anonymous and authenticated open points use the logo's light blue; authenticated visited points use its dark blue and carry a white check matching the logo. The bundled map omits OpenLayers' on-map zoom buttons while retaining its touch, mouse, and keyboard zoom interactions.

The current static map does not use the tours endpoint or admin/import endpoints.

## Intended Usage

Normal user flow:

1. User opens the HTML map served by the TourEd application.
2. Browser checks the TourEd session and offers Google login or logout as appropriate.
3. Browser loads the provider catalog and requests all-provider anonymous points once, or all-provider visited and unvisited points separately for an authenticated cookie session.
4. Backend returns points, provider info, optional tour summaries, and user visit state depending on the session.
5. Map renders markers and shows a responsive detail dialog on selection, with optional pointer hover on suitable desktop devices.

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
  - Utility extensions and JSON converters.
- `TourEd.Tests`
  - xUnit test project.
  - Covers provider-aware persistence/import behavior, readiness, Google account binding, and backend authentication integration.

## Architecture

The backend follows a simple layered structure:

- Controllers handle HTTP shape and routing.
- Managers contain application-level orchestration.
- `TouredRepository` contains EF Core queries and persistence operations.
- `DataContext` defines SQLite-backed EF Core mappings.
- `Toured.Lib` contains reusable domain/import/auth pieces used by the API.

Provider data is represented by `StampingProvider`. Existing users and newly created users default to the Touringen provider through `User.DefaultStampingProviderId`.

`StampingProvider` also stores the public name, description, and optional website used by the provider catalog. Public provider DTOs expose only absolute HTTP(S) website URLs; unsupported URI schemes are omitted.

Users can optionally store a unique Google subject identifier. `GoogleLoginService` resolves an existing binding by subject or atomically binds the first verified Google login to an existing user by normalized email. It never creates users.

The main runtime composition happens in `Api/Program.cs`.

`Api/Program.cs` enables default and static files, so `Api/wwwroot/index.html` and its assets are served by the same application as the API.

Authentication is scheme-separated:

- Browser requests authenticate only through the encrypted `toured-session` cookie, which is the default scheme, is `Secure`, `HttpOnly`, `SameSite=Lax`, expires after eight hours, and uses sliding expiration.
- Google is used only by the explicit `/auth/login` challenge. Its callback binds through `GoogleLoginService`, discards Google claims/tokens, and stores only internal user-id and email claims in the TourEd cookie.
- Import routes use the separate `TouredCliImport` policy and `TouredCliBearer` scheme. Only the configured bearer token can resolve the configured existing user; cookie identities do not satisfy this policy.
- Arbitrary request headers and URL query parameters never establish a browser identity.
- Protected API endpoints return `401`/`403` instead of redirecting to Google or returning HTML.
- Permissive CORS is disabled; browser authentication is intentionally Same-Origin.

Authentication endpoints:

- `GET /auth/login` starts the Google challenge.
- `GET /auth/session` returns anonymous/authenticated state and the authenticated email only.
- `POST /auth/logout` removes the TourEd session cookie.

Runtime configuration uses `Authentication__Google__ClientId`, `Authentication__Google__ClientSecret`, `Authentication__Cli__UserEmail`, `Authentication__Cli__Token`, `PathBase`, `DataProtection__KeysPath`, `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`, and `touringen__StempelstellenUri=https://www.touringen.de/stempelstellen`. Production values belong in the root-protected runtime environment file configured by `RUNTIME_ENV_FILE`, not in appsettings or the visible systemd unit. Server setup validates the required entries and creates the configured persistent Data-Protection directory outside the replaceable application release for the runtime user only. Kestrel must listen only on the trusted proxy host or private network when forwarded headers are globally enabled.

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

- Requires `Authorization: Bearer <token>` using the dedicated CLI configuration and runs as its configured existing TourEd user.
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
- Requests with `vis=true` or `vis=false` return `401` when no valid cookie identity with internal user claims is present.
- Geo filtering exists via query parameters and is used server-side.

Point DTOs include provider info while preserving the existing number, name, position, visited, and tours fields.

Other endpoints:

- `GET /api/providers`
  - Anonymous catalog of all integrated stamping providers.
  - Returns providers ordered by name and slug, including description and an optional validated public website URL.

- `GET /health`
  - Anonymous ASP.NET Core readiness endpoint.
  - Returns healthy only when no EF Core migrations are pending and the seeded Touringen provider exists.
- `GET /api/tours`
  - Exists for hiking tour queries.
  - Not currently used by the bundled HTML map.
- `POST /api/admin/imports/touringen`
  - Imports Touringen source data.
  - Requires the dedicated CLI bearer token and is intended for manual/admin use.
- `POST /api/admin/imports`
  - Imports user visit data.
  - Requires the dedicated CLI bearer token and is intended for manual/admin use.

## Development Notes

The frontend intentionally remains a plain static page under `Api/wwwroot`; there is no separate frontend project or build process.

The API uses:

- .NET 10
- ASP.NET Core
- EF Core
- SQLite
- Swagger in development

`Toured.Lib` has no ASP.NET Core framework dependency. Browser and CLI authentication handlers belong to `Api`; do not introduce ASP.NET Identity or local-password dependencies.

The configured database connection is:

- `Data Source=toured.db`

Current verification baseline:

- `dotnet build TourEd.sln --no-restore` succeeds after a fresh restore with the .NET 10 SDK.
- `dotnet test --no-restore` runs provider-aware persistence/import, readiness, Google account-binding, browser-session/frontend-contract, and CLI-authentication integration tests after a fresh restore with the .NET 10 SDK.

## Deployment

Production deployment is manual through `.github/workflows/deploy.yml` and only accepts runs from `master`.

- GitHub `production` environment variables configure the SSH target, deployment account/home, public URL, and Linux runtime architecture.
- Root-owned `/etc/toured-deploy.conf` configures runtime accounts, application/database/backup paths, service, .NET/listen settings, the public `/health` readiness URL, and retention.
- The same deployment configuration points to a root-only runtime environment file and a persistent Data-Protection key directory; setup validates and installs their systemd integration before deployment.
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

When changing API contracts, consider the bundled HTML page as the primary consumer, especially the shape of `GET api/points` responses and cookie-session behavior.

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
