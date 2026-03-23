import { Fragment } from "react";
import vscodeIconUrl from "../../../assets/icons/vscode.svg";
import visualStudioIconUrl from "../../../assets/icons/visualstudio.svg";
import explorerIconUrl from "../../../assets/icons/windows-explorer.svg";
import type { Repository, RepositoryOpener } from "../../../types";
import { Empty } from "./shared";
import "./RepositoriesWidget.css";

function OpenerIcon({ opener, size }: { opener: RepositoryOpener; size: number }) {
  if (opener.iconType === "vscode")
    return <img src={vscodeIconUrl} width={size} height={size} alt={opener.label} draggable={false} />;
  if (opener.iconType === "visualstudio")
    return <img src={visualStudioIconUrl} width={size} height={size} alt={opener.label} draggable={false} />;
  if (opener.iconPath)
    return <img src={`/api/icon-extractor?path=${encodeURIComponent(opener.iconPath)}`} width={size} height={size} alt={opener.label} draggable={false} />;
  return (
    <span className="opener-icon-initial" style={{ width: size, height: size, fontSize: size * 0.6 }}>
      {opener.label ? opener.label[0].toUpperCase() : "?"}
    </span>
  );
}

export function RepositoriesWidget({
  repos,
  openers,
  openError,
  onClearOpenError,
  onOpen,
  onToggleFav,
}: {
  repos: Repository[];
  openers: RepositoryOpener[];
  openError: string | null;
  onClearOpenError: () => void;
  onOpen: (id: string, openerId?: string) => void;
  onToggleFav: (id: string) => void;
}) {
  if (repos.length === 0) return <Empty text="No repositories found" />;
  return (
    <>
      {openError && (
        <div className="panel-error-bar">
          <span>⚠ {openError}</span>
          <button onClick={onClearOpenError}>✕</button>
        </div>
      )}
    <div className="repo-grid">
      {/* header */}
      <div className="repo-grid-header repo-col-name">Repository</div>
      <div className="repo-grid-header repo-col-tags">Tags</div>
      <div className="repo-grid-header repo-col-branch">Branch</div>
      <div className="repo-grid-header repo-col-openers" />
      <div className="repo-grid-header repo-col-fav" />

      {/* rows */}
      {repos.map((r) => (
        <Fragment key={r.id}>
          {/* name */}
          <div className="repo-cell repo-col-name">
            <span className="item-name">{r.name}</span>
          </div>

          {/* tags */}
          <div className="repo-cell repo-col-tags">
            <div className="repo-tag-list">
              {r.tags.map((tag) => (
                <span key={tag} className="repo-tag-chip">{tag}</span>
              ))}
            </div>
          </div>

          {/* branch + ahead/behind */}
          <div className="repo-cell repo-col-branch">
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
            {r.scanIssueCode && (
              <span className="repo-scan-issue" title={r.scanIssueMessage ?? undefined}>
                ⚠ {getRepositoryScanIssueLabel(r.scanIssueCode)}
              </span>
            )}
          </div>

          {/* Configured openers + Explorer in one cell for even spacing */}
          <div className="repo-cell repo-col-openers">
            {openers.map((opener) => (
              <button
                key={opener.id}
                className="item-open-icon"
                style={{ visibility: r.entryPoints.some((ep) => ep.extension === opener.fileExtension) ? "visible" : "hidden" }}
                onClick={() => onOpen(r.id, opener.id)}
                title={`In ${opener.label} öffnen`}
              >
                <OpenerIcon opener={opener} size={20} />
              </button>
            ))}
            <button
              className="item-open-icon"
              onClick={() => onOpen(r.id)}
              title="In Explorer öffnen"
            >
              <img src={explorerIconUrl} width="20" height="20" alt="Explorer" draggable={false} />
            </button>
          </div>

          {/* Favourite */}
          <div className="repo-cell repo-col-fav">
            <button
              className={`repo-fav-btn${r.isFavorite ? " repo-fav-btn--active" : ""}`}
              onClick={() => onToggleFav(r.id)}
              title={r.isFavorite ? "Favorit entfernen" : "Als Favorit markieren"}
            >
              ★
            </button>
          </div>
        </Fragment>
      ))}
    </div>
    </>
  );
}

function getRepositoryScanIssueLabel(issueCode: string): string {
  switch (issueCode) {
    case "DubiousOwnership":
      return "Git ownership blocked";
    case "NotAGitRepository":
      return "Not a Git repository";
    case "PathNotFound":
      return "Path not found";
    case "RemoteNotFoundOrPermissionDenied":
      return "Remote missing or no access";
    case "FetchTimeout":
      return "Fetch timed out";
    default:
      return "Repository scan warning";
  }
}
