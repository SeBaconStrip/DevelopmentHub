# Codebase Context — DevelopmentHub

> **Purpose of this file:** Complete implementation memory dump for AI agents.
> Describes every file, every architectural decision, every gotcha, and how to continue work.
> Generated: 2026-03-04

---

## What this project is

A **local developer dashboard** running entirely on `localhost`. It has no authentication, no cloud deployment, and no multi-user concerns. It is a personal productivity tool for a single developer on Windows.

- **Backend:** ASP.NET Core Web API on .NET 9 (not .NET 8 — the installed SDK was 9.0.306)
- **Frontend:** React 19 + TypeScript + Vite 7, SWC compiler
- **Database:** MongoDB (driver 3.7.0). Default connection `mongodb://localhost:27017`, database `developmenthub`. No migrations — indexes are created idempotently on startup.
- **Real-time:** ASP.NET Core SignalR (built-in, no extra NuGet) for live script log streaming
- **Git:** LibGit2Sharp 0.31 for local repo reads; raw `git` CLI via `Process` for fetch/pull
- **Azure DevOps:** Direct `HttpClient` calls, PAT auth, REST API v7.1

---

## Repository layout

```
DevelopmentHub/
├── DevelopmentHub.sln
├── DevelopmentHub.code-workspace  ← open this in VS Code for tasks
├── .gitignore
├── .github/
│   └── prompts/
│       ├── plan-localDeveloperDashboard.prompt.md   ← original planning doc
│       └── codebase-context.prompt.md               ← THIS FILE
├── backend/                    ← ASP.NET Core 9 Web API
│   ├── DevelopmentHub.Api.csproj
│   ├── Program.cs
│   ├── appsettings.json
│   ├── appsettings.local.json  ← git-ignored; put secrets here
│   ├── BackgroundServices/
│   │   └── RepositoryScannerService.cs
│   ├── Configuration/
│   │   └── AppSettings.cs
│   ├── Controllers/
│   │   ├── ConfigController.cs
│   │   ├── PullRequestsController.cs
│   │   ├── RepositoriesController.cs
│   │   └── ScriptsController.cs
│   ├── Data/
│   │   └── DashboardDatabase.cs          ← MongoDB singleton: collections + index setup
│   ├── Hubs/
│   │   └── LogHub.cs
│   ├── Models/
│   │   ├── RepositoryEntity.cs
│   │   ├── ScriptExecution.cs
│   │   └── Dtos/
│   │       ├── ConfigDto.cs
│   │       ├── PullRequestDto.cs
│   │       ├── RepositoryDto.cs
│   │       └── ScriptDto.cs
│   ├── Properties/
│   │   └── launchSettings.json   ← backend listens on http://localhost:5131
│   └── Services/
│       ├── AzureDevOpsService.cs
│       ├── GitService.cs
│       ├── LauncherService.cs
│       ├── RepositoryService.cs
│       └── ScriptService.cs
└── frontend/                   ← React 19 + TypeScript + Vite 7 + Tailwind v4
    ├── package.json
    ├── vite.config.ts
    ├── tsconfig.json
    └── src/
        ├── App.tsx             ← router root, QueryClientProvider, Sidebar layout
        ├── index.css           ← @import "tailwindcss"; + base styles
        ├── main.tsx
        ├── api/
        │   ├── config.ts
        │   ├── pullRequests.ts
        │   ├── repositories.ts
        │   └── scripts.ts
        ├── components/
        │   └── Sidebar.tsx
        ├── features/
        │   ├── dashboard/DashboardPage.tsx
        │   ├── pullRequests/PullRequestsPage.tsx
        │   ├── repositories/RepositoriesPage.tsx
        │   ├── scripts/ScriptsPage.tsx
        │   └── settings/SettingsPage.tsx
        ├── hooks/
        │   └── useLogHub.ts    ← SignalR hook for live log streaming
        ├── store/
        │   └── uiStore.ts      ← Zustand store for UI-only state
        └── types/
            └── index.ts        ← All shared TypeScript interfaces
```

---

## How to run

