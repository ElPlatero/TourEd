# TourEd Project Context

## Purpose

TourEd is a small .NET 10 application for stamping points and hiking tours from multiple providers.

It stores Touringen, Harzer Wandernadel (HWN), Malerweg (MW), Schluchtensteig (SST), Heidschnuckenweg (HNW), and Harzer Klosterwanderweg (HKW) stamping points, hiking tours, tour-to-point relationships, users, and user visits in a SQLite database. The main user-facing feature is showing stamping points on a map and distinguishing visited from unvisited points for a known user.

Stamping points are anchored by `StampingProvider` and belong to a provider-scoped `StampingSeries`. A series supplies the number namespace, so equally numbered points from different editions remain distinct. Per-user `UserStampingProvider` entitlements determine which providers an authenticated account may access. Existing users received every provider present when the entitlement migration ran; new users and providers receive no automatic grants. `User.DefaultStampingProviderId` is nullable and is used only when it references an entitled provider.

## Frontend

The repository contains one user-facing frontend rooted at `Api/wwwroot/index.html`, with its local CSS and JavaScript in `Api/wwwroot/css/toured.css` and `Api/wwwroot/js/toured.js`. It is part of the ASP.NET Core application and is published and deployed together with the API.

It is a plain HTML, CSS, and vanilla-JavaScript application using OpenLayers. It has no frontend build process. It loads OpenStreetMap tiles and displays stamping points as map markers.

The frontend is mobile-first and fluid. On touch-oriented or narrow displays, stamping-point details appear as a bottom sheet; fine-pointer desktop displays position the same accessible dialog near the selected marker. Selection works by click or touch, the dialog has an explicit close button and Escape handling, interactive controls have touch-sized targets, safe-area insets are respected, and visited markers add a check symbol so visit state is not conveyed by color alone. Pointer hover remains an optional desktop enhancement. A shared top-right control bar contains mutually exclusive provider-filter and account flyouts; their text labels collapse visually on narrow displays while accessible names remain. Provider information opens in a keyboard-accessible modal with a validated optional external link and an independence disclaimer. The TourEd logo appears as its compact round signet on narrow displays and as the full wordmark on desktop displays.

The page calls the backend with relative URLs:

- `GET auth/session`
  - Loaded first to determine whether the browser has a valid TourEd session.
  - Returns authenticated state, authenticated email, and ticket expiration timestamp (`expiresAt`).
- `GET auth/login`
  - Used by the “Mit Google anmelden” link/button to start the Google challenge.
- `POST auth/logout`
  - Ends the TourEd cookie session, purges local snapshot data, and returns the UI to the login lock screen.
- `GET api/providers`
  - Requires an authenticated session (`401` otherwise).
  - Loads the complete provider catalog configured in the database, including `isEnabled`, `isDataReady`, `isAnonymousAccessAllowed`, `totalPoints` (nullable), `visitedPoints` (nullable), `hasPublicDataDownload`, and overall totals `overallCount`, `totalPoints`, `visitedPoints` across enabled and ready providers.
  - Enabled and data-ready provider slugs are selected internally by default.
- `GET api/points?provider=all&vis=false`
  - Requires an authenticated session (`401` otherwise).
  - Loads unvisited points from every provider enabled for the authenticated user into the in-memory frontend cache.
- `GET api/points?provider=all&vis=true`
  - Requires an authenticated session (`401` otherwise).
  - Loads visited points from every provider enabled for the authenticated user into the in-memory frontend cache.

The frontend is a Progressive Web App (PWA) installable on Android, iOS, and desktop with standalone display. It includes a web app manifest (`manifest.webmanifest`), signet app icons (`icon-192.png`, `icon-512.png`, maskable `icon-maskable-512.png`, `apple-touch-icon.png`, `favicon.ico`), and a dedicated service worker (`service-worker.js`). The service worker caches exclusively the versioned app shell (HTML, CSS, JS, manifest, images, privacy notice) and the exact external OpenLayers CSS and JavaScript assets. It never handles or caches `/auth`, valid `/signin-google` OAuth callbacks, `/api`, `/health`, non-GET requests, or OpenStreetMap tiles; a callback navigation without OAuth state is redirected to the app root so a legacy worker cannot reload a stale callback URL into a `403`. Service worker installation is atomic and each worker reads only its own named cache; increment the cache version whenever a cached asset changes. When an updated version is installed and waiting, a non-intrusive update banner offers a "Neu laden" button that sends `SKIP_WAITING`, claims the current clients, and reloads the application exactly once upon the resulting controller change. The frontend also canonicalizes a stale callback URL to the app root before reloading. Initial service-worker installation never reloads the page automatically.

