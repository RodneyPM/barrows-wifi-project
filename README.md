# Barrows WiFi: assessment repo

A cut-down copy of the Barrows WiFi platform, set up for the technical assessment. It is not production
code, but it is built the same way and has the same kinds of problems a real inherited codebase has.

Your brief lists the tickets. Start with `HANDOVER.md`, which is the note the previous
development partner left behind.

## What's in here

| Folder | What it is |
|---|---|
| `api/` | .NET 8 Minimal API, EF Core on SQL Server, JWT + API-key auth, MassTransit (in-memory locally) |
| `api/db/migrations/` | SQL scripts applied in order on API startup |
| `api/db/pending/` | Scripts prepared for the next release, not applied yet |
| `web/admin/` | React + Vite + TypeScript + Tailwind: campaign portal used by clients and our team |
| `web/portal/` | React captive portal that consumers see when they join the WiFi at an outlet |
| `data/` | Files for the data ingest task |

## Running it

You need Docker, the .NET 8 SDK and Node 22.

**Everything in Docker**

```bash
docker compose up --build
```

**Or run the database in Docker and the rest locally** (faster feedback while you work)

```bash
docker compose up -d sqlserver
cd api && dotnet run --project src/BarrowsWifi.Api
cd web/admin && npm install && npm run dev
cd web/portal && npm install && npm run dev
```

The first API start creates the database and loads the seed data. It takes about a minute.

| Service | URL |
|---|---|
| API + Swagger | http://localhost:5080/swagger |
| Campaign portal | http://localhost:5173 |
| Captive portal | http://localhost:5174/?stp=835903 (an outlet in a live campaign; any STP from `data/outlets-current.csv` works) |

Apple Silicon: enable "Use Rosetta for x86/amd64 emulation" in Docker Desktop for the SQL Server image.

## Seed logins

All seed users share the password `Assessment#2026`.

| Email | Role | Organisation |
|---|---|---|
| admin@barrowswifi.test | PlatformAdmin | Barrows WiFi |
| ops@kwelacola.test | ClientAdmin | Kwela Cola |
| viewer@kwelacola.test | ClientViewer | Kwela Cola |
| ops@shisalager.test | ClientAdmin | Shisa Lager |
| ops@ubuntubank.test | ClientAdmin | Ubuntu Bank |
| ops@freshmart.test | ClientAdmin | FreshMart |

All brands, outlets, people and phone numbers in this repo are made up.

## Tests

```bash
cd api && dotnet test
cd web/admin && npm test
```

## Ground rules

- Use any tools you normally use, including AI assistants.
- Commit as you go. Small commits with clear messages are part of what we look at.
- Write `HANDOVER-NOTES.md` at the root before you finish (your brief explains what goes in it).
