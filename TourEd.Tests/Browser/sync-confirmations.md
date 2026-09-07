# Synchronization confirmation regressions (Issue #80)

The standalone test uses Node.js, Playwright and Chromium. It adds no frontend build or production dependency. Install Playwright in a temporary directory and download the exact OpenLayers file referenced by `Api/wwwroot/index.html`:

```bash
npm install --prefix /tmp/toured-browser-tests playwright
PLAYWRIGHT_MODULE=/tmp/toured-browser-tests/node_modules/playwright node TourEd.Tests/Browser/sync-confirmations.cjs
```

`CHROMIUM_PATH` defaults to `/usr/bin/chromium`. The test serves the real app from a temporary local HTTP server, intercepts API responses, and blocks external requests. Two tabs share one Playwright BrowserContext and real IndexedDB; separate BrowserContexts would isolate storage. Chromium and the server close after the tests.

A stamp is queued through the real visit button. The simulated server applies its desired state immediately but withholds A's HTTP response. Both tab clocks advance beyond the production 30-second lease, then B synchronizes the same action and receives the confirmation. A's response arrives afterwards. The tests assert the stored snapshot, provider totals, point states, queue and lease ownership, as well as the simulated backend's single effective state transition.

Cases:

- B completes the action before A's late response; progress increases exactly once.
- B queues a newer removal for the same point. Its timestamp is deliberately made equal to the old action's timestamp; its distinct action ID, optimistic point state and active lease must survive A's response.
- Another account logs in before the old response arrives, with BroadcastChannel delivery to A suppressed.
- A late `401` from the old request must not purge the new account's snapshot.
- A new initialization generation invalidates an in-flight synchronization run.
- A legacy schema-3 action without an ID receives a persistent ID before sending and survives takeover.
- Calling the actual completion function twice under one valid lease updates progress only once.
- A new lease token within the same tab prevents the previous run from confirming or releasing the new lease.
- An expired lease without takeover leaves the action pending for a later retry.

The test exposes existing production functions only to trigger synchronization, await completion and exercise the transaction guards directly. It does not replace the IndexedDB, queue, lease or completion implementations. Legacy queues and clock/generation changes are deliberate fixture staging.

`SCENARIOS=takeover` selects a single case. `TOURED_SCRIPT=/path/to/old/toured.js` runs a negative control against an older implementation; the original code fails the takeover case by increasing local progress twice. The .NET integration suite separately verifies backend authentication, entitlements and atomic visit-state behavior.