Offline support stores exactly one unencrypted personal snapshot in IndexedDB (`toured-db`, store `snapshots`, key `current`) containing schema version 3, bound email, the effective server session expiration timestamp (including a sliding renewal), complete provider and point responses, and a persistent per-point visit-action queue. Schema-1 and schema-2 snapshots migrate losslessly to schema 3. Every online or offline visit action first atomically stores its expected confirmed state and desired state, then updates the local map and progress overview optimistically; that point remains locked and shows a static accessible synchronization symbol in its details until resolved. The open page synchronizes queued actions sequentially after a confirmed matching session on reconnection or app start, uses a cross-tab IndexedDB lease and notifications to prevent duplicate sends, retries transient failures with bounded exponential backoff, and overlays unresolved local states on refreshed server data. The service worker never synchronizes actions. An expiration timer purges and locks personal offline data even when the page remains open. OpenStreetMap tiles are not stored offline; missing tiles trigger an accessible offline banner. Snapshots and queued actions are purged on logout, account switch, expired session, or `401` response.

The frontend starts fail-closed: the accessible login barrier (`#authBarrier`) is visible in the initial HTML and the main container (`#appShell`) is already `inert` and `aria-hidden="true"`. While the initial session check is pending, the barrier shows an accessible loading indicator instead of the Google login action; that action appears only after an anonymous session is confirmed. A confirmed authenticated session unlocks the application; unauthenticated use sends zero provider or point requests.

The frontend keeps provider metadata and point arrays only in memory. Marker rendering is centralized and filters the cached arrays against the currently selected provider slugs; changing the checkbox selection in the provider flyout does not require another point request. The provider filter flyout shows only enabled and data-ready providers with checkboxes and bulk selection ('Alle' und 'Keine'); individual provider info buttons are omitted from this filter list. An empty selection renders zero points. A permanently visible compact progress counter and bar below the top control bar displays total personal progress across enabled, data-ready providers (unaffected by map filter checkboxes). Clicking it opens a mutually exclusive progress panel listing all providers sorted descending by completion ratio (visited/total) and tied by name ascending, with not-ready providers grouped at the bottom sorted alphabetically. Each progress item displays provider name, abbreviation, counts (`visited / total`), progress bar, and an info button. Locked providers (`isEnabled == false`) are subtly styled, announce `(Nicht freigeschaltet)`, show historical visit counts, and cannot be selected as map filters. Not-ready providers (`isDataReady == false`) show 'In Vorbereitung' without counts or progress bars. Any stamping point with `ValidFrom` or `ValidUntil` set (temporary/special stamps) is excluded from all progress totals and visited counts; `StampingPointDto` carries `countsTowardProgress` to distinguish permanent from temporary points. Provider names, abbreviations, and non-standard series names are used in point details because point numbers are series-scoped and may be absent for temporary stamps. Visit mutations use the stable internal point id together with the provider slug. Provider information also exposes recorded source/licence metadata and a public GeoJSON download when available. Login, logout, and reinitialization reload the catalog and point caches, and stale initialization responses are ignored.

Authenticated users can record a stamp directly from a locked point-detail dialog using large, square action tiles with dedicated icons and concise labels ('Jetzt stempeln', 'Nachtragen', 'Bearbeiten', 'Entfernen'), either online or from a valid offline snapshot, with the captured current local date and time or with no timestamp, a date only, or a date plus time. Existing stamps allow editing only their optional date/time and can be removed after a point-specific confirmation. Every visit request includes the provider slug. Visit writes use the stable-id atomic state endpoint with an expected and desired state; an already reached target is idempotent success, an unchanged expected state is updated, and a differing concurrent server state wins. Successful mutations update the cached point and marker layer without a full page reload.

