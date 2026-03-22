import { Fragment } from "react";
import vscodeIconUrl from "../../../assets/icons/vscode.svg";
import visualStudioIconUrl from "../../../assets/icons/visualstudio.svg";
import explorerIconUrl from "../../../assets/icons/windows-explorer.svg";
import type { Repository } from "../../../types";
import { Empty } from "./shared";
import "./RepositoriesWidget.css";

export function RepositoriesWidget({
  repos,
  openError,
  onClearOpenError,
  onOpen,
  onToggleFav,
}: {
  repos: Repository[];
  openError: string | null;
  onClearOpenError: () => void;
  onOpen: (id: string, openWith: "VsCode" | "VisualStudio" | "Explorer") => void;
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
      <div className="repo-grid-header repo-col-icon" />
      <div className="repo-grid-header repo-col-icon" />
      <div className="repo-grid-header repo-col-icon" />
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

          {/* VS Code */}
          <div className="repo-cell repo-col-icon">
            {r.entryPoints.some(
              (ep) => ep.type === "CodeWorkspace" || ep.type === "Folder",
            ) && (
              <button
                className="item-open-icon"
                onClick={() => onOpen(r.id, "VsCode")}
                title="In VS Code öffnen"
              >
                <img
                  src={vscodeIconUrl}
                  width="24"
                  height="24"
                  alt="VS Code"
                  draggable={false}
                />
              </button>
            )}
          </div>

          {/* Visual Studio */}
          <div className="repo-cell repo-col-icon">
            {r.entryPoints.some((ep) => ep.type === "Solution") && (
              <button
                className="item-open-icon"
                onClick={() => onOpen(r.id, "VisualStudio")}
                title="In Visual Studio öffnen"
              >
                <img
                  src={visualStudioIconUrl}
                  width="24"
                  height="24"
                  alt="Visual Studio"
                  draggable={false}
                />
              </button>
            )}
          </div>

          {/* Explorer */}
          <div className="repo-cell repo-col-icon">
            <button
              className="item-open-icon"
              onClick={() => onOpen(r.id, "Explorer")}
              title="In Explorer öffnen"
            >
              <img
                src={explorerIconUrl}
                width="24"
                height="24"
                alt="Explorer"
                draggable={false}
              />
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
