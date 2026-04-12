import { useState, Fragment, useCallback } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { repositoriesApi } from "../../api/repositories";
import { useRepositoryScan } from "../../hooks/useRepositoryScan";
import { useRepositoryHub } from "../../hooks/useRepositoryHub";
import { configApi } from "../../api/config";
import type { Repository } from "../../types";
import explorerIconUrl from "../../assets/icons/windows-explorer.svg";
import { ErrorBar } from "../../components/ErrorBar";
import { OpenerIcon } from "../../components/OpenerIcon";
import { FilterToolbar } from "../../components/FilterToolbar";
import { TagEditor } from "../../components/TagEditor";
import { getScanIssueLabel } from "../../utils/repositoryUtils";
import "./RepositoriesPage.css";

type Filter = "all" | "favorites" | "issues";

export default function RepositoriesPage() {
  const queryClient = useQueryClient();
  const [search, setSearch] = useState("");
  const [filter, setFilter] = useState<Filter>("all");
  const [openError, setOpenError] = useState<string | null>(null);
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
  const [selectedTags, setSelectedTags] = useState<Set<string>>(new Set());

  const { data: repos = [], isLoading } = useQuery<Repository[]>({
    queryKey: ["repositories"],
    queryFn: repositoriesApi.getAll,
  });

  const scanMutation = useRepositoryScan();

  const handleRepositoriesUpdated = useCallback(() => {
    queryClient.invalidateQueries({ queryKey: ["repositories"] });
  }, [queryClient]);
  const { isScanning } = useRepositoryHub(handleRepositoriesUpdated);

  const { data: config } = useQuery({ queryKey: ["config"], queryFn: configApi.get });
  const openers = config?.repositoryOpeners ?? [];

  const openMutation = useMutation({
    mutationFn: ({ id, openerId }: { id: string; openerId?: string }) =>
      repositoriesApi.open(id, { openerId }),
    onError: (err: Error) => setOpenError(err.message),
  });

  const openWorkspaceMutation = useMutation({
    mutationFn: () => repositoriesApi.openWorkspace([...selectedIds]),
    onError: (err: Error) => setOpenError(err.message),
  });

  const toggleSelect = (id: string) =>
    setSelectedIds((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });

  const updateTagsMutation = useMutation({
    mutationFn: ({ id, tags }: { id: string; tags: string[] }) =>
      repositoriesApi.updateTags(id, tags),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["repositories"] }),
    onError: (err: Error) => setOpenError(err.message),
  });

  const toggleFavMutation = useMutation({
    mutationFn: (id: string) => repositoriesApi.toggleFavorite(id),
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: ["repositories"] }),
  });

  const issueCount = repos.filter((r) => !!r.scanIssueCode).length;

  const allTags = [...new Set(repos.flatMap((r) => r.tags))].sort();

  const toggleTagFilter = (tag: string) =>
    setSelectedTags((prev) => {
      const next = new Set(prev);
      if (next.has(tag)) next.delete(tag); else next.add(tag);
      return next;
    });

  const filtered = repos
    .filter((r) => {
      if (filter === "favorites") return r.isFavorite;
      if (filter === "issues") return !!r.scanIssueCode;
      return true;
    })
    .filter(
      (r) =>
        !search ||
        r.name.toLowerCase().includes(search.toLowerCase()) ||
        r.path.toLowerCase().includes(search.toLowerCase()),
    )
    .filter((r) => selectedTags.size === 0 || [...selectedTags].every((t) => r.tags.includes(t)))
    .sort((a, b) => {
      if (a.isFavorite !== b.isFavorite) return a.isFavorite ? -1 : 1;
      return a.name.localeCompare(b.name);
    });

  const repoFilters = [
    { value: "all", label: `All (${repos.length})` },
    { value: "favorites", label: `★ Favorites` },
    { value: "issues", label: `⚠ Issues${issueCount > 0 ? ` (${issueCount})` : ""}` },
  ];

  return (
    <div className="repos-page">
      <div className="repos-card">
        <FilterToolbar
          search={search}
          onSearchChange={setSearch}
          filter={filter}
          onFilterChange={(f) => setFilter(f as Filter)}
          filters={repoFilters}
          searchPlaceholder="Search by name or path…"
          shownCount={filtered.length}
          totalCount={repos.length}
        >
          {selectedIds.size >= 2 && (
            <button
              className="btn-secondary"
              onClick={() => openWorkspaceMutation.mutate()}
              disabled={openWorkspaceMutation.isPending}
              title={`${selectedIds.size} Repositories in gemeinsamem Workspace öffnen`}
            >
              {openWorkspaceMutation.isPending
                ? "Öffne…"
                : `⧉ Workspace (${selectedIds.size})`}
            </button>
          )}
          <button
            className="btn-primary"
            onClick={() => scanMutation.mutate()}
            disabled={isScanning}
          >
            {isScanning ? <><span className="scan-spinner" /> Scanning…</> : "↻ Scan"}
          </button>
        </FilterToolbar>

        {allTags.length > 0 && (
          <div className="repos-tag-filter-row">
            <span className="repos-tag-filter-label">Tags:</span>
            {allTags.map((tag) => (
              <button
                key={tag}
                className={`repos-tag-filter-chip${selectedTags.has(tag) ? " repos-tag-filter-chip--active" : ""}`}
                onClick={() => toggleTagFilter(tag)}
              >
                {tag}
              </button>
            ))}
            {selectedTags.size > 0 && (
              <button className="repos-tag-filter-clear" onClick={() => setSelectedTags(new Set())}>
                ✕ Clear
              </button>
            )}
          </div>
        )}

        <ErrorBar message={openError} onDismiss={() => setOpenError(null)} className="repos-error-bar" />
      </div>

      {isLoading ? (
        <p className="repos-empty">Loading…</p>
      ) : filtered.length === 0 ? (
        <p className="repos-empty">No repositories match your filter.</p>
      ) : (
        <div className="repos-table">
          {/* header */}
          <div className="repos-th repos-col-check" />
          <div className="repos-th repos-col-name">Repository</div>
          <div className="repos-th repos-col-path">Path</div>
          <div className="repos-th repos-col-branch">Branch</div>
          <div className="repos-th repos-col-used">Last Used</div>
          <div className="repos-th repos-col-tags">Tags</div>
          <div className="repos-th repos-col-actions">Open With</div>
          <div className="repos-th repos-col-fav" />

          {/* rows */}
          {filtered.map((r) => (
            <Fragment key={r.id}>
              <div className="repos-td repos-col-check">
                <input
                  type="checkbox"
                  className="repo-select-checkbox"
                  checked={selectedIds.has(r.id)}
                  onChange={() => toggleSelect(r.id)}
                  title="Für Workspace auswählen"
                />
              </div>
              <div className="repos-td repos-col-name">
                <span className="repos-name">{r.name}</span>
                {r.scanIssueCode && (
                  <span
                    className="repos-issue-badge"
                    title={r.scanIssueMessage ?? undefined}
                  >
                    ⚠ {getScanIssueLabel(r.scanIssueCode)}
                  </span>
                )}
              </div>

              <div className="repos-td repos-col-path">
                <span className="repos-path" title={r.path}>
                  {r.path}
                </span>
              </div>

              <div className="repos-td repos-col-branch">
                {r.currentBranch && (
                  <div className="item-branch-row">
                    <span className="item-branch">{r.currentBranch}</span>
                    {(r.aheadBy ?? 0) > 0 && (
                      <span className="item-ahead">↑{r.aheadBy}</span>
                    )}
                    {(r.behindBy ?? 0) > 0 && (
                      <span className="item-behind">↓{r.behindBy}</span>
                    )}
                  </div>
                )}
              </div>

              <div className="repos-td repos-col-used">
                <span className="repos-last-used">
                  {formatLastUsed(r.lastOpenedAt)}
                </span>
              </div>

              <div className="repos-td repos-col-tags">
                <TagEditor
                  tags={r.tags}
                  onSave={(tags) => updateTagsMutation.mutate({ id: r.id, tags })}
                />
              </div>

              <div className="repos-td repos-col-actions">
                {openers.map((opener) => (
                  <button
                    key={opener.id}
                    className="item-open-icon"
                    style={{ visibility: r.entryPoints.some((ep) => ep.extension === opener.fileExtension) ? "visible" : "hidden" }}
                    onClick={() => openMutation.mutate({ id: r.id, openerId: opener.id })}
                    title={`In ${opener.label} öffnen`}
                  >
                    <OpenerIcon opener={opener} size={20} />
                  </button>
                ))}
                <button
                  className="item-open-icon"
                  onClick={() => openMutation.mutate({ id: r.id })}
                  title="In Explorer öffnen"
                >
                  <img src={explorerIconUrl} width="20" height="20" alt="Explorer" draggable={false} />
                </button>
              </div>

              <div className="repos-td repos-col-fav">
                <button
                  className={`repo-fav-btn${r.isFavorite ? " repo-fav-btn--active" : ""}`}
                  onClick={() => toggleFavMutation.mutate(r.id)}
                  title={
                    r.isFavorite ? "Favorit entfernen" : "Als Favorit markieren"
                  }
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

function formatLastUsed(lastOpenedAt: string | null): string {
  if (!lastOpenedAt) return "—";
  const diffDays = Math.floor(
    (Date.now() - new Date(lastOpenedAt).getTime()) / 86_400_000,
  );
  if (diffDays === 0) return "Today";
  if (diffDays === 1) return "Yesterday";
  if (diffDays < 7) return `${diffDays}d ago`;
  if (diffDays < 30) return `${Math.floor(diffDays / 7)}w ago`;
  if (diffDays < 365) return `${Math.floor(diffDays / 30)}mo ago`;
  return `${Math.floor(diffDays / 365)}y ago`;
}