A magnifier button to the left of the provider and account controls opens a mutually exclusive search flyout. A crosshair button to the left of the search button starts client-side geolocation only after it is pressed, centers the map on the user's current device location, and keeps the marker current while the page remains open. It renders a logo-colored blue marker with a dark blue outline and a soft accuracy area. TourEd does not transmit the coordinates to its backend or store them; centering still causes the normal OpenStreetMap tile requests for the displayed map area. The client searches only the already loaded points of currently selected providers, normalizing case and diacritics across point name, number, provider name, and abbreviation. Search results are capped, contain provider-scoped number labels, and selecting one centers and zooms the existing map before opening its locked point-detail dialog. Search terms and results are not persisted and require no additional backend endpoint.

Locked point details offer an accessible "Link kopieren" action. The canonical same-origin URL identifies a point by provider slug and stable internal point id, so equally numbered series and unnumbered temporary points remain unambiguous. On opening such a link, the frontend validates and canonicalizes its query parameters, retains the link through Google authentication via a server-validated local return URL, activates the entitled provider when necessary, centers and zooms the existing map, and opens the locked point detail without an additional point request. Unknown, unavailable, or non-entitled points disclose no catalog data and show a generic unavailable message.

A three-state icon-only visit filter button between geolocation and search cycles through all, only open, and only visited stamping points. It filters the existing in-memory point caches together with the provider selection, updates search results to the currently visible visit states, and never issues another point request. The filter resets to all after each page initialization and uses distinct icons, colors, tooltips, and accessible labels for every state.

Visible stamping points are clustered client-side through OpenLayers when their markers would overlap. Single points retain their normal pin styles; clusters show a capped total in a white center with a size-scaled outer ring: light blue for entirely open, dark blue with a check for entirely visited, and a fixed diagonal light/dark split for mixed visit states. Provider and visit filters are applied before clustering, searches still open their concrete point, the user-location layer is excluded, and no additional API request is made. Clicking or tapping a cluster animates the map up to three zoom levels toward its extent, respects reduced-motion preferences, and does nothing when an inseparable cluster remains at maximum zoom. Locked point details stay open and follow their point after map movement.

The public privacy notice is served at `Api/wwwroot/datenschutz/index.html` and linked permanently from the map and login barrier. It is available without authentication and carries a `noindex` directive to reduce search-engine discoverability. The notice documents the current Google login, cookies, account/visit storage, IndexedDB offline snapshot, service-worker caching, hosting logs, OpenStreetMap tiles, external OpenLayers CDN, and user-initiated navigation to external provider websites and the public GitHub source repository. Keep it synchronized whenever these data flows or their retention rules change.

The map attribution permanently links its compact `© TourEd` label to the public TourEd source repository, with an accessible AGPL-3.0 source-link label, next to the privacy link.

The frontend never reads or writes user ids, Google subjects, tokens, custom identity headers, local storage, or session storage. A `401` while loading authenticated point data returns the UI to the login lock screen without starting an infinite redirect loop.

Optional `provider` query behavior on `GET api/points`:

- Requires an authenticated cookie session; unauthenticated requests receive `401`.
- Omitted: uses the authenticated user's default provider only when that provider is enabled for the user; there is no fallback.
- Provider slug, for example `provider=touringen`: returns points only when that provider is enabled for the authenticated user; otherwise it returns `403`.
- `provider=all`: returns points from every provider enabled for the authenticated user.

The map uses logo-colored SVG pin assets stored in `Api/wwwroot/img`:

- `img/pin_icon_neutral.svg`
- `img/pin_icon_visited.svg`
- `img/toured-logo-transparent.svg`
- `img/icon-192.png`
- `img/icon-512.png`
- `img/icon-maskable-512.png`
- `img/apple-touch-icon.png`

The login barrier hides the visit-state legend. Open points use the logo's light blue; visited points use its dark blue and carry a white check matching the logo. The bundled map omits OpenLayers' on-map zoom buttons while retaining its touch, mouse, and keyboard zoom interactions and the cluster click-to-zoom behavior. Map rotation is disabled at both the interaction and view levels, including Alt/Shift drag and two-finger rotation.

