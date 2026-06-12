# ProgCompJOliva API

Backend for a competitive-programming community platform (Chile-focused). It manages
users, organizations (universities), problems, contests and trainings, and aggregates
each user's ratings across multiple online judges (Codeforces, AtCoder, etc.).

Built with **ASP.NET Core (.NET 10)**, **Entity Framework Core** on **PostgreSQL**, and
**JWT bearer** authentication.

---

## Table of contents

- [Feature status](#feature-status)
- [Architecture overview](#architecture-overview)
- [Getting started](#getting-started)
- [Configuration](#configuration)
- [Authentication & roles](#authentication--roles)
- [API reference](#api-reference)
- [Data model](#data-model)
- [Judge clients](#judge-clients)
- [Background jobs](#background-jobs)
- [Seed data](#seed-data)
- [Known issues / rough edges](#known-issues--rough-edges)

---

## Feature status

Legend: ✅ ready · 🟡 partial / placeholder · ⛔ stub (not implemented)

### Ready ✅

| Feature | Notes |
|---|---|
| **JWT authentication** | `POST /api/auth/login`, HMAC-SHA256, role claims; 1-hour access token + refresh token (`POST /api/auth/refresh`), refresh lifetime = `SessionDuration` |
| **User management** | Create, modify, soft-delete users (Admin only) |
| **User rankings** | Public endpoint, sorted by Codeforces rating |
| **Organization management** | Create / modify universities incl. logo upload (Admin only) |
| **Problems (tasks)** | Per-judge creation (Codeforces / AtCoder / CSES), metadata (judge, difficulty, topics, keywords, statement folder path), `PATCH` updates (Admin only) |
| **Task search** | `GET /api/problem` — text / judge / topic / difficulty filters, paginated; `GET /api/topic` for the topic list |
| **Solve tracking** | `PUT /api/problem/{id}/solved` records `UserProblemStatus` (self, or another user for Admin) |
| **Contest management** | Search, detail, create-from-list, add / remove / reorder problems (Admin) |
| **Training management** | Search, detail, create-from-list, add / remove / reorder contests (Admin) |
| **Standings** | Per-contest standings and global per-training standings (solved per contest + total) |
| **Codeforces gym registry** | Admin CRUD list of gyms; `POST /api/codeforces-gym/{id}/import` adds a gym **and** imports its problems + starts standings tracking |
| **CSES solved scraper** | Scrapes a user's solved CSES tasks via a service-account session cookie; `GET /api/cses/user/{id}/solved`. Task ids match CSES `Problem.ExternalId` |
| **CSES problemset auto-import** | On every startup a background service adds any CSES problems missing from the DB (scraped from the public list); idempotent |
| **Codeforces worker** | One coordinated worker (every 5 min) owns all CF access: ratings refresh + gym solve sync (marks `UserProblemStatus` by handle); plus a one-shot gym import with the `ADDCODEFORCES` flag. Shared ≥5s rate gate + transient-error retry |
| **Navigation context** | `GET /api/me/navigation-context` — drives the frontend menu/permissions |
| **Codeforces ratings sync** | Live via official Codeforces API |
| **AtCoder ratings sync** | Live via HTML scraping (Chile-filtered rankings) |
| **Background ratings worker** | AtCoder ratings every minute (`PeriodicWorker`); Codeforces ratings via `CodeforcesWorker` (every 5 min) |
| **PostgreSQL persistence** | EF Core, auto-migrate on startup, dev seeder |
| **Docker / docker-compose** | Postgres + DB reset + API, one-command bring-up |

### Partial 🟡

| Feature | Notes |
|---|---|
| **Statement folders** | `Problem.StatementPath` is stored and returned, but there is no upload/import endpoint that populates the folder (images, samples, `hints.md`) yet — set it via `PATCH`. |
| **User restore** | `POST /api/users/{nickname}` exists but contains a bug (see [Known issues](#known-issues--rough-edges)). |
| **Solve auto-sync** | Solves are recorded manually via the endpoint; there's no job that derives them from judge submissions yet. |

### Stub / not implemented ⛔

| Feature | Notes |
|---|---|
| **CSES ratings** | Client returns `0` for all handles; scraper file is empty. |
| **LeetCode ratings** | Client returns `0` for all handles. |
| **CodeChef ratings** | Client returns `0` for all handles. |
| **Luogu ratings** | Client returns `0` for all handles. |
| **IOI / ICPC eligibility logic** | `IcpcEligible` is hard-coded to `true` in rankings; no real eligibility rules. |
| **Teams** | Listed in `Features.txt` but no model or endpoints. |

---

## Architecture overview

```
ProgCompJOlivaApi/
├── Controllers/            # HTTP endpoints, grouped by area (each with its own Dtos/)
│   ├── Authentication/     # login
│   ├── Common/             # shared DTOs (PagedResult, OrderedIdsRequest)
│   ├── Contests/
│   ├── Me/                 # navigation context for the frontend
│   ├── Organizations/
│   ├── Problems/
│   ├── Topics/
│   ├── Trainings/
│   └── Users/
├── Models/                 # EF Core entities
├── Data/                   # AppDbContext + DbDevSeeder
├── Services/               # JwtTokenService, PasswordService, PeriodicWorker
├── JudgeClients/           # one client per online judge
├── Migrations/             # EF Core migrations
├── SeedData/               # organization logos copied to wwwroot on first run
└── Program.cs              # composition root
```

---

## Getting started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/)
- PostgreSQL 18 (or use the bundled Docker setup)

### Run with Docker (recommended)

```bash
docker compose up --build
```

This starts three services:

1. `postgres` — PostgreSQL 18 (`progcomp-postgres`, port `5432`)
2. `db-reset` — one-shot job that creates the `progcomp_backend` database and `progcomp_user` role
3. `api` — the API itself, available at **http://localhost:5029**

On startup the API applies EF Core migrations and seeds dev data (see [Seed data](#seed-data)).

### Run locally

1. Start a PostgreSQL instance matching the dev connection string (see [Configuration](#configuration)).
2. From the project directory:

   ```bash
   cd ProgCompJOlivaApi
   dotnet run
   ```

3. Profiles (from `Properties/launchSettings.json`):
   - **http** → http://localhost:5029
   - **https** → https://localhost:7265 + http://localhost:5029

OpenAPI is exposed in the Development environment via `MapOpenApi()` (`/openapi/v1.json`).

---

## Configuration

Settings live in `appsettings.json` and environment-specific overrides
(`appsettings.Development.json`, `appsettings.Docker.json`).

### Connection string

`ConnectionStrings:DefaultConnection`

| Environment | Host | Database | User |
|---|---|---|---|
| Development | `localhost:5432` | `progcomp_backend` | `progcomp_user` |
| Docker | `postgres:5432` | `progcomp_backend` | `progcomp_user` |

### JWT (`Jwt` section)

| Key | Value |
|---|---|
| `Issuer` | `ProgCompJOlivaApi` |
| `Audience` | `ProgCompJOlivaClient` |
| `AccessTokenMinutes` | `30` |
| `Key` | symmetric signing secret (do **not** commit a real secret; move to user-secrets / env in production) |

### CORS

A single policy named `Frontend` allows **any origin, header, and method**. Tighten this
before production.

---

## Authentication & roles

Authentication is JWT bearer. Obtain a token via `POST /api/auth/login`, then send it on
subsequent requests:

```
Authorization: Bearer <AccessToken>
```

### Roles

The codebase recognizes two roles (`Controllers/Constants.cs`):

- `Admin` — full management access (create/modify users, organizations, problems, contests, trainings)
- `User` — standard authenticated user

> Note: `Features.txt` envisions four user types (User, Coach, Admin, NotAUser). Only
> `Admin` and `User` are implemented today; **Coach** does not exist yet, and unauthenticated
> access is simply "no token".

Token claims include: subject/name identifier (user id), unique name/name (nickname), and
one `role` claim per assigned role.

---

## API reference

Base URL: `http://localhost:5029`

All request/response bodies are JSON unless noted (organization endpoints use
`multipart/form-data`).

### Auth — `/api/auth`

#### `POST /api/auth/login` — _anonymous_

Authenticate and receive a short-lived **access token** plus a long-lived **refresh token**.

Request:
```json
{
  "nickname": "JOliva",
  "password": "123456",
  "sessionDuration": "One"
}
```

Response `200`:
```json
{
  "accessToken": "eyJ...",
  "accessTokenExpiresAtUtc": "2026-06-05T12:30:00Z",
  "refreshToken": "eyJ...",
  "refreshTokenExpiresAtUtc": "2026-06-06T11:30:00Z",
  "roles": ["Admin"]
}
```

- **Access token** lasts **1 hour** — send it as `Authorization: Bearer <accessToken>`.
- **Refresh token** lasts the requested `sessionDuration` (`One`=1 day, `Thirty`=30 days,
  `Forever`=10 years). Keep it; use it to get new access tokens.

#### `POST /api/auth/refresh` — _anonymous_

Exchange a valid refresh token for a fresh access token. Request `{ "refreshToken": "eyJ..." }`;
response is the same shape as login (a new `accessToken`, the same refresh token echoed back).
Returns `401` if the refresh token is missing, expired, or not a refresh token.

> Tokens are tagged `token_use=access|refresh` and kept separate: a refresh token is **rejected
> as a bearer credential**, and an access token is rejected at `/refresh`. Tokens are stateless
> signed JWTs (no server-side revocation), so a leaked refresh token is valid until it expires.

### Me — `/api/me`

#### `GET /api/me/navigation-context` — _anonymous_

Returns auth state, role-based view/action permissions, and navigation items for the
frontend. Works with or without a token (richer result when authenticated).

Response `200` (shape):
```json
{
  "isAuthenticated": true,
  "roles": ["Admin"],
  "permissions": {
    "views":   { "notes": true, "ranking": true, "training": true,
                 "contests": true, "social": true, "admin": true },
    "actions": { "createUser": true, "createOrganization": true }
  },
  "navigationData": {
    "trainingItems": [{ "label": "...", "to": "..." }],
    "socialItems":   [{ "label": "...", "to": "..." }]
  }
}
```

### Users — `/api/users`

| Method | Path | Auth | Description |
|---|---|---|---|
| `POST` | `/api/users` | Admin | Create a user |
| `PATCH` | `/api/users/{nickname}` | Admin | Modify an active user (partial update) |
| `DELETE` | `/api/users/{nickname}` | Admin | Soft-delete (sets `IsActive=false`, stamps `DeletedAtUtc`) |
| `POST` | `/api/users/{nickname}` | Admin | Restore a deleted user (⚠️ see Known issues) |
| `GET` | `/api/users/rankings` | _public_ | List users ranked by Codeforces rating, then nickname |

**Create user** request:
```json
{
  "nickname": "newuser",
  "password": "secret",
  "email": "user@example.com",
  "names": "First",
  "surnames": "Last",
  "dateOfBirth": "2000-01-31",
  "organizationShortName": "UChile",
  "femTeamEligible": false,
  "isCompetitiveProgrammingActive": false,
  "codeforcesHandle": "tourist",
  "atcoderHandle": null,
  "csesHandle": null,
  "csesId": null,
  "codeChefHandle": null,
  "luoguHandle": null,
  "leetCodeHandle": null,
  "roles": ["User"]
}
```
- `organizationShortName` must reference an existing organization (required in practice).
- `roles` defaults to `["User"]` if empty; each role must be in the allowed set (`Admin`, `User`).
- Returns `409 Conflict` if the nickname is taken.

**Modify user**: same fields, all optional; only provided fields are updated.

**Rankings** response item:
```json
{
  "id": "1",
  "nickname": "JOliva",
  "fullName": "Javier Oliva",
  "university": "UChile",
  "universityLogo": "/organizations/logos/UChile.png",
  "femTeamEligible": false,
  "isActive": true,
  "icpcEligible": true,
  "ratings": {
    "codeforces": 2100, "atcoder": 1800, "cses": 0,
    "leetcode": 0, "codechef": 0, "luogu": 0
  }
}
```

### Organizations — `/api/organizations`

| Method | Path | Auth | Description |
|---|---|---|---|
| `POST` | `/api/organizations` | Admin | Create an organization (with logo) |
| `PATCH` | `/api/organizations/{shortName}` | Admin | Modify an organization |

`multipart/form-data` fields:

- Create: `Name`, `ShortName`, `Logo` (file)
- Modify: `NewName?`, `NewShortName?`, `NewLogo?` (all optional)

Logo constraints: `.jpg` / `.jpeg` / `.png`, up to **2 MB**. Files are stored under
`wwwroot/organizations/logos/` and served as `/organizations/logos/{guid}.{ext}`.

### Problems (tasks) — `/api/problem`

> In this codebase a "task" is the `Problem` entity. Codeforces problems come from gyms,
> AtCoder from contests, CSES from the global problemset.

| Method | Path | Auth | Description |
|---|---|---|---|
| `GET` | `/api/problem` | _public_ | Search/list tasks (see query params below) |
| `GET` | `/api/problem/{id}` | _public_ | Task detail |
| `POST` | `/api/problem/codeforces` | Admin | Create a Codeforces task |
| `POST` | `/api/problem/atcoder` | Admin | Create an AtCoder task |
| `POST` | `/api/problem/cses` | Admin | Create a CSES task |
| `PATCH` | `/api/problem/{id}` | Admin | Update metadata (partial) |
| `PUT` | `/api/problem/{id}/solved` | Authenticated | Record solved status |

Each problem reserves an (empty) statement folder at `StatementPath` (`/statements/{judge}/{externalId}/`,
served from `wwwroot`) for future statement content; nothing is fetched yet.

**Search query params:** `search` (matches title / external id / topic / keyword, case-insensitive),
`judge`, `topic`, `minDifficulty`, `maxDifficulty`, `onlyActive` (default `true`),
`sort` (`title` \| `difficulty` \| `created`), `sortDir` (`asc` \| `desc`), `page`, `pageSize`
(≤ 200). Returns a `PagedResult<T>`: `{ items, page, pageSize, total, totalPages }`.

**Create (Codeforces)** — also accepts `keywords`, `topics`, `statementPath`:
```json
{
  "title": "Theatre Square", "url": "https://codeforces.com/gym/100001/problem/A",
  "contestId": 100001, "contestProblemId": "A", "difficulty": 1000,
  "tagsJson": "[\"math\"]", "keywords": ["geometry","ceil"],
  "topics": ["math","implementation"], "statementPath": "/problems/cf/100001/A"
}
```
**Create (AtCoder)**: `{ title, url, contestId: "abc300", taskId: "abc300_a", difficulty?, keywords?, topics?, statementPath? }`
**Create (CSES)**: `{ title, url, csesId: "1068", difficulty?, keywords?, topics?, statementPath? }`
All reject duplicates (by judge identifiers or URL) with `409`. Topics are created on demand.

**Update** (`PATCH`): all fields optional; `null` leaves a field unchanged, while an empty
`keywords`/`topics` list clears it.

**Solve** (`PUT /{id}/solved`): `{ "isSolved": true, "userNickname": null, "notes": null }`.
Defaults to the caller; only an Admin may set it for another user via `userNickname`.

#### Topics — `/api/topic`

`GET /api/topic` (_public_) → `[{ id, name, problemCount }]`, for filter UIs.

### Contests — `/api/contest`

| Method | Path | Auth | Description |
|---|---|---|---|
| `GET` | `/api/contest` | Authenticated | Search contests by name (`search`, `onlyActive`, `sortDir`, `page`, `pageSize`) |
| `GET` | `/api/contest/{id}` | Authenticated | Detail with problems ordered by position |
| `POST` | `/api/contest` | Admin | Create from a list: `{ name, isActive?, problemIds: [] }` |
| `PATCH` | `/api/contest/{id}` | Admin | Rename / toggle active |
| `POST` | `/api/contest/{id}/problems` | Admin | Add a problem: `{ problemId, position? }` (defaults to end) |
| `DELETE` | `/api/contest/{id}/problems/{problemId}` | Admin | Remove a problem (positions repacked) |
| `PUT` | `/api/contest/{id}/problems/order` | Admin | Reorder: `{ orderedIds: [] }` (permutation of current) |
| `GET` | `/api/contest/{id}/standings` | Authenticated | Per-user solved counts for this contest |

**Contest standings** response: `{ contestId, contestName, problems: [{ problemId, position,
title, judge }], rows: [{ nickname, fullName, university, solvedCount, solvedProblemIds }] }`.
`problems` is the contest's problem set ordered by position; `rows` has **one entry only per user
who solved ≥1 problem** (active users), each listing the `solvedProblemIds` they solved, sorted by
`solvedCount` desc then nickname.

### Trainings — `/api/training`

A training is an ordered set of contests.

| Method | Path | Auth | Description |
|---|---|---|---|
| `GET` | `/api/training` | Authenticated | Search trainings (`search`, `onlyActive`, `onlyPublic`, `sortDir`, `page`, `pageSize`) |
| `GET` | `/api/training/{id}` | Authenticated | Detail with contests ordered by position |
| `POST` | `/api/training` | Admin | Create from a list: `{ name, description?, isPublic?, isActive?, contestIds: [] }` |
| `PATCH` | `/api/training/{id}` | Admin | Update name / description / public / active |
| `POST` | `/api/training/{id}/contests` | Admin | Add a contest: `{ contestId, position? }` |
| `DELETE` | `/api/training/{id}/contests/{contestId}` | Admin | Remove a contest (positions repacked) |
| `PUT` | `/api/training/{id}/contests/order` | Admin | Reorder: `{ orderedIds: [] }` |
| `GET` | `/api/training/{id}/standings` | Authenticated | Global standings (solved per contest + total) |

A unique `slug` is generated from the name on creation.

**Training standings** response: `{ trainingId, trainingName, contests: [{ contestId, name,
position, problemCount }], rows: [{ nickname, fullName, university, perContest: [{ contestId,
solved }], total }] }`, sorted by `total` desc then nickname. Each row's `perContest` lines up
with the `contests` order.

### Codeforces gyms — `/api/codeforces-gym`

A registry of Codeforces gyms to use as problem sources. Each gym records **how** it should be
fetched (`fetchMethod`, a string enum — currently only `Standings`). **All endpoints are Admin-only.**

The main entry point is **import**: `POST /api/codeforces-gym/{gymContestId}/import` registers the
gym (enabled, so the solve sync starts tracking it) **and** imports its problems via the Codeforces
API in one call (idempotent — re-importing only adds new problems). Response:
`{ gymContestId, name, addedProblems, totalProblems, gymWasNew }`. It makes one rate-limited
Codeforces call, so it can take a few seconds; `502` on Codeforces errors / missing credentials.
Creating a Codeforces task also auto-registers its gym (without importing).

| Method | Path | Description |
|---|---|---|
| `POST` | `/api/codeforces-gym/{gymContestId}/import` | **Add a gym + import its problems** |
| `GET` | `/api/codeforces-gym` | List gyms (`onlyEnabled`, `search` by name or gym id) |
| `GET` | `/api/codeforces-gym/{id}` | Gym detail |
| `GET` | `/api/codeforces-gym/fetch-methods` | Available fetch-method names (for a dropdown) |
| `POST` | `/api/codeforces-gym` | Register a gym (no import) |
| `PATCH` | `/api/codeforces-gym/{id}` | Edit a gym (partial) |
| `DELETE` | `/api/codeforces-gym/{id}` | Remove a gym |

**Create** `{ "gymContestId": 100001, "name": "…", "fetchMethod": "Standings", "enabled": true }`
— `gymContestId` must be positive and unique (`409` on duplicate); `fetchMethod` defaults to
`Standings` and is validated (`400` on unknown value); `name` is optional.

### CSES — `/api/cses`

CSES shows solved tasks only to a logged-in session, but a logged-in account can view any
user's statistics page — so the backend uses **one service-account session cookie** to scrape
any user. Configure it via `Cses:SessionCookie` (the `PHPSESSID` value) in user-secrets or the
`Cses__SessionCookie` env var — **never commit it**.

| Method | Path | Auth | Description |
|---|---|---|---|
| `GET` | `/api/cses/user/{csesUserId}/solved` | Admin | Solved CSES task ids for that CSES user |

Response: `{ csesUserId, solvedCount, taskIds: [] }`. The ids match `Problem.ExternalId` for
CSES problems, so they can drive `UserProblemStatus`. Returns `502` if the cookie is missing
or has expired.

---

## Data model

PostgreSQL via EF Core. Tables use snake_case names. Key entities:

- **User** (`users`) — identity, contact, per-judge handles **and** cached ratings
  (`CodeforcesRating`, `AtcoderRating`, `CsesRating`, `LeetCodeRating`, `CodeChefRating`,
  `LuoguRating`), `FemTeamEligible`, `IsCompetitiveProgrammingActive`, soft-delete
  (`IsActive`, `DeletedAtUtc`), `PasswordHash`. Unique index on `Nickname`.
- **Organization** (`organizations`) — `Name` (unique), `ShortName` (unique), `LogoUrl`.
  One organization → many users (delete sets user's org to null).
- **UserRole** (`user_roles`) — `(UserId, RoleName)` unique; cascade-deleted with the user.
- **Problem** (`problems`) — `Judge`, `ExternalId`, `Title`, `Url`, optional `ContestId`,
  `ContestProblemId`, `Difficulty`, `TagsJson`, `StatementPath`, `Keywords` (`text[]`), and a
  many-to-many `Topics`. Indexed on `Judge`, `Difficulty`, `ExternalId`.
- **Topic** (`topics`) — `Name` (unique); many-to-many with problems via `problem_topics`.
- **CodeforcesGym** (`codeforces_gyms`) — `GymContestId` (unique), `Name`, `FetchMethod`
  (`GymFetchMethod` enum stored as string), `Enabled`. Registry of gym problem sources.
- **Contest** (`contests`) and **ContestProblem** (`contest_problems`) — contest with an
  ordered list of problems.
- **Training** (`trainings`) and **TrainingContest** (`training_contests`) — training with
  an ordered list of contests; many-to-many with users.
- **UserProblemStatus** (`user_problem_statuses`) — per-user solve state (`IsSolved`,
  `SolvedAtUtc`, `Notes`); unique per `(UserId, ProblemId)`. Written via
  `PUT /api/problem/{id}/solved` and read by the standings endpoints.

Migrations are applied automatically at startup (`db.Database.Migrate()`).

---

## Judge clients

Each judge implements `IJudgeClient` (`GetUsersRatings(handles, ct) → Dictionary<handle,int>`).

| Judge | Status | Source |
|---|---|---|
| **Codeforces** | ✅ implemented | Official API: `user.info` (ratings) + signed `contest.standings` (gym problems & solves). Key/secret from config; all calls throttled ≥5s apart |
| **AtCoder** | ✅ implemented | HTML scraping of Chile-filtered rankings (HtmlAgilityPack) |
| CSES | ✅ rating = **number of solved problems** (`CsesWorker`, every 10 min, via `CsesSolvedScraper` + service-account cookie); 0 until the cookie is configured |
| LeetCode | ⛔ stub | returns 0 |
| CodeChef | ⛔ stub | returns 0 |
| Luogu | ⛔ stub | returns 0 |

> The Codeforces API key/secret now come from configuration (`Codeforces:Key` / `Codeforces:Secret`
> via user-secrets or env vars), not source.

---

## Background jobs

`PeriodicWorker` (a hosted `BackgroundService`) runs **every 1 minute** and refreshes **AtCoder**
ratings (scraped from the Chile-filtered rankings). It's wrapped in try/catch so a failure doesn't
stop the worker. (Codeforces lives in its own worker — see below; other judges are stubs.)

`CsesProblemImportService` is a separate one-shot hosted service that runs **once at each
startup**: it scrapes the public CSES problemset list and inserts any tasks missing from the
`problems` table (matched by `ExternalId` among CSES problems). It's idempotent — existing
problems are untouched — and failures (e.g. CSES unreachable) are logged and swallowed so they
never block or crash startup.

### Codeforces worker

**`CodeforcesWorker`** is the **single owner of all Codeforces API access** — because every
request leaves from the same server IP (one CF rate-limit budget), ratings and solve-sync run in
one coordinated loop instead of competing background jobs. All CF calls go through a process-wide
gate: **one at a time, ≥5s apart** (measured after each response), and transient responses
(500/502/503/504/429 — Codeforces behind Cloudflare 502s intermittently) are **retried up to 4×**.

Configure credentials via `Codeforces:Key` / `Codeforces:Secret` (user-secrets or the
`Codeforces__Key` / `Codeforces__Secret` env vars — **never commit them**). Ratings work without
credentials; gym problem/solve sync (signed `contest.standings`) needs them.

Each cycle (**every 5 minutes**, after the previous finishes) the worker:
1. Refreshes `CodeforcesRating` for users with a handle (`user.info`).
2. For each enabled gym in the registry, fetches standings for the users' handles and marks solved
   problems in `UserProblemStatus` (sets solved only; never un-solves).

When the API is started with the **`ADDCODEFORCES` flag** (e.g. `dotnet run -- ADDCODEFORCES`),
the worker first runs a one-shot import of the configured gym contests: registering each gym and
inserting its problems via signed `contest.standings`. Idempotent.

---

## Seed data

`DbDevSeeder` runs on startup **only when the users table is empty**. It:

- Copies `SeedData/` (logos) into `wwwroot/`.
- Creates 4 organizations: **UChile**, **UTFSM**, **PUC**, **UDEC**.
- Creates 5 users, all with password **`123456`**:
  - `JOliva` (Admin), `MrYhatoh` (Admin)
  - `Dmitri`, `abner_vidal`, `Scarl3th` (Users)

> These are development credentials. Never run this seeder against production data.

---

## Known issues / rough edges

These are real, verified observations from the current code — worth fixing:

1. **`RestoreUser` doesn't restore.** `POST /api/users/{nickname}`
   ([UserController.cs:206](ProgCompJOlivaApi/Controllers/Users/UserController.cs#L206))
   sets `IsActive = false` and stamps `DeletedAtUtc` — the same as delete. It should set
   `IsActive = true` and clear `DeletedAtUtc`.
2. **`ModifyUser` nickname-uniqueness check is wrong.** It checks whether the *current*
   nickname exists (always true) instead of the requested new one, so a nickname change is
   effectively blocked / inconsistent
   ([UserController.cs:122](ProgCompJOlivaApi/Controllers/Users/UserController.cs#L122)).
3. **`ModifyUser` ignores `Password`, `DateOfBirth`, `CsesId`, and `Roles`** even though the
   DTO accepts them — they're never applied.
4. **`icpcEligible` is hard-coded `true`** in the rankings response; there's no eligibility logic.
5. **Secrets in source:** the JWT signing key still lives in appsettings — move it to
   user-secrets / environment variables. (Codeforces key/secret and the CSES cookie are
   already config-only.)
6. **CORS is wide open** (`AllowAnyOrigin/Header/Method`) — scope it for production.

> Fixed on branch `feature/tasks-contests-trainings-standings`: the `NavigtationItemDto`
> compile-breaking typo; the `GET /api/training` placeholder (now a real search endpoint); and
> the 30-minute hardcoded session — login now issues a 1-hour access token plus a refresh token
> whose lifetime is the requested `SessionDuration` (One=1d / Thirty=30d / Forever=10y). The
> `RestoreUser` / `ModifyUser` bugs above are still open.

---

_Generated from source on 2026-06-05. Endpoint count: 40 across 10 controllers (Auth, Me, Users,
Organizations, Problems, Topics, Contests, Trainings, CodeforcesGyms, Cses)._
