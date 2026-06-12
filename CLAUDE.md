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
  host port **15432** — below the Windows/WinNAT reserved range and clear of a local Postgres on 5432).
- Local: `cd ProgCompJOlivaApi && dotnet run` (needs Postgres on `localhost:5432`).
- Dev seed users have password `123456` (admins: `JOliva`, `MrYhatoh`). The Docker DB seeds
  users/orgs and (on startup) all CSES problems; create contests/trainings via the API.
- `dotnet run -- ADDCODEFORCES` (CLI flag) or `ADDCODEFORCES=true` (env/config) triggers the
  one-shot Codeforces gym import on that startup. Needs `Codeforces:Key/Secret` configured.
- `docker-compose.yml` passes `Codeforces__Key/Secret`, `Cses__SessionCookie` and `ADDCODEFORCES`
  through to the `api` container from the host environment (empty defaults; secrets never committed).

## Current context

_Last reflects: 46 endpoints across 11 controllers; verified end-to-end on PostgreSQL 18._

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
  standings (`GET /api/contest/{id}/standings` — ordered `problems` list, now carrying
  judge/title/url/externalId/difficulty per problem, + one row per user with ≥1 solve, listing
  the problem ids they solved).
- **Trainings:** search/list (replaces the old placeholder), detail, create-from-list, `PATCH`,
  add/remove/reorder contests, global standings (`GET /api/training/{id}/standings`,
  solved-per-contest + total). Slugs auto-generated. New `TrainingParticipants` M2M
  (`training_participants` join table, explicit `TrainingParticipant` entity; separate from the
  legacy implicit `Training.Users` relation that `MeController` still uses). `POST /api/training`
  accepts `participantNicknames` (resolved to active users, 400 on any unknown). `GET /api/training/mine`
  (`[Authorize]`) returns the caller's participant trainings as a plain `List<TrainingListItemDto>`.
  `PUT /api/training/{id}/participants` (Admin) replaces the participant set from a nickname list.
  Training read endpoints (search, detail, standings) are `[AllowAnonymous]`: anonymous callers
  only see **public** trainings (`IsPublic`); private ones 404 for them. Authenticated callers see all.
- **Codeforces gyms:** `CodeforcesGym` registry (`GymContestId`, `FetchMethod` enum stored as
  string — only `Standings`, `Enabled`) with admin CRUD at `/api/codeforces-gym`. Creating a
  Codeforces task auto-registers its gym here (idempotent; existing gyms untouched).
  `POST /api/codeforces-gym/{gymContestId}/import` (Admin) registers a gym + imports its problems
  in one call via `CodeforcesGymImporter` (also used by the `ADDCODEFORCES` startup import).
- **Problem statements:** each problem reserves an **empty** folder at `Problem.StatementPath`
  (`/statements/{judge}/{externalId}/`, gitignored, served from wwwroot) for future content — no
  scraping/fetching yet (deliberately deferred). `StatementStore.EnsureFolder` creates it; CSES/gym
  imports and per-judge creates set it, plus a startup pass (`CsesProblemImportService`) reserves
  one for any problem still missing it. (CF statements can't be scraped: pages are behind Cloudflare
  + gym login; the API has no statement endpoint.)
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
- **CSES rating:** `CsesWorker` (hosted, every 10 min) sets `User.CsesRating` = the user's number
  of solved CSES problems (via `CsesSolvedScraper` by `CsesId`; needs `Cses:SessionCookie`, else 0).
