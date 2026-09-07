# Snapshot initialization regressions (Issue #77)

This standalone test uses Node.js, Playwright and Chromium. There is no frontend build or production dependency. Install Playwright in a temporary directory; the tests use the bundled OpenLayers assets:

```bash
npm install --prefix /tmp/toured-browser-tests playwright
PLAYWRIGHT_MODULE=/tmp/toured-browser-tests/node_modules/playwright node TourEd.Tests/Browser/snapshot-initialization.cjs
```

`CHROMIUM_PATH` defaults to `/usr/bin/chromium`. The script starts a temporary HTTP server, serves the real app, intercepts backend requests with a controlled session and point fixture, and blocks external requests. No production service or account is used. Chromium and the server close when the test ends.

Two tabs in the **same** Playwright BrowserContext share real IndexedDB and cookie state; separate BrowserContexts would isolate storage. A test-only wrapper pauses initialization immediately before its final snapshot transaction. The production mutation, visit buttons, queue, lease and synchronization code run unchanged. Cross-tab notifications to the reloading tab are suppressed to test durable invalidation independently of delivery timing.

Covered interleavings:

- B queues a stamp after A's earlier reads; A must preserve it, then synchronization must send one successful mutation and retain the visit.
- B completes a newly queued stamp before A writes; A must not recreate the action.
- A has already read a queued action held by B's synchronization lease; B completes it before A writes. The older queue must not be restored.
- Logout deletes the snapshot before A writes; A must remain locked and leave it deleted.
- Logout and a different account's initialization complete before A writes; A must preserve the new account's snapshot and remain locked.
- Logout in A changes its initialization generation while the write is pending.
- A's confirmed session expires before its final write.

For a negative control, `TOURED_SCRIPT` can point to an older copy of `toured.js`; `SCENARIOS=queued` or `SCENARIOS=completed-old` selects a deterministic regression case. The pre-fix implementation fails both. These frontend fixtures supplement the .NET integration suite; they do not replace backend authentication and entitlement tests.

## Local OpenLayers installation (Issue #84)

Run `PLAYWRIGHT_MODULE=/tmp/toured-browser-tests/node_modules/playwright node TourEd.Tests/Browser/local-openlayers.cjs`. Fresh browser contexts allow real service workers. DNS blocks every foreign host, including tile hosts, while the local server supplies session fixtures. The test covers root and PathBase hosting, anonymous and authenticated startup, local library/license cache entries, and offline reload.
