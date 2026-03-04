# Plan: Local Developer Dashboard — Technical Implementation Plan

A locally-hosted ASP.NET Core 8 Web API + React/TypeScript dashboard to manage Git repos, run dev scripts, and surface Azure DevOps PRs. SQLite persists usage tracking and script history. SignalR streams live process output to the frontend. The MVP is built in 6 phases with a clear feature scope per phase.

---

## 1. Project Structure

```
DevelopmentHub/
├── backend/                        # ASP.NET Core 8 Web API
│   ├── Controllers/                # REST endpoints
│   ├── Hubs/                       # SignalR hubs (LogHub)
│   ├── Services/                   # Business logic interfaces + implementations
│   ├── Models/                     # Domain models + DTOs
│   ├── Data/                       # EF Core DbContext + migrations
│   ├── Configuration/              # Strongly-typed settings classes
│   ├── appsettings.json            # Default config (no secrets)
│   ├── appsettings.local.json      # git-ignored, PAT + local paths
│   └── Program.cs
│
└── frontend/                       # React + TypeScript + Vite
    └── src/
        ├── features/
        │   ├── repositories/       # Repo list, cards, actions
        │   ├── pullRequests/       # PR list, filters
        │   └── scripts/            # Script list, execution, log stream
        ├── components/             # Shared UI (layout, badges, modals)
        ├── hooks/                  # Custom hooks (useSignalR, useConfig)
        ├── api/                    # Typed fetch wrappers per feature
        ├── store/                  # Zustand stores (UI state only)
        └── types/                  # Shared TypeScript interfaces
```

---

## 2. Recommended Libraries & Packages

**Backend (NuGet)**

| Package | Purpose |
|---|---|
| `LibGit2Sharp` v0.31 | Local repo introspection (branch, ahead/behind) |
| `Microsoft.EntityFrameworkCore.Sqlite` | ORM + schema migrations for SQLite |
| `Microsoft.AspNetCore.SignalR` | Real-time log streaming (built-in .NET 8) |
| `Microsoft.Extensions.Options` | Strongly-typed config binding (built-in) |

**Frontend (npm)**

| Package | Purpose |
|---|---|
| `@tanstack/react-query` v5 | Server data fetching + caching |
| `zustand` | UI-only client state |
| `@microsoft/signalr` | Subscribe to log stream from SignalR hub |
| `shadcn/ui` + Tailwind CSS | Component library (copied source, not dependency) |
| `react-router-dom` v6 | Page routing |
| `axios` or native `fetch` | HTTP client for API calls |

---

## 3. Domain Models

**`Repository`**
- `Id`, `Name`, `Path`, `IsFavorite`
- `CurrentBranch`, `AheadBy`, `BehindBy` (populated at scan time)
- `EntryPoints`: list of `.sln` / `.code-workspace` file paths
- `OpenCount`, `LastOpenedAt`, `LastSyncedAt` (usage tracking)
- `UsageScore` (computed: weighted combination of recency + open count)

**`ScriptDefinition`**
- `Id`, `Name`, `Description`
- `WorkingDirectory`, `Command`, `Arguments`
- `EnvironmentVariables`: `Dictionary<string, string>`

**`ScriptExecution`**
- `Id`, `ScriptDefinitionId`, `StartedAt`, `FinishedAt`
- `ExitCode`, `Status` (Running / Success / Failed / Cancelled)
- `OutputLog`: stored as text (stdout + stderr interleaved with timestamps)

**`PullRequest`** (not persisted — fetched live from Azure DevOps)
- `PrId`, `Title`, `RepositoryName`, `Status`, `Url`
- `CreatedByMe`, `IsReviewer`, `ReviewerVote`
- `SourceBranch`, `TargetBranch`, `CreatedAt`, `IsDraft`

**Configuration models** (bound from `appsettings.json`):
- `AppSettings.RepositoryRoots`: `string[]`
- `AppSettings.AzureDevOps`: `{ Organization, Project, UserEmail, Pat }`
- `AppSettings.Scripts`: `ScriptDefinition[]`

---

## 4. Database & Storage Design

**SQLite file** at a configurable path (e.g., `./data/dashboard.db`), managed by EF Core.

**Tables:**
- `Repositories` — persisted metadata + usage counters; `Path` is the unique key
- `ScriptExecutions` — history log per script run; `OutputLog` stored as TEXT

**What stays in JSON config** (`appsettings.json` / `appsettings.local.json`):
- `RepositoryRoots` — scan paths
- Script definitions — editable without redeploying
- Azure DevOps credentials — never written to DB

