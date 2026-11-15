# TourEd API reference

Detailed reference moved out of `AGENTS.md` unchanged on 2026-11-15. `AGENTS.md` keeps the rules and points here; update this file in the same change whenever the behavior it describes changes.

Main consumer endpoint:

- `GET /api/points`

Useful query behavior:

- Requires an authenticated session (`401` otherwise).
- `provider=<slug>` returns points for a specific stamping provider when it is enabled for the authenticated user; disabled providers return `403`.
- `provider=all` returns all providers enabled for the authenticated user.
- `vis=true` returns visited points for the authenticated user.
- `vis=false` returns unvisited points for the authenticated user.
- Omitting `vis` returns both visited and unvisited points with the authenticated user’s own visit state and optional date/time. Visit metadata always uses the authenticated identity independently of the optional visit-state filter.
- Geo filtering exists via query parameters and is used server-side.

Point DTOs include a stable internal id, provider and series metadata, optional number, name, position, explicit visit state, optional visit date/time, and tours.

Visit state is represented independently from its optional timestamp: `isVisited` reports whether a visit row exists, `visitedOn` is the optional date, and `visitedAt` is the optional time. A time requires a date. The persistence model retains the nullable legacy `Visited` value and uses `HasVisitedTime` to distinguish a date-only value from a precise time; user and stamping-point ids form a unique visit key.

Other endpoints:

- `GET /api/providers`
  - Complete provider catalog with entitlement, readiness, and personal progress aggregates for the authenticated user; requires an authenticated session.
  - Returns providers ordered by name and slug, including abbreviation, description, data readiness (also exposed as the legacy `isAnonymousAccessAllowed` field, which the frontend still reads as a fallback for older offline snapshots), optional validated public website/source/licence URLs, attribution, and public-data-download availability.

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
