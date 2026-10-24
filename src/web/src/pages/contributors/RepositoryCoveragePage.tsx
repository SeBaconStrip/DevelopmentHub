import { useMemo, useState } from "react";
import { useMutation } from "@tanstack/react-query";
import {
  contributorsApi,
  type ContributorCoverage,
} from "../../api/contributors";
import { ContributorsTabs } from "./ContributorsTabs";
import {
  RepoSelectionPanel,
  RepoSelectionToolbar,
  ReportWarnings,
} from "./RepoSelection";
import { useRepoSelection } from "./useRepoSelection";
import "./ContributorsPage.css";
import "./RepositoryCoveragePage.css";

type ViewMode = "byContributor" | "combined";

interface CombinedRepoContributor {
  login: string;
  displayName: string;
  avatarUrl: string;
  authoredCount: number;
  reviewedCount: number;
  filesAdded: number;
  filesEdited: number;
  filesDeleted: number;
}

interface CombinedRepo {
  repositoryId: string;
  repositoryName: string;
  totalAuthored: number;
  totalReviewed: number;
  filesAdded: number;
  filesEdited: number;
  filesDeleted: number;
  contributors: CombinedRepoContributor[];
}

function parseContributors(raw: string): string[] {
  return Array.from(
    new Set(
      raw
        .split(/[\n,]/)
        .map((s) => s.trim())
        .filter(Boolean),
    ),
  );
}

