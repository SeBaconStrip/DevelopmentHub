import { useState, Fragment } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { fetchRepositories, repositoriesApi } from "../../api/repositories";
import type { Repository } from "../../types";
import vscodeIconUrl from "../../assets/icons/vscode.svg";
import visualStudioIconUrl from "../../assets/icons/visualstudio.svg";
import explorerIconUrl from "../../assets/icons/windows-explorer.svg";
import "./RepositoriesPage.css";

type Filter = "all" | "favorites" | "issues";

export default function RepositoriesPage() {
  const queryClient = useQueryClient();
  const [search, setSearch] = useState("");
  const [filter, setFilter] = useState<Filter>("all");
  const [openError, setOpenError] = useState<string | null>(null);

  const { data: repos = [], isLoading } = useQuery<Repository[]>({
    queryKey: ["repositories"],
    queryFn: fetchRepositories,
  });

  const scanMutation = useMutation({
    mutationFn: repositoriesApi.scan,
    onSuccess: (data) => queryClient.setQueryData(["repositories"], data),
  });

  const openMutation = useMutation({
    mutationFn: ({ id, openWith }: { id: string; openWith: "VsCode" | "VisualStudio" | "Explorer" }) =>
      repositoriesApi.open(id, { openWith }),
    onError: (err: Error) => setOpenError(err.message),
  });

  const toggleFavMutation = useMutation({
    mutationFn: (id: string) => repositoriesApi.toggleFavorite(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["repositories"] }),
  });

  const issueCount = repos.filter((r) => !!r.scanIssueCode).length;

  const filtered = repos
    .filter((r) => {
      if (filter === "favorites") return r.isFavorite;
      if (filter === "issues") return !!r.scanIssueCode;
      return true;
    })
    .filter((r) => !search || r.name.toLowerCase().includes(search.toLowerCase()) || r.path.toLowerCase().includes(search.toLowerCase()))
    .sort((a, b) => {
      if (a.isFavorite !== b.isFavorite) return a.isFavorite ? -1 : 1;
      return a.name.localeCompare(b.name);
    });

  return (
    <div className="repos-page">
      <div className="repos-card">
      <div className="repos-toolbar">
        <div className="repos-toolbar-left">
          <input
            className="repos-search"
            type="search"
            placeholder="Search by name or path…"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
          <div className="repos-filters">
            {(["all", "favorites", "issues"] as Filter[]).map((f) => (
              <button
                key={f}
                className={`repos-filter-btn${filter === f ? " repos-filter-btn--active" : ""}`}
                onClick={() => setFilter(f)}
              >
                {f === "all" && `All (${repos.length})`}
                {f === "favorites" && `★ Favorites`}
                {f === "issues" && `⚠ Issues${issueCount > 0 ? ` (${issueCount})` : ""}`}
              </button>
            ))}
          </div>
        </div>
        <div className="repos-toolbar-right">
          {filtered.length !== repos.length && (
            <span className="repos-count">{filtered.length} shown</span>
          )}
          <button
            className="btn-primary"
            onClick={() => scanMutation.mutate()}
            disabled={scanMutation.isPending}
          >
            {scanMutation.isPending ? "Scanning…" : "↻ Scan"}
          </button>
        </div>
      </div>

      {openError && (
        <div className="repos-error-bar">
          <span>⚠ {openError}</span>
          <button onClick={() => setOpenError(null)}>✕</button>
        </div>
      )}

      </div>

      {isLoading ? (
        <p className="repos-empty">Loading…</p>
      ) : filtered.length === 0 ? (
        <p className="repos-empty">No repositories match your filter.</p>
      ) : (
        <div className="repos-table">
          {/* header */}
          <div className="repos-th repos-col-name">Repository</div>
          <div className="repos-th repos-col-path">Path</div>
          <div className="repos-th repos-col-branch">Branch</div>
          <div className="repos-th repos-col-used">Last Used</div>
          <div className="repos-th repos-col-actions">Open With</div>
          <div className="repos-th repos-col-fav" />

          {/* rows */}
          {filtered.map((r) => (
            <Fragment key={r.id}>
              <div className="repos-td repos-col-name">
                <span className="repos-name">{r.name}</span>
                {r.scanIssueCode && (
                  <span className="repos-issue-badge" title={r.scanIssueMessage ?? undefined}>
                    ⚠ {getScanIssueLabel(r.scanIssueCode)}
                  </span>
                )}
              </div>

              <div className="repos-td repos-col-path">
                <span className="repos-path" title={r.path}>{r.path}</span>
              </div>

              <div className="repos-td repos-col-branch">
                {r.currentBranch && (
                  <div className="item-branch-row">
                    <span className="item-branch">{r.currentBranch}</span>
                    {(r.aheadBy ?? 0) > 0 && <span className="item-ahead">↑{r.aheadBy}</span>}
                    {(r.behindBy ?? 0) > 0 && <span className="item-behind">↓{r.behindBy}</span>}
                  </div>
                )}
              </div>

              <div className="repos-td repos-col-used">
                <span className="repos-last-used">{formatLastUsed(r.lastOpenedAt)}</span>
              </div>

              <div className="repos-td repos-col-actions">
                {r.entryPoints.some((ep) => ep.type === "CodeWorkspace" || ep.type === "Folder") && (
                  <button className="item-open-icon" onClick={() => openMutation.mutate({ id: r.id, openWith: "VsCode" })} title="In VS Code öffnen">
                    <img src={vscodeIconUrl} width="22" height="22" alt="VS Code" draggable={false} />
                  </button>
                )}
                {r.entryPoints.some((ep) => ep.type === "Solution") && (
                  <button className="item-open-icon" onClick={() => openMutation.mutate({ id: r.id, openWith: "VisualStudio" })} title="In Visual Studio öffnen">
                    <img src={visualStudioIconUrl} width="22" height="22" alt="Visual Studio" draggable={false} />
                  </button>
                )}
                <button className="item-open-icon" onClick={() => openMutation.mutate({ id: r.id, openWith: "Explorer" })} title="In Explorer öffnen">
                  <img src={explorerIconUrl} width="22" height="22" alt="Explorer" draggable={false} />
                </button>
              </div>

              <div className="repos-td repos-col-fav">
                <button
                  className={`repo-fav-btn${r.isFavorite ? " repo-fav-btn--active" : ""}`}
                  onClick={() => toggleFavMutation.mutate(r.id)}
                  title={r.isFavorite ? "Favorit entfernen" : "Als Favorit markieren"}
                >
                  ★
                </button>
              </div>
            </Fragment>
          ))}
        </div>
      )}
    </div>
  );
}

function getScanIssueLabel(code: string): string {
  switch (code) {
    case "DubiousOwnership": return "Git ownership blocked";
    case "NotAGitRepository": return "Not a Git repository";
    case "PathNotFound": return "Path not found";
    case "RemoteNotFoundOrPermissionDenied": return "Remote missing or no access";
    case "FetchTimeout": return "Fetch timed out";
    default: return "Scan warning";
  }
}

function formatLastUsed(lastOpenedAt: string | null): string {
  if (!lastOpenedAt) return "—";
  const diffDays = Math.floor((Date.now() - new Date(lastOpenedAt).getTime()) / 86_400_000);
  if (diffDays === 0) return "Today";
  if (diffDays === 1) return "Yesterday";
  if (diffDays < 7) return `${diffDays}d ago`;
  if (diffDays < 30) return `${Math.floor(diffDays / 7)}w ago`;
  if (diffDays < 365) return `${Math.floor(diffDays / 30)}mo ago`;
  return `${Math.floor(diffDays / 365)}y ago`;
}
