# Handover notes

**To:** Head of Product, Barrows WiFi  **From:** [Rodney]  **Re:** What I fixed, what I would fix next, and what I would question in the Kestrel handover

I worked the tickets in order of risk (data leaks and wrong invoices first) and committed in small steps. I tested locally but **not** against production (ClickHouse, Service Bus) or on real old phones, so those parts are hypotheses.

## What I changed, and how I know it works

| Ticket | Real cause | Fix and proof |
|---|---|---|
| Reports leak | Report loaded by id with no organisation check; campaign `PUT` had no role policy | Report built per caller (other clients get 404); policy on `PUT`. Isolation tests, plus a test that fails if any endpoint lacks authorisation |
| Inflated numbers | Report counted other campaigns' events at shared outlets, and retried events were stored several times | Count only the campaign's own events; unique `EventId`; idempotent ingest. 4,703 reproduced and corrected to 3,646 (`Docs/ticket2-investigation.sql`) |
| Summary email (KES-212) | Stub consumer | Delivery ledger, atomic claim before sending, a relay timeout is `Unknown` and never resent. Tests for redelivery, timeout and refusal |
| Wizard (KES-251) | Per-step state, no submit guard, dates sent as UTC midnight (last day lost), 1,600 outlets downloaded | State kept in the wizard, per-step validation, SAST dates with exclusive end, paged search. Unit tests |
| Portal (KES-255) | `esnext` build; unguarded `Object.hasOwn`, `.at()`, `randomUUID`, `localStorage`; survey reshuffled on every render (the repeated question) | `es2015` build, guarded APIs, memoised shuffle. **Not tested on a handset** |
| Distributor file (KES-260) | n/a | `tools/reconcile_outlets.py`, read-only, a reason per row, 9 tests. Real file: 1,809 rows → 1,109 match, 126 new, 443 conflict, 131 rejected |

**The email guarantees** at most one message per ClientAdmin, even with redelivery or relay timeouts. **It does not guarantee delivery:** a timeout leaves the row `Unknown` and nobody is told; a crash mid-send leaves `Sending`.

**Portal, how I would confirm in the field:** a blank screen only in the "Sign in to Wi-Fi" window points to a JavaScript error on an old WebView with a browser that blocks storage. I would reproduce on a real Galaxy J2 or BrowserStack, then add a `window.onerror` beacon so real failures reach us.

## What else I would fix, in priority order

1. **Do not release KES-240 (`db/pending/005_add_event_region.sql`) as written.** It updates ~180M rows in one transaction, then makes `Region` `NOT NULL` with no default while the API never writes it, so every portal event insert would fail. It is also T-SQL, but HANDOVER says production events are in ClickHouse.
2. **Demo data and logins are in the migrations folder.** `003`/`004` seed files sit in `db/migrations`, which runs on every API start. A fresh or restored environment gets a `PlatformAdmin` with the shared password `Assessment#2026` and 37,000 fake events.
3. **Secrets are in git** (`appsettings.json`, `docker-compose.yml`, `web/portal/.env`): the SQL `sa` password (named `Kestrel!Prod2026`), JWT signing key, ingest key. The app connects as `sa`. If any are real: rotate, use a secret store and a least-privilege user.
4. **Tokens never expire.** `ValidateLifetime = false` in `Program.cs`. A leaked or former employee's token works forever. The comment blames drifted tablet clocks, but tablets use the API key, not tokens.
5. **The portal key is public and ingest trusts the client.** The key is compiled into the portal JavaScript, and `POST /api/portal/events` accepts any `campaignId` for any outlet, so anyone can inflate numbers that feed invoices. Check the campaign against the outlet's live campaign, sign a per-outlet token, rate limit.
6. **Phone numbers are logged and stored in plain text.** Ingest logs the whole event (`{@Event}`, with `Msisdn`) at Information level: a POPIA exposure. Stop logging payloads, hash or encrypt, set retention.
7. **Clients can rewrite history.** `PUT /api/campaigns/{id}` changes dates, budget and outlets of a Live or Ended campaign, and the dates decide what is counted. Lock fields once Live, audit changes, decide who may change status.
8. **The email path is not durable.** The local bus is in memory, the lifecycle service publishes before saving status, and there is no outbox or sweeper. `Unknown`/`Sending` rows need an alert and an owner. Campaigns that ended before this release got no summary, and the numbers are a snapshot, so late events can make the live report drift.
9. **Cheap phones, still open.** `es2015` changes syntax, not APIs or CSS. `AbortController` (Chrome 66+) sits inside a `try` that hides failure, so older WebViews silently never send events. Tailwind 4 targets recent browsers and I have not seen the built CSS on an old phone. The page ships all of `lodash` and `moment` plus a 3.5 MB hero image.
10. **Wizard gaps.** The double-click guard is client-side: a lost response plus a retry, or a second tab, can still duplicate (an idempotency key would close it). The server has no limits on name length (200 in the column) or budget size. `Objective` is collected but never sent or stored. Search returns inactive outlets, which the API then rejects with a generic message.
11. **Reports will not scale, and the basics are missing.** `ConnectionEvents` has no index but the unique `EventId`, so every report scans 180M rows; the campaigns list runs a query per campaign. No CI, no login rate limit, Swagger on everywhere, tokens in `localStorage`, and ingest trusts the phone's clock.

