# Handover note: Kestrel Digital to Barrows

Prepared by the Kestrel Digital delivery team at the end of our engagement.
Thanks for having us. This should get you going.

## Architecture in one paragraph

The **API** (`api/`) is a .NET 8 Minimal API. Clients and Barrows staff sign in with email and password
and get a JWT. The **captive portal** calls a separate set of endpoints (`/api/portal/*`) with a shared
API key, because users aren't signed in when they join the WiFi. Campaign lifecycle runs in a background
service that moves campaigns from Scheduled → Live → Ended and publishes a `CampaignEnded` message.
In production the bus is Azure Service Bus and connection events go to ClickHouse; locally both are
replaced (in-memory bus, events in SQL Server) so the whole thing runs on a laptop.

## Data model

- **Organisation**: Barrows itself (`Internal`) or a client brand (`Client`).
- **User**: belongs to one organisation. Roles: `PlatformAdmin` (Barrows staff, sees every client),
  `ClientAdmin` (manages their own organisation's campaigns), `ClientViewer` (read only).
- **Outlet**: a venue with a hotspot. Identified by its 6-digit **STP number**. Leading zeros matter.
- **Campaign**: belongs to one client organisation, targets a set of outlets for a flight.
  `StartsAt` is inclusive and `EndsAt` is exclusive. All times are stored with their offset; outlets are
  all in South Africa (SAST, UTC+2).
- **ConnectionEvent**: one row per thing that happens on the portal: `connect`, `portal_view`,
  `ad_complete`, `survey_complete`. The portal generates an `EventId` (GUID) for each event.

## Security model

- Every client-facing query must be limited to the caller's organisation. This is done with the
  `VisibleTo(user)` extension in `Infrastructure/Auth.cs`, which is applied across the campaign endpoints.
- Creating campaigns needs `ClientAdmin` or `PlatformAdmin` (policy `CanEditCampaigns`).
- Portal endpoints use the `X-Api-Key` header.

## Reporting

`ReportService` produces the campaign summary (impressions = ad completes, unique users, survey
completes). Clients see it on the campaign page and it will go out by email at the end of each
campaign once KES-212 is done. Numbers were checked against the ad server for the July campaigns.

## Open items

| Ticket | Status | Notes |
|---|---|---|
| KES-212 | In progress | Post-campaign summary email. Consumer is wired up, logic still TODO. Mailer is in place. |
| KES-240 | Ready for release | `db/pending/005_add_event_region.sql` adds Region to events for the provincial dashboard. Run it with the next deployment. |
| KES-251 | Backlog | Outlet picker is slow for some users on older laptops. Probably needs paging. |
| KES-255 | Backlog | Support says the portal shows a blank page on some phones. Could not reproduce on our test devices. |
| KES-260 | Backlog | Sync outlet master data from the national distributor file (CSV drop, monthly). |

## Running locally

See `README.md`. Database scripts in `db/migrations` run automatically on API startup.

## Contacts

Our architect remains available for questions during the transition period.

Kestrel Digital
