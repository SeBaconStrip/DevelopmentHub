import { SearchInput } from "../../plugin-sdk/ui";
import {
  PROVIDERS,
  providerLabel,
  type RepoSelection,
} from "./useRepoSelection";

/** Provider toggle, date range and generate button. */
export function RepoSelectionToolbar({
  selection,
  isPending,
  onGenerate,
}: {
  selection: RepoSelection;
  isPending: boolean;
  onGenerate: () => void;
}) {
  const { provider, setProvider, since, setSince, until, setUntil } = selection;
  return (
    <div className="contributors-toolbar">
      <div className="contributors-providers">
        {PROVIDERS.map((p) => (
          <button
            key={p.id}
            className={
              "contributors-provider-btn" +
              (provider === p.id ? " contributors-provider-btn--active" : "")
            }
            onClick={() => setProvider(p.id)}
          >
            {p.label}
          </button>
        ))}
      </div>

      <div className="contributors-dates">
        <label className="contributors-date">
          <span>From</span>
          <input
            type="date"
            value={since}
            max={until || undefined}
            onChange={(e) => setSince(e.target.value)}
          />
        </label>
        <label className="contributors-date">
          <span>To</span>
          <input
            type="date"
            value={until}
            min={since || undefined}
            onChange={(e) => setUntil(e.target.value)}
          />
        </label>
        <button
          className="btn-primary"
          disabled={!selection.isComplete || isPending}
          onClick={onGenerate}
          title={
            !since || !until
              ? "Select a date range"
              : selection.selectedRepoIds.length === 0
                ? "Select at least one repository"
                : undefined
          }
        >
          {isPending ? "Generating…" : "Generate report"}
        </button>
      </div>
    </div>
  );
}

/** Filterable, multi-select repository list. */
export function RepoSelectionPanel({ selection }: { selection: RepoSelection }) {
  const {
    provider,
    repoSearch,
    setRepoSearch,
    repositories,
    reposLoading,
    reposError,
    filteredRepos,
    selectedRepoIds,
    toggleRepo,
    allFilteredSelected,
    toggleSelectAll,
  } = selection;

  return (
    <aside className="contributors-repos-card">
      <div className="contributors-repos-header">
        <SearchInput
          wrapperClassName="contributors-repo-search"
          placeholder="Filter repositories…"
          value={repoSearch}
          onChange={(e) => setRepoSearch(e.target.value)}
        />
        <button
          className="contributors-selectall"
          onClick={toggleSelectAll}
          disabled={filteredRepos.length === 0}
        >
          {allFilteredSelected ? "Clear" : "All"}
        </button>
      </div>
      <div className="contributors-repos-meta">
        {selectedRepoIds.length} selected · {repositories.length} total
      </div>

      {reposLoading ? (
        <p className="contributors-empty">Loading repositories…</p>
      ) : reposError ? (
        <p className="contributors-empty contributors-empty--error">
          Failed to load {providerLabel(provider)} repositories:{" "}
          {reposError.message}
        </p>
      ) : filteredRepos.length === 0 ? (
        <p className="contributors-empty">No repositories found.</p>
      ) : (
        <ul className="contributors-repo-list">
          {filteredRepos.map((repo) => (
            <li key={repo.id}>
              <label className="contributors-repo-item">
                <input
                  type="checkbox"
                  checked={selectedRepoIds.includes(repo.id)}
                  onChange={() => toggleRepo(repo.id)}
                />
                <span className="contributors-repo-name">{repo.name}</span>
              </label>
            </li>
          ))}
        </ul>
      )}
    </aside>
  );
}

/** Tells the user which parts of a report are incomplete. */
export function ReportWarnings({ warnings }: { warnings: string[] }) {
  if (warnings.length === 0) return null;
  return (
    <div className="contributors-warnings" role="status">
      <strong>Some data is missing from this report:</strong>
      <ul>
        {warnings.map((w) => (
          <li key={w}>{w}</li>
        ))}
      </ul>
    </div>
  );
}
