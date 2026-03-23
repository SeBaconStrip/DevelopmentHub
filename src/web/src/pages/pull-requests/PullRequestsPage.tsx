import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { fetchPullRequests } from "../../api/pullRequests";
import { PullRequestsWidget } from "../dashboard/widgets/PullRequestsWidget";
import type { PullRequest } from "../../types";
import "./PullRequestsPage.css";

type Filter = "all" | "mine" | "reviewer" | "draft";

export default function PullRequestsPage() {
  const [search, setSearch] = useState("");
  const [filter, setFilter] = useState<Filter>("all");

  const { data: prs = [], isLoading } = useQuery<PullRequest[]>({
    queryKey: ["pullrequests"],
    queryFn: fetchPullRequests,
  });

  const filtered = prs
    .filter((pr) => {
      if (filter === "mine") return pr.createdByMe && !pr.isDraft;
      if (filter === "reviewer") return pr.isReviewer;
      if (filter === "draft") return pr.isDraft;
      return true;
    })
    .filter(
      (pr) =>
        !search ||
        pr.title.toLowerCase().includes(search.toLowerCase()) ||
        pr.repositoryName.toLowerCase().includes(search.toLowerCase()) ||
        pr.authorDisplayName.toLowerCase().includes(search.toLowerCase()),
    );

  const mineCount = prs.filter((pr) => pr.createdByMe && !pr.isDraft).length;
  const reviewerCount = prs.filter((pr) => pr.isReviewer).length;
  const draftCount = prs.filter((pr) => pr.isDraft).length;

  return (
    <div className="pr-page">
      <div className="pr-page-card">
        <div className="pr-page-toolbar">
          <div className="pr-page-toolbar-left">
            <input
              className="pr-page-search"
              type="search"
              placeholder="Search by title, repo or author…"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
            <div className="pr-page-filters">
              {(["all", "mine", "reviewer", "draft"] as Filter[]).map((f) => (
                <button
                  key={f}
                  className={`pr-page-filter-btn${filter === f ? " pr-page-filter-btn--active" : ""}`}
                  onClick={() => setFilter(f)}
                >
                  {f === "all" && `All (${prs.length})`}
                  {f === "mine" && `Mine${mineCount > 0 ? ` (${mineCount})` : ""}`}
                  {f === "reviewer" && `Reviewer${reviewerCount > 0 ? ` (${reviewerCount})` : ""}`}
                  {f === "draft" && `Draft${draftCount > 0 ? ` (${draftCount})` : ""}`}
                </button>
              ))}
            </div>
          </div>
          {filtered.length !== prs.length && (
            <span className="pr-page-count">{filtered.length} shown</span>
          )}
        </div>
      </div>

      {isLoading ? (
        <p className="pr-page-empty">Loading…</p>
      ) : (
        <PullRequestsWidget prs={filtered} />
      )}
    </div>
  );
}
