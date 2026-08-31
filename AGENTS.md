# TourEd Project Context

## Purpose

TourEd is a small .NET 10 application for stamping points and hiking tours from multiple providers.

It stores Touringen and Harzer Wandernadel (HWN) stamping points, hiking tours, tour-to-point relationships, users, and user visits in a SQLite database. The main user-facing feature is showing stamping points on a map and distinguishing visited from unvisited points for a known user.

Stamping points are anchored by `StampingProvider` and belong to a provider-scoped `StampingSeries`. A series supplies the number namespace, so equally numbered points from different editions remain distinct. Each provider declares an abbreviation and whether its data is available anonymously. Touringen allows anonymous access. Harzer Wandernadel becomes anonymously available only after its first complete OSM import. Users store a `DefaultStampingProviderId` that currently defaults to Touringen.

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
  - Loads the providers available to the current anonymous or authenticated session before point data.
  - All returned provider slugs are selected internally by default.
- `GET api/points?provider=all`
  - Used when the session is anonymous.
  - Loads points only from providers that permit anonymous access into the in-memory frontend cache.
  - Renders all returned points with a neutral marker because personal visit state is unavailable.
- `GET api/points?provider=all&vis=false`
  - Used when `auth/session` reports an authenticated cookie session.
  - Loads unvisited points from every integrated provider into the in-memory frontend cache.
- `GET api/points?provider=all&vis=true`
  - Used when `auth/session` reports an authenticated cookie session.
  - Loads visited points from every integrated provider into the in-memory frontend cache.

The frontend keeps provider metadata and point arrays only in memory. Marker rendering is centralized and filters the cached arrays against the currently selected provider slugs; changing the checkbox selection in the provider flyout does not require another point request. All available providers are selected after each page initialization, `Alle` and `Keine` provide bulk selection, and an empty selection renders zero points. Provider names, abbreviations, and non-standard series names are used in point details because point numbers are series-scoped and may be absent for temporary stamps. Visit mutations use the stable internal point id together with the provider slug. Provider information also exposes recorded source/licence metadata and a public GeoJSON download when available. Login, logout, and reinitialization reload the catalog and point caches, and stale initialization responses are ignored.

Authenticated users can record a stamp directly from a locked point-detail dialog using large, square action tiles with dedicated icons and concise labels ('Jetzt stempeln', 'Nachtragen', 'Bearbeiten', 'Entfernen'), either with the current local date and time or with no timestamp, a date only, or a date plus time. Existing stamps allow editing only their optional date/time and can be removed after a point-specific confirmation. Every visit request includes the provider slug. Successful mutations update the cached point and marker layer without a full page reload; anonymous users see a login link instead of write controls.

A magnifier button to the left of the provider and account controls opens a mutually exclusive search flyout. A crosshair button to the left of the search button starts client-side geolocation only after it is pressed, centers the map on the user's current device location, and keeps the marker current while the page remains open. It renders a logo-colored blue marker with a dark blue outline and a soft accuracy area. TourEd does not transmit the coordinates to its backend or store them; centering still causes the normal OpenStreetMap tile requests for the displayed map area. The client searches only the already loaded points of currently selected providers, normalizing case and diacritics across point name, number, provider name, and abbreviation. Search results are capped, contain provider-scoped number labels, and selecting one centers and zooms the existing map before opening its locked point-detail dialog. Search terms and results are not persisted and require no additional backend endpoint.

The public privacy notice is served at `Api/wwwroot/datenschutz/index.html` and linked permanently from the map. It is available without authentication and carries a `noindex` directive to reduce search-engine discoverability. The notice documents the current Google login, cookies, account/visit storage, hosting logs, OpenStreetMap tiles, external OpenLayers CDN, and user-initiated navigation to external provider websites and the public GitHub source repository. Keep it synchronized whenever these data flows or their retention rules change.

The map attribution permanently links its compact `© TourEd` label to the public TourEd source repository, with an accessible AGPL-3.0 source-link label, next to the privacy link.