## What in HANDOVER.md I would question

- **"`VisibleTo` is applied across the campaign endpoints."** It was missing on reports, and `PUT` had no role policy. I would test every endpoint before trusting the security model.
- **"Numbers were checked against the ad server for the July campaigns."** In the repo's data the report overcounted by up to 59% (Summer Refresh +29%, Heritage Month Braai +59%). Either the check did not happen or those campaigns were unaffected. Finance should review anything invoiced from the old report.
- **"KES-240 ready for release."** See priority 1.
- **"KES-251: older laptops" and "KES-255: could not reproduce on our test devices."** The first was phones at 360px; the second needs old handsets, and the repeated survey question reproduces on any device.
- **"In production the bus is Service Bus and events go to ClickHouse."** My unique index and ledger only protect SQL Server. I have not seen production.
- **"Scripts in `db/migrations` run automatically."** True, including the seed data (priority 2), and as `sa`.

## What I would do in my first month

1. **Week 1:** learn what production really is (access, backups, which items above are live). Rotate secrets, take seed data off the migration path, switch on token lifetimes, stop logging phone numbers, and have Finance check past invoices.
2. **Week 2:** CI running `dotnet test`, `npm test` and both builds on every push; a staging environment with production-shaped data; alerts, including `Unknown` deliveries.
3. **Weeks 2 to 3:** harden ingest, de-duplicate in ClickHouse, add report indexes, and redo KES-240 as a batched online backfill with a default.
4. **Weeks 3 to 4:** test the portal on real old phones and add the error beacon; shrink the page; agree a stable file format and owner with the distributor, since 24% of this month's rows needed a human decision.

## AI use

- **Used Claude for:** reading the codebase and finding root causes (report leak, inflated numbers, email-once design), drafting fixes and tests for tickets 1 to 3, reviewing my own code, and a final review of my diff. 
- **Used ChatGpt for:** 
- **Ticket 6 (KES-260):** Designing and implementing a Python-based outlet reconciliation tool, defining matching and data-validation rules, documenting assumptions and trade-offs, and creating unit tests. Testing against the supplied distributor and existing outlet CSV files.
- Using ChatGPT as a technical sounding board to challenge assumptions, troubleshoot errors and validate implementation approaches.
- **What it got wrong, or I changed:**
  - Commands it gave did not run first time (I had .NET 10 and Windows blocked the test DLLs, so I ran tests in a Docker container). It cannot compile .NET, so every line of C# was checked by my own test runs.
  - Reviewing my email consumer it found real bugs that I fixed: an HTTP timeout is a `TaskCanceledException`, which my `catch` would have treated as a shutdown, and a refusal and a timeout were treated the same.
  - Its final review found that my Ticket 5 change had moved the event id back inside the retry loop, undoing my Ticket 2 fix. I fixed it. 
  - It was unsure, and said so, whether the duplicate events came from portal retries. I could not confirm, so I fixed both the API and the portal.
- **Did myself:** **["wrote the Ticket 3 consumer, the Ticket 4 wizard and search endpoint,script; ran every test and the end-to-end checks against SQL Server in Docker; made every commit".]**
