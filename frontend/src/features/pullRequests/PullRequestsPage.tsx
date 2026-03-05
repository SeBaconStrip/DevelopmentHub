import { useQuery } from "@tanstack/react-query";
import { pullRequestsApi } from "../../api/pullRequests";
import type { PullRequest } from "../../types";

const voteLabel: Record<number, string> = {
  10: "Approved",
  5: "Approved with suggestions",
  0: "No vote",
  [-5]: "Waiting",
  [-10]: "Rejected",
};

const voteColor: Record<number, string> = {
  10: "#43c59e",
  5: "#a3c95e",
  0: "#888",
  [-5]: "#f9c74f",
  [-10]: "#f05252",
};

export default function PullRequestsPage() {
  const {
    data: prs = [],
    isLoading,
    refetch,
    isFetching,
  } = useQuery<PullRequest[]>({
    queryKey: ["pullrequests"],
    queryFn: pullRequestsApi.getOpen,
    refetchInterval: 120_000,
  });

  if (isLoading) return <p style={{ padding: 32 }}>Loading pull requests…</p>;

  return (
    <div style={{ padding: 32 }}>
      <div
        style={{
          display: "flex",
          alignItems: "center",
          justifyContent: "space-between",
          marginBottom: 24,
        }}
      >
        <h1 style={{ margin: 0, fontSize: 24, fontWeight: 700 }}>
          Pull Requests
        </h1>
        <button
          onClick={() => refetch()}
          disabled={isFetching}
          style={{
            padding: "8px 18px",
            borderRadius: 8,
            border: "none",
            background: "#6c63ff",
            color: "#fff",
            cursor: "pointer",
            fontWeight: 600,
          }}
        >
          {isFetching ? "Refreshing…" : "Refresh"}
        </button>
      </div>

      {prs.length === 0 ? (
        <p style={{ color: "#888" }}>No open pull requests.</p>
      ) : (
        <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
          {prs.map((pr) => (
            <a
              key={pr.prId}
              href={pr.url}
              target="_blank"
              rel="noreferrer"
              style={{ textDecoration: "none", color: "inherit" }}
            >
              <div
                style={{
                  background: "rgba(255,255,255,0.06)",
                  borderRadius: 10,
                  padding: "14px 18px",
                  display: "flex",
                  alignItems: "center",
                  gap: 16,
                  cursor: "pointer",
                  transition: "background 0.15s",
                }}
                onMouseEnter={(e) =>
                  (e.currentTarget.style.background = "rgba(255,255,255,0.11)")
                }
                onMouseLeave={(e) =>
                  (e.currentTarget.style.background = "rgba(255,255,255,0.06)")
                }
              >
                <div style={{ flex: 1, minWidth: 0 }}>
                  <div
                    style={{
                      display: "flex",
                      alignItems: "center",
                      gap: 8,
                      flexWrap: "wrap",
                    }}
                  >
                    <span style={{ fontWeight: 600, fontSize: 15 }}>
                      {pr.title}
                    </span>
                    {pr.isDraft && (
                      <span
                        style={{
                          fontSize: 11,
                          background: "#555",
                          borderRadius: 4,
                          padding: "1px 6px",
                        }}
                      >
                        Draft
                      </span>
                    )}
                    {pr.createdByMe && (
                      <span
                        style={{
                          fontSize: 11,
                          background: "#3a5a8a",
                          borderRadius: 4,
                          padding: "1px 6px",
                        }}
                      >
                        Mine
                      </span>
                    )}
                  </div>
                  <div style={{ fontSize: 12, color: "#888", marginTop: 3 }}>
                    {pr.repositoryName} · {pr.sourceBranch} → {pr.targetBranch}{" "}
                    · {pr.authorDisplayName}
                  </div>
                </div>
                {pr.isReviewer && (
                  <span
                    style={{
                      fontSize: 12,
                      fontWeight: 600,
                      color: voteColor[pr.reviewerVote] ?? "#888",
                      whiteSpace: "nowrap",
                    }}
                  >
                    {voteLabel[pr.reviewerVote] ?? "No vote"}
                  </span>
                )}
              </div>
            </a>
          ))}
        </div>
      )}
    </div>
  );
}
