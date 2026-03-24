import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { fetchPullRequests } from "../../api/pullRequests";
import { PullRequestsWidget } from "../dashboard/widgets/PullRequestsWidget";
import { FilterToolbar } from "../../components/FilterToolbar";
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

  const prFilters = [
    { value: "all", label: `All (${prs.length})` },
    { value: "mine", label: `Mine${mineCount > 0 ? ` (${mineCount})` : ""}` },
    { value: "reviewer", label: `Reviewer${reviewerCount > 0 ? ` (${reviewerCount})` : ""}` },
    { value: "draft", label: `Draft${draftCount > 0 ? ` (${draftCount})` : ""}` },
  ];

  return (
    <div className="pr-page">
      <div className="pr-page-card">
        <FilterToolbar
          search={search}
          onSearchChange={setSearch}
          filter={filter}
          onFilterChange={(f) => setFilter(f as Filter)}
          filters={prFilters}
          searchPlaceholder="Search by title, repo or author…"
          shownCount={filtered.length}
          totalCount={prs.length}
        />
      </div>

      {isLoading ? (
        <p className="pr-page-empty">Loading…</p>
      ) : (
        <PullRequestsWidget prs={filtered} />
      )}
    </div>
  );
}