### Quickest way — VS Code workspace tasks
Open `DevelopmentHub.code-workspace` in VS Code, then run the default build task:
- **Ctrl+Shift+B** (or Terminal → Run Build Task) → **dev: start all**
- This launches `backend: run` and `frontend: dev` in parallel, each in its own dedicated terminal panel.

Individual tasks also available: `backend: build`, `frontend: type-check`, `frontend: build`, `frontend: install`.

### Manual
```powershell
# Terminal 1 — Backend
cd backend
dotnet run
# Listens on http://localhost:5131
# Requires MongoDB on localhost:27017 (see Prerequisites below)
# Indexes are created idempotently on startup — no migration step needed

# Terminal 2 — Frontend
cd frontend
npm run dev
# Starts at http://localhost:5173
# Vite proxy: /api → :5131, /hubs → :5131 (ws)
```

### Prerequisites — MongoDB
MongoDB must be running locally before starting the backend.
- **Windows (recommended):** Install [MongoDB Community Edition](https://www.mongodb.com/try/download/community) and start the service:
  ```powershell
  Start-Service MongoDB
  ```
- **Docker alternative:**
  ```powershell
  docker run -d -p 27017:27017 --name devhub-mongo mongo:8
  ```
- Default connection string: `mongodb://localhost:27017` (database: `developmenthub`)
- Override via `appsettings.local.json` using `MongoConnectionString` / `MongoDatabaseName` keys.

### Frontend
```powershell
cd frontend
npm run dev
# Starts at http://localhost:5173
# /api/* → proxied to backend
# /hubs/* → proxied to backend (WebSocket upgrade)
```

### First-time setup
Create `backend/appsettings.local.json` (git-ignored):
```json
{
  "RepositoryRoots": ["C:\\Projects"],
  "MongoConnectionString": "mongodb://localhost:27017",
  "MongoDatabaseName": "developmenthub",
  "AzureDevOps": {
    "Organization": "myorg",
    "Project": "MyProject",
    "UserEmail": "you@example.com",
    "Pat": "your-pat-here"
  },
  "Scripts": [
    {
      "Id": "reset-db",
      "Name": "Reset Database",
      "Description": "Drops and recreates the local dev database",
      "WorkingDirectory": "C:\\Projects\\MyApp",
      "Command": "dotnet",
      "Arguments": ["ef", "database", "drop", "--force"],
      "EnvironmentVariables": {}
    }
  ]
}
```

---

## Key architectural decisions and gotchas

### 1. .NET version is 9, not 8
The plan said .NET 8 but `dotnet --version` returned `9.0.306`. All packages are on `9.0.3`.
- MongoDB.Driver: `3.7.0`
- LibGit2Sharp: `0.31.0`
- SignalR is built into ASP.NET Core — **no separate NuGet package needed**

### 2. MongoDB replaces EF Core + SQLite
`DashboardDatabase` (Singleton) wraps `IMongoDatabase` and exposes two typed `IMongoCollection<T>` properties:
- `Repositories` → collection `"repositories"`
- `ScriptExecutions` → collection `"script_executions"`

`EnsureIndexesAsync()` is called once at startup. It is idempotent — MongoDB silently skips index creation if an identical index already exists. No migration tooling is needed.

### 3. All entity IDs are now `string` (MongoDB ObjectId)
Previously `int` auto-increment, IDs are now 24-character hex strings (MongoDB ObjectId). They are pre-generated in the C# model constructor using `ObjectId.GenerateNewId().ToString()` so the ID is known before the insert. The `[BsonId]` + `[BsonRepresentation(BsonType.ObjectId)]` attributes make the driver serialize/deserialize correctly.

This cascades everywhere:
- DTOs: `string Id`
- Controller routes: `{id:int}` → `{id}`, `{executionId:int}` → `{executionId}`
- Frontend `types/index.ts`: `id: string` for `Repository` and `Execution`
- Frontend API clients: all `id` / `executionId` parameters are `string`
- `useLogHub` hook: `executionId: string | null`
- SignalR hub methods `JoinExecution` / `LeaveExecution`: `string executionId`
- `ScriptService._running` dictionary: `ConcurrentDictionary<string, ...>`

### 4. `ScriptService` is registered as Singleton, now simpler
`IScriptService` / `ScriptService` is `AddSingleton` because it holds a `ConcurrentDictionary` of live running processes. Previously it used `IServiceScopeFactory` because `DashboardDbContext` was Scoped. Since `DashboardDatabase` is also Singleton (MongoDB clients are designed for this), `ScriptService` now **injects `DashboardDatabase` directly** — the `IServiceScopeFactory` workaround is gone.

### 5. PAT is stored in `appsettings.local.json`, never the DB
The PAT is read at startup to configure the named `HttpClient`. The `GET /api/config` endpoint returns `"***"` as a placeholder for the PAT field. The `PUT /api/config` endpoint only overwrites the PAT if the submitted value is not `"***"` or empty — otherwise it preserves the existing value from `IOptions<AppSettings>`.

### 6. `HttpClient` for Azure DevOps is configured at startup
The PAT is baked into the `HttpClient` factory in `Program.cs` at startup. If the user saves a new PAT via the Settings UI, they need to restart the backend for the new PAT to take effect on HTTP calls. The `appsettings.local.json` file is updated immediately, but `IHttpClientFactory` does not reload mid-process.

### 7. Vite proxy for SignalR WebSockets
`vite.config.ts` has `ws: true` on the `/hubs` proxy entry. SignalR negotiates transport over HTTP first at `/hubs/log/negotiate`, then upgrades to WebSocket at `/hubs/log`. Both are proxied.

### 8. CORS requires `AllowCredentials()` for SignalR
SignalR uses cookies/credentials during the WebSocket upgrade. `AllowAnyOrigin()` combined with `AllowCredentials()` is invalid in ASP.NET Core and throws at runtime. The policy uses `WithOrigins("http://localhost:5173")` specifically.

### 9. Repository scan does not recurse into discovered repos
When the scanner finds a `.git` folder it records that directory as a repo and stops recursing into it. This prevents subdirectories of a repo (e.g., `vendor/`, `submodules/`) from being listed as separate repos.

### 10. Entry points are stored as a native array in MongoDB
`RepositoryEntity.EntryPoints` is a `List<string>` stored as a BSON array — no JSON serialization needed. `RepositoryService.MapToDto()` maps it directly (no `JsonSerializer.Deserialize` call).

### 11. `UsageScore` formula
```csharp
double daysSinceOpen = entity.LastOpenedAt.HasValue
    ? (DateTime.UtcNow - entity.LastOpenedAt.Value).TotalDays
    : double.MaxValue;
double usageScore = entity.OpenCount + (daysSinceOpen < 7 ? 10.0 : 0.0);
```
Repos opened in the last 7 days get +10 bonus. Sorting is: favorites first, then by `usageScore` descending.

### 12. Tailwind v4
The project uses Tailwind CSS v4 with `@tailwindcss/vite` (not the PostCSS plugin). The setup is:
- `npm install -D tailwindcss @tailwindcss/vite`
- `vite.config.ts`: `plugins: [react(), tailwindcss()]`
- `src/index.css`: first line is `@import "tailwindcss";`
- **No `tailwind.config.js` needed** — Tailwind v4 scans files automatically.

### 13. React Router v7
The installed version is `react-router-dom@7.13.1` (not v6 as the plan said). The API is the same for basic `<BrowserRouter>`, `<Routes>`, `<Route>`, `<NavLink>` usage.

### 14. Vite proxy port — already fixed
The auto-generated `launchSettings.json` uses port `5131`. The Vite proxy in `vite.config.ts` was previously pointing at `:5000` — **this has been corrected to `:5131`**. Both proxy entries (`/api` and `/hubs`) now target `http://localhost:5131`.

### 15. `.code-workspace` file
`DevelopmentHub.code-workspace` at the repo root defines VS Code workspace tasks. Opening this file gives you the **dev: start all** task (Ctrl+Shift+B) which launches backend and frontend in parallel. It also recommends the MongoDB for VS Code extension (`mongodb.mongodb-vscode`) for inspecting the database.

---

## REST API reference

| Method | Route | Controller | Description |
|--------|-------|-----------|-------------|
| `GET` | `/api/repositories` | `RepositoriesController` | All repos sorted by favorite then usage score |
| `POST` | `/api/repositories/scan` | `RepositoriesController` | Trigger full directory scan and upsert |
| `PATCH` | `/api/repositories/{id}/favorite` | `RepositoriesController` | Toggle `IsFavorite` |
| `POST` | `/api/repositories/{id}/open` | `RepositoriesController` | Launch VS/VS Code, increment open count |
| `POST` | `/api/repositories/{id}/sync` | `RepositoriesController` | `git fetch --prune` + `git pull` |
| `GET` | `/api/pullrequests` | `PullRequestsController` | Active PRs (60s cache). Fetches as author + reviewer |
| `GET` | `/api/scripts` | `ScriptsController` | Script definitions from config |
| `POST` | `/api/scripts/{scriptId}/execute` | `ScriptsController` | Start a script, returns `{ id, status, ... }` |
| `POST` | `/api/scripts/executions/{id}/cancel` | `ScriptsController` | Kill the running process tree |
| `GET` | `/api/scripts/executions` | `ScriptsController` | Recent execution history (default: last 50) |
| `GET` | `/api/scripts/executions/{id}` | `ScriptsController` | Full detail with `outputLog` text |
| `GET` | `/api/config` | `ConfigController` | Current config (PAT redacted as `***`) |
| `PUT` | `/api/config` | `ConfigController` | Write to `appsettings.local.json` |

### SignalR hub: `/hubs/log`
Client methods to invoke on the server:
- `JoinExecution(executionId: number)` — subscribe to log stream for an execution
- `LeaveExecution(executionId: number)` — unsubscribe

Server events pushed to client:
- `LogLine` → `{ text: string, stream: "stdout"|"stderr", timestamp: string }`
- `ExecutionCompleted` → `{ executionId: number, exitCode: number, status: string }`

---

## Database schema (MongoDB)

MongoDB database: `developmenthub` (configurable via `MongoDatabaseName`)

### Collection: `repositories`
| Field | BSON Type | Notes |
|-------|-----------|-------|
| `_id` | ObjectId | 24-char hex string in C#, mapped via `[BsonId]` |
| `Name` | String | Directory name |
| `Path` | String | Absolute path — **unique index** |
| `IsFavorite` | Boolean | |
| `CurrentBranch` | String/null | |
| `AheadBy` | Int32 | vs upstream |
| `BehindBy` | Int32 | vs upstream |
| `EntryPoints` | Array\<String\> | Native BSON array of .sln / .code-workspace paths |
| `OpenCount` | Int32 | |
| `LastOpenedAt` | Date/null | UTC |
| `LastSyncedAt` | Date/null | UTC |
| `LastSeenAt` | Date/null | Updated on every scan |
| `CreatedAt` | Date | UTC |

### Collection: `script_executions`
| Field | BSON Type | Notes |
|-------|-----------|-------|
| `_id` | ObjectId | 24-char hex string in C# |
| `ScriptDefinitionId` | String | Matches `AppSettings.Scripts[].Id` |
| `ScriptName` | String | Denormalized for display |
| `StartedAt` | Date | **Descending index** for history queries |
| `FinishedAt` | Date/null | |
| `ExitCode` | Int32/null | |
| `Status` | String | `Running`/`Success`/`Failed`/`Cancelled` (stored as string via `[BsonRepresentation(BsonType.String)]`) |
| `OutputLog` | String | Full stdout+stderr with timestamps |

---

## Service dependency graph

```
Program.cs
├── IMongoClient → MongoClient             (Singleton)
├── DashboardDatabase                      (Singleton — wraps IMongoClient)
├── IGitService → GitService               (Scoped)
├── ILauncherService → LauncherService     (Scoped)
├── IRepositoryService → RepositoryService (Scoped)
│     depends on: DashboardDatabase, IGitService, ILauncherService, IOptions<AppSettings>
├── IScriptService → ScriptService         (Singleton — injects DashboardDatabase directly)
│     depends on: DashboardDatabase, IOptions<AppSettings>, IHubContext<LogHub>
├── IAzureDevOpsService → AzureDevOpsService (Scoped)
│     depends on: IHttpClientFactory("AzureDevOps"), IOptions<AppSettings>, IMemoryCache
├── RepositoryScannerService               (IHostedService — background)
│     depends on: IServiceScopeFactory, IOptions<AppSettings>
└── LogHub                                 (SignalR Hub — transient)
```

---

## Frontend query keys (TanStack Query)

All server state is cached under these keys. Use `queryClient.invalidateQueries({ queryKey: [...] })` after mutations.

| Key | Data | Refetch interval |
|-----|------|-----------------|
| `['repositories']` | `Repository[]` | On window focus |
| `['pullrequests']` | `PullRequest[]` | 120 seconds |
| `['scripts']` | `Script[]` | On window focus |
| `['executions']` | `Execution[]` | 5 seconds |
| `['execution-detail', id]` | `ExecutionDetail` | Only fetched when `completed = true` |
| `['config']` | `AppConfig` | On window focus |

---

## What is NOT yet implemented (future work)

From the original plan, these items are unstarted or only partially complete:

1. **Repositories page** — `RepositoriesPage.tsx` is fully implemented. The open/sync/favorite buttons all use `string` IDs after the MongoDB migration.

2. **Config hot-reload** — `appsettings.local.json` has `reloadOnChange: true` in `Program.cs`, so `IOptionsMonitor<AppSettings>` would pick up changes. However `IScriptService` uses `IOptions` (snapshot at startup). Consider switching `ScriptService` to `IOptionsMonitor` for live script definition updates.

3. **PAT rotation without restart** — The `HttpClient` named `"AzureDevOps"` bakes the PAT at startup. To support PAT changes without restart, refactor to add the `Authorization` header per-request inside `AzureDevOpsService` instead of in the factory registration in `Program.cs`.

4. **Scan progress feedback** — Long scans on large directories block silently. Consider streaming scan progress via SignalR or a dedicated SSE endpoint.

5. **Nested `.code-workspace` / `.sln` discovery** — Current implementation searches up to `EntryPointMaxDepth` levels deep. Hidden directories (starting with `.`) are skipped. `node_modules` is also skipped. Other heavy directories (e.g., `packages/`, `artifacts/`) are not currently excluded.

6. **Error feedback in UI** — Most mutations use `useMutation` without `onError` handlers. A toast/notification system should be added.

7. **Script definitions in DB** — Currently scripts live only in `appsettings.json` / `appsettings.local.json`. A future phase would move them to SQLite with full CRUD API so they can be managed in the UI without editing JSON.

8. **Soft-delete for removed repos** — When a repository is no longer found during a scan, it stays in the DB with an old `LastSeenAt`. There is no UI to show stale repos or remove them. A cleanup endpoint or stale-repo indicator should be added.

9. **`devenv.exe` path detection** — `LauncherService` calls `devenv.exe` directly, assuming it is on `PATH`. On most Windows systems it is not. A future improvement would detect Visual Studio installations via the VS Setup API or by scanning known installation paths like `C:\Program Files\Microsoft Visual Studio\`.

10. **Frontend TypeScript strict checks** — `tsconfig.json` strict mode may flag some implicit `any` in `useLogHub.ts` closures. Run `npx tsc --noEmit` from `frontend/` before committing.

---

## Immediate next steps for a new agent

1. **Ensure MongoDB is running** on `localhost:27017` before starting the backend (Community Edition service or Docker).

2. **Open `DevelopmentHub.code-workspace`** in VS Code, then press **Ctrl+Shift+B** and run **dev: start all** to launch both servers in parallel.

3. **Create `backend/appsettings.local.json`** with at least `RepositoryRoots` to trigger the initial scan (see First-time setup section above).

4. **Smoke test** at `http://localhost:5173` — the Settings page lets you configure roots without editing JSON.

5. **MongoDB for VS Code extension** (`mongodb.mongodb-vscode`) is recommended in the `.code-workspace` — use it to inspect the `developmenthub` database during development.
