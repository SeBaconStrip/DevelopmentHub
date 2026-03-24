import { launcherApi } from "../../../api/launcher";
import githubIconUrl from "../../../assets/icons/github.svg";
import azureDevOpsIconUrl from "../../../assets/icons/azure-devops.svg";
import type { PullRequest } from "../../../types";
import { Empty } from "./shared";
import "./PullRequestsWidget.css";

const VOTE_LABEL: Record<number, string> = {
  10: "Approved",
  5: "Approved w/ suggestions",
  [-5]: "Waiting for author",
  [-10]: "Rejected",
};

const PROVIDER_ICONS: Record<PullRequest["providerId"], string> = {
  azureDevOps: azureDevOpsIconUrl,
  github: githubIconUrl,
};

export function PullRequestsWidget({ prs }: { prs: PullRequest[] }) {
  if (prs.length === 0) return <Empty text="No pull requests" />;
  return (
    <div className="pr-grid">
      {/* header cells — direct grid children, same as repo-grid pattern */}
      <div className="pr-grid-head pr-col-provider" />
      <div className="pr-grid-head pr-col-title">Title</div>
      <div className="pr-grid-head pr-col-repo">Repository</div>
      <div className="pr-grid-head pr-col-branch">Branch</div>
      <div className="pr-grid-head pr-col-author">Author</div>
      <div className="pr-grid-head pr-col-badges" />

      {/* rows — display:contents so cells share the parent grid tracks */}
      {prs.map((pr) => (
        <div
          key={`${pr.providerId}-${pr.prId}`}
          className="pr-grid-row"
          role="button"
          tabIndex={0}
          title={pr.title}
          onClick={() => launcherApi.openUrl(pr.url)}
          onKeyDown={(e) => {
            if (e.key === "Enter" || e.key === " ") {
              e.preventDefault();
              launcherApi.openUrl(pr.url);
            }
          }}
        >
          <div className="pr-cell pr-col-provider">
            <img
              src={PROVIDER_ICONS[pr.providerId]}
              alt={pr.providerId}
              className="pr-provider-icon"
              draggable={false}
            />
          </div>
          <div className="pr-cell pr-col-title">
            <span className="item-name">{pr.title}</span>
          </div>
          <div className="pr-cell pr-col-repo">
            <span className="item-meta">{pr.repositoryName}</span>
          </div>
          <div className="pr-cell pr-col-branch">
            <div className="item-branch-row">
              <span className="item-branch">{pr.sourceBranch}</span>
              <span className="pr-branch-arrow">→</span>
              <span className="item-branch">{pr.targetBranch}</span>
            </div>
          </div>
          <div className="pr-cell pr-col-author">
            <span className="item-meta">{pr.authorDisplayName}</span>
          </div>
          <div className="pr-cell pr-col-badges">
            {pr.isDraft && (
              <span className="pr-chip pr-chip--draft">Draft</span>
            )}
            {!pr.isDraft && pr.createdByMe && (
              <span className="pr-chip pr-chip--mine">Mine</span>
            )}
            {pr.isReviewer && pr.reviewerVote !== 0 && (
              <span
                className={`pr-chip pr-chip--vote pr-chip--vote-${pr.reviewerVote > 0 ? "pos" : pr.reviewerVote === -5 ? "wait" : "neg"}`}
              >
                {VOTE_LABEL[pr.reviewerVote] ?? "Reviewed"}
              </span>
            )}
            {pr.isReviewer && pr.reviewerVote === 0 && (
              <span className="pr-chip pr-chip--reviewer">Reviewer</span>
            )}
          </div>
        </div>
      ))}
    </div>
  );
}
