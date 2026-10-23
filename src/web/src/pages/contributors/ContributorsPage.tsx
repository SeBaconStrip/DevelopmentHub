import { useMemo, useState } from "react";
import { useMutation } from "@tanstack/react-query";
import { contributorsApi, type ContributorStat } from "../../api/contributors";
import { ContributorsTabs } from "./ContributorsTabs";
import {
  RepoSelectionPanel,
  RepoSelectionToolbar,
  ReportWarnings,
} from "./RepoSelection";
import { useRepoSelection } from "./useRepoSelection";
import "./ContributorsPage.css";

type SortKey = "authored" | "reviewed" | "name";

export default function ContributorsPage() {
  const [sortKey, setSortKey] = useState<SortKey>("authored");

  const statsMutation = useMutation({
    mutationFn: () =>
      contributorsApi.getStats({
        provider: selection.provider,
        repositoryIds: selection.selectedRepoIds,
        ...selection.dateRange(),
      }),
  });
  const selection = useRepoSelection(statsMutation.reset);

  const sortedContributors = useMemo(() => {
    const list = [...(statsMutation.data?.contributors ?? [])];
    list.sort((a, b) => {
      if (sortKey === "name")
        return a.displayName.localeCompare(b.displayName);
      if (sortKey === "reviewed")
        return b.reviewedCount - a.reviewedCount || b.authoredCount - a.authoredCount;
      return b.authoredCount - a.authoredCount || b.reviewedCount - a.reviewedCount;
    });
    return list;
  }, [statsMutation.data, sortKey]);

  const maxAuthored = useMemo(
    () => Math.max(1, ...sortedContributors.map((c) => c.authoredCount)),
    [sortedContributors],
  );

  return (
    <div className="contributors-page">
      <ContributorsTabs />
      <div className="contributors-card">
        <RepoSelectionToolbar
          selection={selection}
          isPending={statsMutation.isPending}
          onGenerate={() => statsMutation.mutate()}
        />
      </div>

      <div className="contributors-layout">
        <RepoSelectionPanel selection={selection} />

        <section className="contributors-results-card">
          {statsMutation.isError ? (
            <p className="contributors-empty contributors-empty--error">
              {statsMutation.error.message || "Failed to generate report."}
            </p>
          ) : statsMutation.isPending ? (
            <p className="contributors-empty">Aggregating pull requests…</p>
          ) : !statsMutation.data ? (
            <div className="contributors-empty">
              <p>Select repositories and a date range, then generate the report.</p>
            </div>
          ) : (
            <>
              <ReportWarnings warnings={statsMutation.data.warnings} />
              {sortedContributors.length === 0 ? (
                <p className="contributors-empty">
                  No pull requests found for the selected repositories and date range.
                </p>
              ) : (
                <ContributorsTable
                  contributors={sortedContributors}
                  maxAuthored={maxAuthored}
                  sortKey={sortKey}
                  onSort={setSortKey}
                />
              )}
            </>
          )}
        </section>
      </div>
    </div>
  );
}

function ContributorsTable({
  contributors,
  maxAuthored,
  sortKey,
  onSort,
}: {
  contributors: ContributorStat[];
  maxAuthored: number;
  sortKey: SortKey;
  onSort: (key: SortKey) => void;
}) {
  const sortIndicator = (key: SortKey) => (sortKey === key ? " \u25BE" : "");
  return (
    <table className="contributors-table">
      <thead>
        <tr>
          <th className="contributors-th-rank">#</th>
          <th
            className="contributors-th-name contributors-th-sortable"
            onClick={() => onSort("name")}
          >
            Contributor{sortIndicator("name")}
          </th>
          <th
            className="contributors-th-num contributors-th-sortable"
            onClick={() => onSort("authored")}
          >
            Authored{sortIndicator("authored")}
          </th>
          <th
            className="contributors-th-num contributors-th-sortable"
            onClick={() => onSort("reviewed")}
          >
            Reviewed{sortIndicator("reviewed")}
          </th>
          <th className="contributors-th-bar">Authored share</th>
        </tr>
      </thead>
      <tbody>
        {contributors.map((c, i) => (
          <tr key={c.login || c.displayName}>
            <td className="contributors-td-rank">{i + 1}</td>
            <td className="contributors-td-name">
              {c.avatarUrl ? (
                <img
                  className="contributors-avatar"
                  src={c.avatarUrl}
                  alt=""
                  loading="lazy"
                />
              ) : (
                <span className="contributors-avatar contributors-avatar--fallback">
                  {(c.displayName || c.login).charAt(0).toUpperCase()}
                </span>
              )}
              <span className="contributors-name-text" title={c.login}>
                {c.displayName || c.login}
              </span>
            </td>
            <td className="contributors-td-num">{c.authoredCount}</td>
            <td className="contributors-td-num">{c.reviewedCount}</td>
            <td className="contributors-td-bar">
              <div className="contributors-bar-track">
                <div
                  className="contributors-bar-fill"
                  style={{ width: `${(c.authoredCount / maxAuthored) * 100}%` }}
                />
              </div>
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}