The current static map does not use the tours endpoint or admin/import endpoints.

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

Provider data is represented by `StampingProvider`; collections and editions are represented by `StampingSeries`. The seeded Touringen series are Standard, Naturschätze, Familienwanderwege Rhön, and the variable temporary Sonderstempel collection. HWN, Malerweg (8 points), Schluchtensteig (6 points), Heidschnuckenweg (13 points), and Harzer Klosterwanderweg (16 points) each have one standard series. Numbered points are unique by series and number; unnumbered points retain identity through their provider-scoped external id. A point's provider and series are constrained to match. `UserStampingProvider` uses `(UserId, StampingProviderId)` as its unique key. Removing an entitlement hides its provider and visits without deleting visit rows; restoring it makes those visits visible again. Deleting a user cascades to entitlements.

Administrative entitlement and registration decisions are recorded in `AdminAuditEntry` with timestamp, actor user id, action (`registration.approved`, `registration.rejected`, etc.), optional target user id, optional registration-request id, and optional provider slug. Registration rejections identify the request without inventing a target user. Tokens, email addresses, and Google subjects are not copied into the audit table. The CLI-protected, bounded `GET /api/admin/audit` endpoint returns the newest entries for the separate admin client. Audit entries are retained for 90 days from creation and automatically deleted by the hosted retention cleanup.

`StampingProvider` also stores the public name, abbreviation, description, optional website, legacy anonymous-access/data-readiness flag, and optional imported-data provenance used by the provider catalog and GeoJSON export. Provenance includes source and licence links, attribution, source revision/timestamp, and import timestamp. Public provider DTOs expose only absolute HTTP(S) URLs; unsupported URI schemes are omitted. Browser catalogs, points, tours, visits, search data and GeoJSON exports are always restricted by user entitlements.

Self-registration is managed via `RegistrationRequest` (`GoogleSubject`, `Email`, `Status`, `CreatedAt`, `UpdatedAt`, `DecidedAt`, `AdminNotificationSentAt`). Unknown Google logins do not create immediate user accounts; instead, `GoogleLoginService` creates or updates one pending request and the browser is redirected to the login barrier with a pending notification (`/?registration=pending`). Duplicate pending logins may update a changed verified email without creating another request or resetting its retention period. A rejected request remains rejected and unchanged on later Google logins; the callback returns `/?registration=rejected`, and pending/rejected barrier states hide the Google login action. Once the rejected request is deleted, the same identity may submit a new request because TourEd keeps no permanent blocklist. A hosted cleanup runs once after application startup and every 24 hours, independently of login and admin traffic. It deletes pending requests 30 days after creation, decided requests 30 days after the decision, and admin audit entries 90 days after creation; failures are logged without failing readiness or user operations and are retried at the next run. Only pending requests can be decided; repeated or conflicting decisions return `409`, and approval persists the new user, request status, and audit entry atomically. Approval creates a new `User` without entitlements. Entitled default providers and entitlements are configured administratively afterwards.

To notify administrators of new registration requests without polling the admin client manually, a dedicated `RegistrationRequestNotificationService` hosted background service checks for unnotified pending requests (`Status == Pending && AdminNotificationSentAt == null`) once after startup and every five minutes. Multiple pending requests are aggregated into a single plain-text email sent via MailKit to the configured administrative recipient. SMTP connections require STARTTLS; production uses IONOS on `smtp.ionos.de:587`. A singleton `RegistrationNotificationState` stores the last successful send independently of request retention and deletion, ensuring a global one-hour throttle across all requests. The notification contains strictly aggregated request counts (number of new requests and total open requests) and an instruction to open `TourEd.Admin`; applicant emails, Google subjects, internal IDs, and direct decision links are never included. Only after the SMTP server successfully accepts the email are the included pending requests and the global send timestamp updated atomically. Transient SMTP, network, or configuration failures are logged by exception type only, without secrets or applicant data, and retried on the next scheduled tick without affecting application startup, readiness, logins, or existing HTTP/CLI contracts.