**Scan-merge strategy:** On each `/api/repositories/scan` call, walk root directories for `.git` folders, upsert rows in `Repositories` (new paths added, existing paths updated with fresh git data, removed paths optionally soft-deleted with a `LastSeenAt` field).

---

## 5. REST API Design

| Method | Route | Description |
|---|---|---|
| `POST` | `/api/repositories/scan` | Trigger full scan of all root directories |
| `GET` | `/api/repositories` | Return all repos sorted by favorites then usage score |
| `PATCH` | `/api/repositories/{id}/favorite` | Toggle `IsFavorite` |
| `POST` | `/api/repositories/{id}/open` | Launch VS / VS Code, record open event |
| `POST` | `/api/repositories/{id}/sync` | Run `git fetch` + `git pull`, update branch data |
| `GET` | `/api/pullrequests` | Fetch active PRs from Azure DevOps (live call) |
| `GET` | `/api/scripts` | List all script definitions from config |
| `POST` | `/api/scripts/{id}/execute` | Start script execution, return `executionId` |
| `POST` | `/api/scripts/executions/{id}/cancel` | Send cancellation signal to running process |
| `GET` | `/api/scripts/executions` | List recent execution history |
| `GET` | `/api/scripts/executions/{id}` | Get full log for a completed execution |
| `GET` | `/api/config` | Return current config (PAT redacted) |
| `PUT` | `/api/config` | Update config and persist to `appsettings.local.json` |

**SignalR Hub:** `LogHub` at `/hubs/log`
- Client joins group `execution-{id}` on connect
- Backend pushes `LogLine` events (text, stream type, timestamp) as the process writes output
- Backend pushes `ExecutionCompleted` event with exit code when done

---

## 6. Git Integration Strategy

Use **LibGit2Sharp** for all read operations (fast, no subprocess):
- `Repository.IsValid(path)` — used during directory scan
- `repo.Head.FriendlyName` — current branch name
- `repo.Head.TrackingDetails.AheadBy` / `BehindBy` — upstream comparison
- `repo.RetrieveStatus()` — working directory changes

Use **`git` CLI via `Process`** for write operations (fetch/pull), since LibGit2Sharp's network support is limited and requires native credential helpers:
- `git fetch --prune` then `git pull` with a configurable timeout
- Capture stdout/stderr and stream them through SignalR

**Scan algorithm:**
1. For each root directory in config, walk subdirectories
2. If a subdirectory contains a `.git` folder → it's a repo
3. Do not recurse into discovered repo directories (treat nested repos as separate entries)
4. Upsert into SQLite; populate entry points by scanning for `.sln` and `.code-workspace` files at the repo root level

---

## 7. Azure DevOps Integration

Use **direct `HttpClient` calls** (no SDK dependency) to the Azure DevOps REST API v7.1:

- `GET .../pullrequests?searchCriteria.status=active&searchCriteria.creatorId={userId}` → PRs authored by user
- `GET .../pullrequests?searchCriteria.status=active&searchCriteria.reviewerId={userId}` → PRs where user is reviewer
- Merge and deduplicate results; annotate each PR with `CreatedByMe` / `IsReviewer` flags

**Auth:** PAT sent as `Authorization: Basic {Base64(":" + PAT)}` header via a named `HttpClient` registered in DI.

**User identity:** Azure DevOps uses GUIDs (`descriptorId`) as user identifiers, but the API accepts email-based identity lookup. Add a config field for `UserEmail` and resolve the GUID via `GET .../identities?searchFilter=MailAddress&filterValue={email}` on startup or first PR fetch.

**Caching:** Cache PR results for 60 seconds in memory (`IMemoryCache`) to avoid hammering the API on every dashboard refresh.

---

## 8. Script Execution Architecture

**Execution flow:**
1. `POST /api/scripts/{id}/execute` creates a `ScriptExecution` record (status = Running), returns `executionId`
2. A background `Task` starts the process via `ProcessStartInfo` with `RedirectStandardOutput = true`, `RedirectStandardError = true`, `UseShellExecute = false`
3. Event handlers on `OutputDataReceived` / `ErrorDataReceived` both: (a) append line to an in-memory buffer, and (b) push a `LogLine` SignalR message to the execution's group
4. On process exit: update `ScriptExecution.Status`, `ExitCode`, `FinishedAt`; flush the full log to SQLite; push `ExecutionCompleted` SignalR event
5. Cancellation: call `process.Kill(entireProcessTree: true)` when a cancel request is received

**Concurrency:** Use a `ConcurrentDictionary<Guid, (Process, CancellationTokenSource)>` to track running executions. Optionally limit to one running instance per script definition.