- **ICPC standings (.dat):** `Controllers/IcpcTraining/` parses DOMjudge/ICPC `.dat` files
  (`DatStandingsParser`) into standings (rank by solved desc, then penalty = solveMin + 20×wrong-
  before-AC; failed = `WA`/`TL`/`RT`/`RJ`; CE free, `??` ignored; per-cell `failedAttempts`; team
  names parse quoted or bare). `GET /api/icpc-standings/{org}/{year}` (public) returns a **list** of
  standings, one per phase ("fase") ordered by fase number (1 phase → length 1, 2 phases → length 2);
  `org` is normalized (`utfsm`==`usm`). `GET /api/icpc-standings/{key}` (public) serves one by key;
  `GET /api/icpc-standings` lists keys; `GET /api/icpc-standings/catalog` (public) returns the full
  catalog grouped org→year→fase (key + contest name per fase) so the front discovers which
  "selectivos" exist instead of hardcoding; `POST /api/icpc-standings/{key}` (Admin) uploads a `.dat`.
  `.dat` bytes are decoded UTF-8-first with a Latin-1 fallback (`DatStandingsParser.DecodeBytes`), so
  accented contest names (e.g. uchile2024 stores "ó" as Latin-1 `0xF3`) survive.
  Each row carries `org`/`year`/`fase`; team names are matched at request time to active users (by
  Codeforces handle, nickname, or real "Names Surnames", case-insensitive — the real-name match
  covers Kattis-sourced files that show full names) — matched rows get `registered=true`,
  `displayName` (real name), `nickname`, and `rating` (`CodeforcesRating`, so the front colors the
  name). Stored as JSON in `wwwroot/standings` (gitignored); `IcpcStandingsSeedService` seeds from
  `SeedData/standings/*.dat` at startup, **idempotently** (skips any key whose JSON already exists —
  delete the JSON to re-seed). Key→(org,year,fase) mapping in `IcpcStandingsKey`. Seeds:
  `usm2024-1`, `usm2024-2`, `usm2025-1`, `usm2025-2`, `usm2026`, `uchile2023`, `uchile2024`,
  `uchile2025`. (`usm2026` is a supplied `.dat` using Codeforces handles as team names, with the
  team "el jaime es poco serio" removed; ranks recompute on parse, so no renumbering was needed.)
