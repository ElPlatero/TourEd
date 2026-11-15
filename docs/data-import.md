# TourEd data import reference

Detailed reference moved out of `AGENTS.md` unchanged on 2026-11-15. `AGENTS.md` keeps the rules and points here; update this file in the same change whenever the behavior it describes changes.

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

Seeded trail providers (Malerweg, Schluchtensteig, Heidschnuckenweg, Harzer Klosterwanderweg, Bliessteig, Kellerwaldsteig):

- Fixed stamping points across established trails (Malerweg: 8, Schluchtensteig: 6, Heidschnuckenweg: 13, Harzer Klosterwanderweg: 16, Bliessteig: 10, Kellerwaldsteig: 10 unnumbered points), seeded directly via EF Core migrations. The Bliessteig seed records its official CC BY 4.0 route-data provenance; the Kellerwaldsteig seed records its official CC BY-SA 4.0 station-data provenance. Neither offers a GeoJSON export.
- Available without a background network import, but visible only to authenticated users with a matching entitlement.
- Provenance/import metadata fields remain null for Malerweg, Schluchtensteig, Heidschnuckenweg, and Harzer Klosterwanderweg, hiding external source/licence links and public GeoJSON export for those providers. Bliessteig and Kellerwaldsteig display their official source and licence, but intentionally leave `DataImportedAt` unset so TourEd does not offer the optional GeoJSON export.

User data import:

- Requires `Authorization: Bearer <token>` using the dedicated CLI configuration and runs as its configured existing TourEd user.
- Accepts uploaded CSV-like data.
- Requires exactly one non-empty CSV file with headerless `number;dd.MM.yyyy;HH:mm` rows; date and time may be empty, but a time requires a date. Numbers contain 1–3 ASCII digits and must be positive. Date-only visits retain their date without implying a time.
- Validates complete rows, duplicate numbers (including equivalent leading-zero forms), and unknown numbers in the entitled default provider’s standard series. Any error rejects the whole file with HTTP 400 and structured `errors` containing 1-based `line` numbers (null for file-level errors). No partial writes occur.
- Successful imports return `imported`, `existing`, `rejected` (zero), and `errors` (empty). Existing visits are counted and never overwritten. Failed validation reports offending rows in `rejected`; other rows remain unprocessed and `imported`/`existing` are zero.
- Maps numbers only to stored stamping points from the authenticated user's default provider when that provider is enabled for the user; it never falls back to another provider.
- Creates user visit records for the authenticated user.