The frontend never reads or writes user ids, Google subjects, tokens, custom identity headers, local storage, or session storage. A `401` while loading authenticated point data returns the UI to anonymous mode without starting a login redirect.

Optional `provider` query behavior on `GET api/points`:

- Omitted: uses the authenticated user's default provider, or `touringen` for anonymous requests.
- Provider slug, for example `provider=touringen`: returns points from that provider if it is available to the current session; otherwise an anonymous request receives `401`.
- `provider=all`: returns points from every provider for authenticated users and only anonymously enabled providers for anonymous users.

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

Provider data is represented by `StampingProvider`; collections and editions are represented by `StampingSeries`. The seeded Touringen series are Standard, Naturschätze, Familienwanderwege Rhön, and the variable temporary Sonderstempel collection. HWN has one standard series. Numbered points are unique by series and number; unnumbered points retain identity through their provider-scoped external id. A point's provider and series are constrained to match. Existing users and newly created users default to the Touringen provider through `User.DefaultStampingProviderId`.

`StampingProvider` also stores the public name, abbreviation, description, optional website, anonymous-access flag, and optional imported-data provenance used by the provider catalog and GeoJSON export. Provenance includes source and licence links, attribution, source revision/timestamp, and import timestamp. Public provider DTOs expose only absolute HTTP(S) URLs; unsupported URI schemes are omitted. Anonymous catalogs and point queries omit providers whose anonymous-access flag is false.

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

Runtime configuration uses `Authentication__Google__ClientId`, `Authentication__Google__ClientSecret`, `Authentication__Cli__UserEmail`, `Authentication__Cli__Token`, `PathBase`, `DataProtection__KeysPath`, `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`, and `touringen__StempelstellenUri=https://www.touringen.de/stempelstellen`. Production values belong in the root-protected runtime environment file configured by `RUNTIME_ENV_FILE`, not in appsettings or the visible systemd unit. The non-secret HWN OSM relation id, API/public URLs, and size limit are configured under `harzerWandernadel` in appsettings. Server setup validates the required environment entries and creates the configured persistent Data-Protection directory outside the replaceable application release for the runtime user only. Kestrel must listen only on the trusted proxy host or private network when forwarded headers are globally enabled.

## Data Import

Touringen data import:

- Reads the 430 standard stamping points directly from OSM relation 14773147 (requiring exactly numbers 1 through 430 with valid name and coordinates), and downloads the official GPX archives for 8 Naturschätze and 13 Familienwanderwege Rhön points with complete, non-duplicated number ranges.
- Uses an explicit verified name-to-number correction map for Naturschätze because that archive omits its public numbers. Unknown names fail the import instead of being guessed.
- Fetches `https://www.touringen.de/stempelstellen`, extracts the embedded JavaScript `dmos` JSON string, and uses it only for hiking-tour relationships among standard points; the verified Naturschätze area ids 102 through 109 are explicitly excluded from that legacy relationship source.
- Updates points by series and number while retaining their database-generated internal ids and visits. The distinct series namespaces prevent special-edition points 1 through 8 from overwriting standard points 1 through 8; existing visits on standard points 1 through 8 are preserved when updating them to canonical standard data.
- Seeds/supports a variable temporary Sonderstempel series with optional point validity dates via the administrative upsert endpoint.
- Atomically records OSM provenance/licence metadata (attributing OpenStreetMap contributors under ODbL 1.0) and records the import metadata, enabling public GeoJSON export for Touringen.

Harzer Wandernadel data import:

- Reads the direct node members of OSM relation 148007 and uses OSM as the sole source for HWN point number, name, and coordinates.
- Accepts exactly the 222 regular numbered points HWN 1 through HWN 222; child relations, Sonderstempel, temporary points, and the winter alternative for HWN 69 are excluded.
- Updates points by series and number while retaining their internal ids and user visits.
- Atomically records OSM provenance/licence metadata, records the import, and enables anonymous HWN access only after the complete validated point update succeeds.
- Is started manually through the CLI-protected admin endpoint; no schedule or workflow triggers it automatically.

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