- **OCI standings (school olympiad):** `Controllers/Oci/` serves OCI (Olimpiada Chilena de
  Informática) standings — **no penalty**; tasks have subtasks so each task is a partial score
  (0..maxScore) and `global` = sum across tasks. Editions are typed: `regional` / `nacional` /
  `clasificatoria` — the OCI's IOI qualifier; the bare `ioi` type is **reserved** for the international
  IOI (`OciStandingsKey`, key = `{type}{year}` e.g. `regional2022`, `clasificatoria2024`). Endpoints
  (all `[AllowAnonymous]` except upload): `GET /api/oci-standings/{type}` lists that type's editions
  (year+contest+`phases` count) — i.e. which Regionales / Nacionales / Clasificatorias exist;
  `GET /api/oci-standings/{type}/{year:int}` returns that edition as a **single** standings object
  (regional/nacional/single-phase clasificatoria); for a multi-phase clasificatoria it returns the
  weighted aggregate with the phases nested under `phases` (by phase);
  `GET /api/oci-standings/catalog` groups all by type→year (one entry per year); `GET /api/oci-standings`
  lists keys; `POST /api/oci-standings/{key}` (Admin) uploads standings **JSON**;
  `POST /api/oci-standings/{type}/{year}/csv` (Admin) uploads from a CSV whose columns are matched by
  **header name** (any order): `Nombre` + `TOTAL`/`Global` required; `Sede`/`Región`, `Clasifica`
  (1/0) and `Medalla` (`ORO/PLATA/BRONCE/NA`, nacional) optional; a `#`/rank column is ignored; every
  other column is a task. A **phase** CSV usually has no Sede/Clasifica/Medalla (→ `qualified=false`,
  empty sede). Optional `?contest=`. Replaces the matching standings if it exists — catalog/standings
  update immediately.
  For a **clasificatoria** pass `?phase=N` (a phase → `clasificatoria{year}-N`) or `?weighted=true`
  (the final → `clasificatoria{year}`, `weighted`); omit both for a single-phase clasificatoria;
  `phase`/`weighted` are rejected for regional/nacional. The admin uploads each phase + the weighted
  final as CSVs (the API stores them; it doesn't compute the weighting). A standings carries
  `type`, `year`, `phase` (nullable; only multi-phase clasificatoria), `weighted` (true on the aggregate),
  `weight` (a phase's share of the aggregate), `problems` (tasks, or phases on the weighted) and
  `rows`. Rows carry `rank` (nullable; `regional2022` ranks are computed contiguously by `global`
  desc, so `mgodoy` — printed without a number — sits 10th), `sede`, `username`, `user` (real name),
  `scores[]`, `global`, `qualified` (advanced to next stage: regional→nacional, nacional→training
  camp, clasificatoria→IOI), `medal` (nacional only: `oro`/`plata`/`bronce` or null, independent of `qualified`),
  plus the same request-time user match as ICPC (by username/nickname/handle or real name →
  `registered`/`displayName`/`nickname`/`rating`). Multi-phase clasificatoria keys: `clasificatoriaYYYY-1`,
  `clasificatoriaYYYY-2` (phases) + `clasificatoriaYYYY` (weighted); `OciStandingsKey` parses the optional
  phase suffix. Stored as JSON in `wwwroot/oci-standings` (gitignored); `OciStandingsSeedService`
  seeds from `SeedData/oci-standings/*.json` idempotently. The source data is **not** parsed in-app:
  each edition is transcribed offline into the seed JSON — from the PDF via `pdfplumber`, or from a
  provided CSV when one exists (`regional2018` was rebuilt from a CSV: 156 participants). Seeded
  regionales: `regional2018`, `regional2019`, `regional2022`, `regional2023`, `regional2024`,
  `regional2025` (each layout differs — column order, task names, `Sede`/`Región`/`Team`, with/without
  `#`/`Username`, decimal-comma scores in 2018, sort-arrow glyphs in 2019 headers; rows are validated
  by `global == sum(scores)` and cross-checked against the printed `#` count). `qualified` is **not** a
  score cutoff — it's a per-sede quota, read from the PDF's highlight rects (the darkest non-white fill,
  to avoid mistaking light alternating row-striping for qualification, as in 2019). NOTE: `SeedData/**/*.json`
  needed `<Content Remove="SeedData\**\*.json"/>` before the `Include` glob in the csproj (json is a
  default Content item, so the glob double-included it — `.dat` seeds didn't have this problem).
- **Stub:** LeetCode / CodeChef / Luogu *rating* clients return `0`; no Coach role, Teams, or real
  ICPC/IOI eligibility.
- **Conventions added this branch:** reorder endpoints take the full ordered id list
  (`Common/OrderedIdsRequest`); list endpoints return `Common/PagedResult<T>`; judge strings
  live in `Controllers/Constants.cs` (`Judges`); "task" == the `Problem` entity.
- **Auth:** task browse is public (`GET /api/problem`, `/api/problem/{id}`, `/api/topic` are
  `[AllowAnonymous]`); create/update/solve stay protected. Login returns a 1h access token +
  refresh token (lifetime = `SessionDuration`: `One`=1d, `Thirty`=30d, `Forever`=10y);
  `POST /api/auth/refresh` swaps a refresh token for a new access token. Tokens carry
  `token_use=access|refresh`; refresh tokens are rejected as bearer creds (JwtBearer
  `OnTokenValidated`) and access tokens rejected at `/refresh`. Stateless (no revocation).
  Dev skips HTTPS redirection (plain-HTTP SPA).
- **Known bugs (pre-existing):** `RestoreUser` sets `IsActive=false` (doesn't restore);
  `ModifyUser` nickname-uniqueness check tests the old nickname and still ignores
  `DateOfBirth`/`CsesId`/`Roles`. See README "Known issues". (`ModifyUser` **does** now apply
  `Password` — admins can reset a user's password via `PATCH /api/users/{nickname}` with a non-blank
  `password`; blank is ignored.)

> When you add, remove, or change code, update the lists above so this stays accurate.
