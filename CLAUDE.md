# CLAUDE.md

Guidance for Claude Code when working in this repository. Keep this file's **Current context**
section up to date as the code changes.

## Project

ProgCompJOliva API — backend for a Chilean competitive-programming community platform.
ASP.NET Core (**.NET 10**), **EF Core** on **PostgreSQL**, **JWT** auth. Aggregates per-user
ratings from multiple online judges.

Full documentation lives in [README.md](README.md). Read it for the complete API reference,
data model, and setup. This file is the short orientation; README is the source of truth.

## Layout

```
ProgCompJOlivaApi/
├── Controllers/   # endpoints by area, each with a Dtos/ subfolder
├── Models/        # EF Core entities
├── Data/          # AppDbContext + DbDevSeeder
├── Services/      # JwtTokenService, PasswordService, PeriodicWorker (AtCoder ratings, 1 min),
│                  #   CsesProblemImportService (startup CSES import),
│                  #   CodeforcesWorker (all CF: ratings + gym solve sync, 5 min; ADDCODEFORCES import)
├── JudgeClients/  # one client per online judge (IJudgeClient)
└── Program.cs     # composition root
```

## Conventions

- Controllers use primary-constructor DI and `[Authorize(Roles = "Admin")]` for management endpoints.
- Two roles only today: `Admin`, `User` (`Controllers/Constants.cs`).
- DB tables are snake_case; migrations auto-apply on startup (`db.Database.Migrate()`).
- Users are soft-deleted (`IsActive` / `DeletedAtUtc`), not removed.
- Per-judge ratings are cached on `User` and refreshed by `PeriodicWorker` every minute.
- Shell scripts are LF-only (`.gitattributes`): `docker/postgres/*.sh` are bind-mounted into
  Linux containers, so CRLF breaks them. Don't introduce CRLF.

## Run

- Docker: `docker compose up --build` → API at http://localhost:5029 (Postgres published on
  host port **55432** to avoid clashing with a local Postgres on 5432).
- Local: `cd ProgCompJOlivaApi && dotnet run` (needs Postgres on `localhost:5432`).
- Dev seed users have password `123456` (admins: `JOliva`, `MrYhatoh`). The Docker DB seeds
  users/orgs and (on startup) all CSES problems; create contests/trainings via the API.
- `dotnet run -- ADDCODEFORCES` (CLI flag) or `ADDCODEFORCES=true` (env/config) triggers the
  one-shot Codeforces gym import on that startup. Needs `Codeforces:Key/Secret` configured.
- `docker-compose.yml` passes `Codeforces__Key/Secret`, `Cses__SessionCookie` and `ADDCODEFORCES`
  through to the `api` container from the host environment (empty defaults; secrets never committed).

## Current context

_Last reflects: 40 endpoints across 10 controllers; verified end-to-end on PostgreSQL 18._

> Branch `feature/tasks-contests-trainings-standings` adds problem metadata, solve tracking,
> full contest/training management and standings, plus a Codeforces gym registry (all built
> and runtime-verified).

- **Ready:** auth/login, user CRUD (soft-delete), public rankings, organization CRUD + logo
  upload, navigation-context, Codeforces + AtCoder ratings sync, background worker, Docker.
- **Problems:** `Problem` has `StatementPath`, `Keywords` (`text[]`) and `Topics` (M2M via
  `Topic`). Endpoints: task search `GET /api/problem` (text/judge/topic/difficulty filters +
  paging), `GET /api/problem/{id}`, per-judge create (`/codeforces`, `/atcoder`, `/cses`),
  `PATCH /api/problem/{id}`, solve toggle `PUT /api/problem/{id}/solved`. Topics at `GET /api/topic`.
- **Contests:** search/list, detail, create-from-list, `PATCH`, add/remove/reorder problems,
  standings (`GET /api/contest/{id}/standings`).
- **Trainings:** search/list (replaces the old placeholder), detail, create-from-list, `PATCH`,
  add/remove/reorder contests, global standings (`GET /api/training/{id}/standings`,
  solved-per-contest + total). Slugs auto-generated.
- **Codeforces gyms:** `CodeforcesGym` registry (`GymContestId`, `FetchMethod` enum stored as
  string — only `Standings`, `Enabled`) with admin CRUD at `/api/codeforces-gym`. Creating a
  Codeforces task auto-registers its gym here (idempotent; existing gyms untouched).
- **Codeforces worker:** `CodeforcesWorker` is the single owner of all CF API access (they share
  the server IP / one rate budget). One 5-min loop does ratings refresh + gym solve sync (marks
  `UserProblemStatus` by handle, solved-only); the `ADDCODEFORCES` flag adds a one-shot gym import
  (problems + gym registration) first. `CodeforcesClient` does signed `contest.standings`,
  key/secret from config (`Codeforces:Key/Secret`), all CF calls throttled ≥5s apart process-wide
  with retry on transient 5xx/429. AtCoder ratings stay in `PeriodicWorker` (1 min).
- **CSES solved scraper:** `CsesSolvedScraper` (DI singleton) scrapes a user's solved CSES task
  ids from `/problemset/user/{id}/` using a service-account cookie (`Cses:SessionCookie` — set
  via user-secrets / `Cses__SessionCookie` env, never committed). Endpoint
  `GET /api/cses/user/{id}/solved` (Admin). Ids match CSES `Problem.ExternalId`. Not yet wired
  to `UserProblemStatus` (offered next step).
- **CSES problemset auto-import:** `CsesProblemImportService` (hosted, one-shot per startup)
  scrapes the public CSES list (`CsesProblemsetScraper`) and inserts any missing CSES problems.
  Idempotent; CSES outages are logged and swallowed (never block startup).
- **Stub:** CSES / LeetCode / CodeChef / Luogu *rating* clients return `0`; no Coach role, Teams,
  or real ICPC/IOI eligibility.
- **Conventions added this branch:** reorder endpoints take the full ordered id list
  (`Common/OrderedIdsRequest`); list endpoints return `Common/PagedResult<T>`; judge strings
  live in `Controllers/Constants.cs` (`Judges`); "task" == the `Problem` entity.
- **Auth:** task browse is public (`GET /api/problem`, `/api/problem/{id}`, `/api/topic` are
  `[AllowAnonymous]`); create/update/solve stay protected. Login honours `SessionDuration`
  (`One`=1d, `Thirty`=30d, `Forever`=10y) via `JwtTokenService.CreateAccessToken(user, lifetime)`.
- **Known bugs (pre-existing):** `RestoreUser` sets `IsActive=false` (doesn't restore);
  `ModifyUser` nickname-uniqueness check tests the old nickname and ignores
  `Password`/`DateOfBirth`/`CsesId`/`Roles`. See README "Known issues".

> When you add, remove, or change code, update the lists above so this stays accurate.