export default function RepositoryCoveragePage() {
  const [contributorsText, setContributorsText] = useState("");
  const [viewMode, setViewMode] = useState<ViewMode>("byContributor");

  const coverageMutation = useMutation({
    mutationFn: () =>
      contributorsApi.getRepositoryCoverage({
        provider: selection.provider,
        repositoryIds: selection.selectedRepoIds,
        contributors: parseContributors(contributorsText),
        ...selection.dateRange(),
      }),
  });
  const selection = useRepoSelection(coverageMutation.reset);

  const contributors = coverageMutation.data?.contributors ?? [];

  const combinedRepos = useMemo<CombinedRepo[]>(() => {
    const map = new Map<string, CombinedRepo>();
    for (const c of contributors) {
      for (const r of c.repositories) {
        let entry = map.get(r.repositoryId);
        if (!entry) {
          entry = {
            repositoryId: r.repositoryId,
            repositoryName: r.repositoryName,
            totalAuthored: 0,
            totalReviewed: 0,
            filesAdded: 0,
            filesEdited: 0,
            filesDeleted: 0,
            contributors: [],
          };
          map.set(r.repositoryId, entry);
        }
        entry.totalAuthored += r.authoredCount;
        entry.totalReviewed += r.reviewedCount;
        entry.filesAdded += r.filesAdded;
        entry.filesEdited += r.filesEdited;
        entry.filesDeleted += r.filesDeleted;
        entry.contributors.push({
          login: c.login,
          displayName: c.displayName,
          avatarUrl: c.avatarUrl,
          authoredCount: r.authoredCount,
          reviewedCount: r.reviewedCount,
          filesAdded: r.filesAdded,
          filesEdited: r.filesEdited,
          filesDeleted: r.filesDeleted,
        });
      }
    }
    return Array.from(map.values())
      .map((e) => ({
        ...e,
        contributors: e.contributors.sort(
          (a, b) =>
            b.authoredCount + b.reviewedCount - (a.authoredCount + a.reviewedCount),
        ),
      }))
      .sort(
        (a, b) =>
          b.totalAuthored + b.totalReviewed - (a.totalAuthored + a.totalReviewed) ||
          a.repositoryName.localeCompare(b.repositoryName),
      );
  }, [contributors]);

  const showCombined = viewMode === "combined" && contributors.length > 1;

  return (
    <div className="contributors-page">
      <ContributorsTabs />
      <div className="contributors-card">
        <RepoSelectionToolbar
          selection={selection}
          isPending={coverageMutation.isPending}
          onGenerate={() => coverageMutation.mutate()}
        />

        <label className="coverage-contributors-field">
          <span className="coverage-contributors-label">
            Contributors <em>(optional)</em>
          </span>
          <textarea
            className="coverage-contributors-input"
            rows={2}
            placeholder="One per line or comma-separated: login (user@company.com) or display name. Leave empty to include everyone."
            value={contributorsText}
            onChange={(e) => setContributorsText(e.target.value)}
          />
        </label>
      </div>

      <div className="contributors-layout">
        <RepoSelectionPanel selection={selection} />

        <section className="contributors-results-card">
          {coverageMutation.isError ? (
            <p className="contributors-empty contributors-empty--error">
              {coverageMutation.error.message || "Failed to generate report."}
            </p>
          ) : coverageMutation.isPending ? (
            <p className="contributors-empty">Mapping repository activity...</p>
          ) : !coverageMutation.data ? (
            <div className="contributors-empty">
              <p>
                Select repositories and a date range (optionally name specific
                contributors), then generate the report.
              </p>
            </div>
          ) : contributors.length === 0 ? (
            <>
              <ReportWarnings warnings={coverageMutation.data.warnings} />
              <p className="contributors-empty">
                No matching contributions found for the selected repositories, date
                range, and contributors.
              </p>
            </>
          ) : (
            <div className="coverage-results-wrap">
              <ReportWarnings warnings={coverageMutation.data.warnings} />
              {contributors.length > 1 && (
                <div className="coverage-viewtoggle">
                  <button
                    className={
                      "coverage-viewtoggle-btn" +
                      (viewMode === "byContributor"
                        ? " coverage-viewtoggle-btn--active"
                        : "")
                    }
                    onClick={() => setViewMode("byContributor")}
                  >
                    Per contributor
                  </button>
                  <button
                    className={
                      "coverage-viewtoggle-btn" +
                      (viewMode === "combined"
                        ? " coverage-viewtoggle-btn--active"
                        : "")
                    }
                    onClick={() => setViewMode("combined")}
                  >
                    Combined
                  </button>
                </div>
              )}

              {showCombined ? (
                <CombinedView
                  repositories={combinedRepos}
                  contributorCount={contributors.length}
                />
              ) : (
                <div className="coverage-results">
                  {contributors.map((c) => (
                    <CoverageCard key={c.login || c.displayName} contributor={c} />
                  ))}
                </div>
              )}
            </div>
          )}
        </section>
      </div>
    </div>
  );
}

function FilesChip({
  added,
  edited,
  deleted,
}: {
  added: number;
  edited: number;
  deleted: number;
}) {
  if (added + edited + deleted === 0) return null;
  return (
    <span
      className="coverage-chip coverage-chip--files"
      title={`Files: ${added} added, ${edited} edited, ${deleted} deleted`}
    >
      +{added} ~{edited} -{deleted}
    </span>
  );
}

function FilesBadge({
  added,
  edited,
  deleted,
}: {
  added: number;
  edited: number;
  deleted: number;
}) {
  const total = added + edited + deleted;
  if (total === 0) return null;
  return (
    <span
      className="coverage-badge coverage-badge--files"
      title={`Files: ${added} added, ${edited} edited, ${deleted} deleted`}
    >
      {total} files
    </span>
  );
}