Users can optionally store a unique Google subject identifier. `GoogleLoginService` resolves an existing binding by subject or atomically binds the first verified Google login to an existing user by normalized email. It never creates users directly.

The main runtime composition happens in `Api/Program.cs`.

`Api/Program.cs` enables default and static files, so `Api/wwwroot/index.html` and its assets are served by the same application as the API.

Authentication is scheme-separated:

- Browser requests authenticate only through the encrypted `toured-session` cookie, which is the default scheme, is `Secure`, `HttpOnly`, `SameSite=Lax`, expires after eight hours, and uses sliding expiration.
- Cookie principals are revalidated against the stored user on every authenticated request; deleting a user therefore invalidates already issued sessions.
- Google is used only by the explicit `/auth/login` challenge. Its callback binds through `GoogleLoginService`, discards Google claims/tokens, and stores only internal user-id and email claims in the TourEd cookie.
- Import routes use the separate `TouredCliImport` policy and `TouredCliBearer` scheme. Only the configured bearer token can resolve the configured existing user; cookie identities do not satisfy this policy.
- Arbitrary request headers and URL query parameters never establish a browser identity.
- Protected API endpoints return `401`/`403` instead of redirecting to Google or returning HTML.
- Permissive CORS is disabled; browser authentication is intentionally Same-Origin.

Authentication endpoints:

- `GET /auth/login` starts the Google challenge.
- `GET /auth/session` returns anonymous/authenticated state and the authenticated email only.
- `POST /auth/logout` removes the TourEd session cookie.

Runtime configuration uses `Authentication__Google__ClientId`, `Authentication__Google__ClientSecret`, `Authentication__Cli__UserEmail`, `Authentication__Cli__Token`, `PathBase`, `DataProtection__KeysPath`, `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`, `touringen__StempelstellenUri=https://www.touringen.de/stempelstellen`, `RegistrationNotifications__Enabled`, `RegistrationNotifications__SmtpHost`, `RegistrationNotifications__SmtpPort`, `RegistrationNotifications__SmtpUsername`, `RegistrationNotifications__SmtpPassword`, `RegistrationNotifications__SenderAddress`, and `RegistrationNotifications__RecipientAddress`. Production values belong in the root-protected runtime environment file configured by `RUNTIME_ENV_FILE`, not in appsettings or the visible systemd unit. The non-secret HWN OSM relation id, API/public URLs, and size limit are configured under `harzerWandernadel` in appsettings. Server setup validates the required environment entries and creates the configured persistent Data-Protection directory outside the replaceable application release for the runtime user only. Kestrel must listen only on the trusted proxy host or private network when forwarded headers are globally enabled.

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
- Atomically records OSM provenance/licence metadata, records the import, and marks HWN data ready for entitled catalog/GeoJSON access only after the complete validated point update succeeds.
- Is started manually through the CLI-protected admin endpoint; no schedule or workflow triggers it automatically.

Seeded trail providers (Malerweg, Schluchtensteig, Heidschnuckenweg, Harzer Klosterwanderweg):

- Fixed stamping points across established trails (Malerweg: 8, Schluchtensteig: 6, Heidschnuckenweg: 13, Harzer Klosterwanderweg: 16), seeded directly via EF Core migrations.
- Available without a background network import, but visible only to authenticated users with a matching entitlement.
- Provenance/import metadata fields remain null, hiding external source/licence links and public GeoJSON export in the provider information modal.

User data import:

- Requires `Authorization: Bearer <token>` using the dedicated CLI configuration and runs as its configured existing TourEd user.
- Accepts uploaded CSV-like data.
- Parses stamping point numbers and optional visit timestamps.
- Maps numbers only to stored stamping points from the authenticated user's default provider when that provider is enabled for the user; it never falls back to another provider.
- Creates user visit records for the authenticated user.

## Important API Context

Main consumer endpoint:

- `GET /api/points`

Useful query behavior:

- Requires an authenticated session (`401` otherwise).
- `provider=<slug>` returns points for a specific stamping provider when it is enabled for the authenticated user; disabled providers return `403`.
- `provider=all` returns all providers enabled for the authenticated user.
- `vis=true` returns visited points for the authenticated user.
- `vis=false` returns unvisited points for the authenticated user.
- Geo filtering exists via query parameters and is used server-side.