- `provider=<slug>` returns points for a specific stamping provider when it is available to the current session; anonymous access to a restricted provider returns 401.
- `provider=all` returns all providers for authenticated users and only anonymously enabled providers for anonymous users.
- `vis=true` returns visited points for the authenticated user.
- `vis=false` returns unvisited points for the authenticated user.
- Requests with `vis=true` or `vis=false` return `401` when no valid cookie identity with internal user claims is present.
- Geo filtering exists via query parameters and is used server-side.

Point DTOs include a stable internal id, provider and series metadata, optional number, name, position, explicit visit state, optional visit date/time, and tours.

Visit state is represented independently from its optional timestamp: `isVisited` reports whether a visit row exists, `visitedOn` is the optional date, and `visitedAt` is the optional time. A time requires a date. The persistence model retains the nullable legacy `Visited` value and uses `HasVisitedTime` to distinguish a date-only value from a precise time; user and stamping-point ids form a unique visit key.

Other endpoints:

- `GET /api/providers`
  - Session-aware catalog of available stamping providers; restricted providers are omitted for anonymous requests.
  - Returns providers ordered by name and slug, including abbreviation, description, anonymous-access status, optional validated public website/source/licence URLs, attribution, and public-data-download availability.

- `GET /api/providers/{slug}/points.geojson`
  - Anonymous machine-readable export for an anonymously enabled provider with complete source/licence metadata.
  - Returns point number, name, provider, reference, OSM element id, coordinates, source revision/timestamps, attribution, and licence metadata; never returns accounts, visits, or authentication state.

- `GET /health`
  - Anonymous ASP.NET Core readiness endpoint.
  - Returns healthy only when no EF Core migrations are pending and the seeded Touringen provider exists.
- `GET /api/tours`
  - Exists for hiking tour queries.
  - Not currently used by the bundled HTML map.
- `GET /api/points/{number}?provider={slug}`
  - Returns the authenticated user's visit details for one point; `series={slug}` selects its provider-scoped series and defaults to `standard`.
- `PUT /api/points/{number}?provider={slug}`
  - Creates one visit with optional `visitedOn` and `visitedAt`; `series={slug}` selects the series and defaults to `standard`; duplicates return `409`.
- `PATCH /api/points/{number}?provider={slug}`
  - Changes only the optional date/time of an existing visit.
- `DELETE /api/points/{number}?provider={slug}`
  - Deletes an existing visit. The bundled frontend asks for confirmation before calling it.
- `GET|PUT|PATCH|DELETE /api/points/id/{id}?provider={slug}`
  - Stable-id equivalents used by the bundled frontend, including for temporary points without a public number.
- `POST /api/admin/imports/touringen`
  - Imports Touringen source data.
  - Requires the dedicated CLI bearer token and is intended for manual/admin use.
- `POST /api/admin/imports/harzer-wandernadel`
  - Imports the 222 regular Harzer Wandernadel summer locations from OSM relation 148007.
  - Requires the dedicated CLI bearer token and is intended for manual/admin use.
- `POST /api/admin/imports`
  - Imports user visit data.
  - Requires the dedicated CLI bearer token and is intended for manual/admin use.
- `POST|PUT /api/admin/points`
  - Upserts one or more stamping points (e.g. temporary Sonderstempel) from a JSON payload.
  - Existing points matched by `(series, number)` or `(provider, externalId)` are updated in place while retaining internal IDs and user visits; new points are created.
  - Requires the dedicated CLI bearer token and is intended for manual/admin use.

## Development Notes

The frontend intentionally remains a plain static page under `Api/wwwroot`; there is no separate frontend project or build process.

TourEd source code is licensed under `AGPL-3.0-only`. Separate commercial licenses are available from the copyright holder for organizations that do not want to comply with the AGPL. The AGPL does not grant trademark rights in the TourEd name or branding. Third-party software and provider data retain their own licenses; imported OpenStreetMap data remains subject to ODbL 1.0 where identified.

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
- `dotnet test --no-restore` runs provider-aware persistence/import, readiness, Google account-binding, browser-session/mobile-visit/frontend-contract, and CLI-authentication integration tests after a fresh restore with the .NET 10 SDK.

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