function CombinedView({
  repositories,
  contributorCount,
}: {
  repositories: CombinedRepo[];
  contributorCount: number;
}) {
  return (
    <div className="coverage-combined">
      <p className="coverage-combined-summary">
        {repositories.length} repositories touched by {contributorCount}{" "}
        contributors
      </p>
      {repositories.map((r) => (
        <article className="coverage-combined-card" key={r.repositoryId}>
          <header className="coverage-combined-head">
            <span className="coverage-combined-repo" title={r.repositoryName}>
              {r.repositoryName}
            </span>
            <div className="coverage-totals">
              <span className="coverage-badge coverage-badge--repos">
                {r.contributors.length} contributors
              </span>
              <span className="coverage-badge">{r.totalAuthored} authored</span>
              <span className="coverage-badge">{r.totalReviewed} reviewed</span>
              <FilesBadge
                added={r.filesAdded}
                edited={r.filesEdited}
                deleted={r.filesDeleted}
              />
            </div>
          </header>
          <ul className="coverage-combined-people">
            {r.contributors.map((p) => (
              <li
                className="coverage-combined-person"
                key={p.login || p.displayName}
              >
                {p.avatarUrl ? (
                  <img
                    className="coverage-avatar coverage-avatar--sm"
                    src={p.avatarUrl}
                    alt=""
                    loading="lazy"
                  />
                ) : (
                  <span className="coverage-avatar coverage-avatar--sm coverage-avatar--fallback">
                    {(p.displayName || p.login).charAt(0).toUpperCase()}
                  </span>
                )}
                <span
                  className="coverage-combined-person-name"
                  title={p.login}
                >
                  {p.displayName}
                </span>
                <span className="coverage-repo-counts">
                  {p.authoredCount > 0 && (
                    <span className="coverage-chip coverage-chip--authored">
                      {p.authoredCount} authored
                    </span>
                  )}
                  {p.reviewedCount > 0 && (
                    <span className="coverage-chip coverage-chip--reviewed">
                      {p.reviewedCount} reviewed
                    </span>
                  )}
                  <FilesChip
                    added={p.filesAdded}
                    edited={p.filesEdited}
                    deleted={p.filesDeleted}
                  />
                </span>
              </li>
            ))}
          </ul>
        </article>
      ))}
    </div>
  );
}

function CoverageCard({ contributor }: { contributor: ContributorCoverage }) {
  return (
    <article className="coverage-card">
      <header className="coverage-card-head">
        {contributor.avatarUrl ? (
          <img
            className="coverage-avatar"
            src={contributor.avatarUrl}
            alt=""
            loading="lazy"
          />
        ) : (
          <span className="coverage-avatar coverage-avatar--fallback">
            {(contributor.displayName || contributor.login)
              .charAt(0)
              .toUpperCase()}
          </span>
        )}
        <div className="coverage-identity">
          <span className="coverage-name">{contributor.displayName}</span>
          {contributor.login &&
            contributor.login !== contributor.displayName && (
              <span className="coverage-login">{contributor.login}</span>
            )}
        </div>
        <div className="coverage-totals">
          <span className="coverage-badge coverage-badge--repos">
            {contributor.repositories.length} repos
          </span>
          <span className="coverage-badge">
            {contributor.totalAuthored} authored
          </span>
          <span className="coverage-badge">
            {contributor.totalReviewed} reviewed
          </span>
          <FilesBadge
            added={contributor.totalFilesAdded}
            edited={contributor.totalFilesEdited}
            deleted={contributor.totalFilesDeleted}
          />
        </div>
      </header>
      <ul className="coverage-repo-list">
        {contributor.repositories.map((r) => (
          <li className="coverage-repo" key={r.repositoryId}>
            <span className="coverage-repo-name" title={r.repositoryName}>
              {r.repositoryName}
            </span>
            <span className="coverage-repo-counts">
              {r.authoredCount > 0 && (
                <span className="coverage-chip coverage-chip--authored">
                  {r.authoredCount} authored
                </span>
              )}
              {r.reviewedCount > 0 && (
                <span className="coverage-chip coverage-chip--reviewed">
                  {r.reviewedCount} reviewed
                </span>
              )}
              <FilesChip
                added={r.filesAdded}
                edited={r.filesEdited}
                deleted={r.filesDeleted}
              />
            </span>
          </li>
        ))}
      </ul>
    </article>
  );
}