**Environment variables:** Merge script-defined env vars with inherited process environment using `ProcessStartInfo.Environment` dictionary.

---

## 9. Security Considerations (Local Tool)

Since this runs only on localhost, the threat model is limited but still worth considering:

- **PAT storage:** Store in `appsettings.local.json` (git-ignored), or better, use `dotnet user-secrets` in development. Never commit PATs.
- **Command injection:** Script `Command` and `Arguments` come from the config file (not user input), so risk is low — but still use `ProcessStartInfo.ArgumentList` (individual args, not a single string) to prevent shell injection when constructing process arguments.
- **Path traversal:** Validate that requested repository paths are under a configured root directory before performing any git operations on them.
- **CORS:** Use the Vite proxy during development (no CORS config needed); if CORS is added, explicitly list only `http://localhost:5173` — never use `AllowAnyOrigin()` with credentials.
- **PAT redaction:** The `GET /api/config` endpoint must strip the PAT value from the response before returning it to the frontend.
- **Process tree cleanup:** Always call `process.Kill(entireProcessTree: true)` on cancellation and app shutdown to prevent orphaned child processes.

---

## 10. Step-by-Step Implementation Roadmap

**Phase 1 — Project Scaffolding (Day 1)**
1. Create solution: `dotnet new sln`, `dotnet new webapi -n DevelopmentHub.Api`
2. Scaffold frontend: `npm create vite@latest frontend -- --template react-ts`
3. Configure Vite proxy to `/api` → `http://localhost:5000`
4. Add EF Core SQLite, LibGit2Sharp, and SignalR to backend; install shadcn/ui, TanStack Query, Zustand to frontend
5. Add `appsettings.local.json` to `.gitignore`; set up `IOptions<AppSettings>` binding

**Phase 2 — Repository Dashboard MVP (Days 2–4)**
1. Implement directory scanner service using LibGit2Sharp for discovery and metadata
2. Implement `Repositories` EF Core entity + upsert logic
3. Build `RepositoriesController` with scan, list, favorite, open, and sync endpoints
4. Implement VS / VS Code launcher via `ProcessStartInfo`
5. Build React repo list page: cards with branch badge, ahead/behind, favorite toggle, open buttons

**Phase 3 — Script Execution + Log Streaming (Days 5–7)**
1. Implement `ProcessRunner` service with async stdout/stderr capture and cancellation support
2. Implement `ScriptExecutions` EF Core entity and execution lifecycle (create → run → persist)
3. Add `LogHub` SignalR hub; wire `ProcessRunner` events to hub groups
4. Build `ScriptsController` with execute and cancel endpoints
5. Build React scripts page: list scripts, trigger execution, subscribe to `LogHub`, render live log output

**Phase 4 — Azure DevOps PR Dashboard (Day 8)**
1. Register named `HttpClient` with PAT auth header in DI
2. Implement `AzureDevOpsService`: resolve user GUID, fetch author + reviewer PRs, merge and annotate
3. Add 60-second memory cache around PR fetch
4. Build `PullRequestsController` with single `GET /api/pullrequests` endpoint
5. Build React PR page: table with title, repo, status, reviewer vote, PR link

**Phase 5 — Configuration UI (Day 9)**
1. Implement `GET /api/config` (PAT redacted) and `PUT /api/config` (writes `appsettings.local.json`)
2. Build React settings page: add/remove root directories, edit AzDO org/project/email, update PAT, add/remove scripts

**Phase 6 — Polish & Sorting (Day 10)**
1. Implement `UsageScore` computation: `score = openCount + (daysSinceLastOpen < 7 ? 10 : 0)`
2. Apply sorting: favorites first, then by `UsageScore` desc
3. Add dashboard home page: repo count widget, running script status, PR count, last scan time
4. Add auto-scan on startup with configurable interval (background `IHostedService`)

---

## Further Considerations

1. **Entry point detection scope:** Should `.sln`/`.code-workspace` scanning recurse into subdirectories, or only look at the repo root? Recursing is more complete but slower — recommend a configurable max depth (default 2).
2. **SignalR vs SSE for log streaming:** SignalR is recommended for its reconnection handling and future bidirectionality (e.g., sending stdin to a process), but SSE via `text/event-stream` is a simpler alternative if bidirectional control is not needed.
3. **Script definitions in DB vs config:** The plan above stores scripts in `appsettings.json` for easy editing. If you want a full CRUD UI without editing JSON files, scripts could move to SQLite — this is a natural Phase 2 extension.