Point DTOs include a stable internal id, provider and series metadata, optional number, name, position, explicit visit state, optional visit date/time, and tours.

Visit state is represented independently from its optional timestamp: `isVisited` reports whether a visit row exists, `visitedOn` is the optional date, and `visitedAt` is the optional time. A time requires a date. The persistence model retains the nullable legacy `Visited` value and uses `HasVisitedTime` to distinguish a date-only value from a precise time; user and stamping-point ids form a unique visit key.

Other endpoints:

- `GET /api/providers`
  - Complete provider catalog with entitlement, readiness, and personal progress aggregates for the authenticated user; requires an authenticated session.
  - Returns providers ordered by name and slug, including abbreviation, description, anonymous-access status, optional validated public website/source/licence URLs, attribution, and public-data-download availability.

- `GET /api/providers/{slug}/points.geojson`
  - Machine-readable export for a user-entitled, data-ready provider with complete source/licence metadata; requires an authenticated session.
  - Returns point number, name, provider, reference, OSM element id, coordinates, source revision/timestamps, attribution, and licence metadata; never returns accounts, visits, or authentication state.

- `GET /health`
  - Anonymous ASP.NET Core readiness endpoint.
  - Returns healthy only when no EF Core migrations are pending and the seeded Touringen provider exists.
- `GET /api/tours`
  - Exists for hiking tour queries.
  - Not currently used by the bundled HTML map.
- `GET /api/points/{number}?provider={slug}`
  - Returns the authenticated user's visit details for one point; `series={slug}` selects its provider-scoped series and defaults to `standard`.
- `PUT /api/points/{number}?provider={slug}`, `PATCH /api/points/{number}?provider={slug}`, and `DELETE /api/points/{number}?provider={slug}`
  - Legacy number-based visit writes remain available, but the bundled frontend does not use them.
- `GET|PUT|PATCH|DELETE /api/points/id/{id}?provider={slug}`
  - Stable-id legacy visit operations, including for temporary points without a public number; the bundled frontend now uses the atomic state endpoint for writes and does not need a separate reconciliation read.
- `PUT /api/points/id/{id}/state?provider={slug}`
  - Atomically compares an expected visit state with the authenticated user's current state and applies a desired state. It returns the canonical `VisitDto`; an already reached desired state is an idempotent success, while a concurrent differing state returns `409` with that canonical server state. The bundled frontend uses this endpoint for all online and queued writes.
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
- `DELETE /api/admin/users/{id}`
  - Permanently removes a non-administrative target user together with visits, provider entitlements, Google binding, and matching registration request while retaining global stamping points and a minimal `user.deleted` audit entry. The configured CLI user cannot delete itself.
- `GET /api/admin/users`
  - Lists existing users with Google-link state, provider entitlements, and optional entitled default provider.
  - Requires the dedicated CLI bearer token.
- `GET /api/admin/providers`
  - Lists every provider for administrative entitlement editing, independent of the CLI identity's own entitlements.
  - Requires the dedicated CLI bearer token.
- `PUT /api/admin/users/{id}/providers`
  - Atomically replaces one user's provider entitlement set and optional default provider; the default must belong to the submitted set.
  - Writes minimal audit entries for grants, revocations, and default-provider changes and preserves all visit rows.
  - Requires the dedicated CLI bearer token.
- `GET /api/admin/registrations`
  - Lists registration requests with an optional `status` query filter; retention cleanup runs separately in the hosted cleanup service.
  - Requires the dedicated CLI bearer token.
- `POST /api/admin/registrations/{id}/approve`
  - Approves a registration request, creates the user account without entitlements, and logs an audit entry.
  - Requires the dedicated CLI bearer token.
- `POST /api/admin/registrations/{id}/reject`
  - Rejects a registration request, updates status to `rejected`, and logs an audit entry.
- `GET /api/admin/audit?offset=0&limit=100`
  - Returns audit entries newest first; offset is non-negative and limit is clamped to 1 through 250.
  - Requires the dedicated CLI bearer token.

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
