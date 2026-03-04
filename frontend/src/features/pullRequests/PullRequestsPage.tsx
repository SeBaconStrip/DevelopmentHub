import { useQuery } from '@tanstack/react-query';
import { pullRequestsApi } from '../../api/pullRequests';
import type { PullRequest } from '../../types';

const voteLabels: Record<number, { label: string; color: string }> = {
  10: { label: 'Approved', color: 'text-green-600' },
  5: { label: 'Approved w/ suggestions', color: 'text-yellow-600' },
  0: { label: 'No vote', color: 'text-gray-400' },
  [-5]: { label: 'Waiting', color: 'text-orange-500' },
  [-10]: { label: 'Rejected', color: 'text-red-600' },
};

function PrRow({ pr }: { pr: PullRequest }) {
  const vote = voteLabels[pr.reviewerVote] ?? voteLabels[0];
  return (
    <tr className="hover:bg-gray-50">
      <td className="px-4 py-3">
        <div className="flex flex-col gap-0.5">
          <a
            href={pr.url}
            target="_blank"
            rel="noreferrer"
            className="text-blue-600 hover:underline font-medium text-sm"
          >
            {pr.title}
            {pr.isDraft && <span className="ml-2 text-xs text-gray-400">[Draft]</span>}
          </a>
          <span className="text-xs text-gray-400 font-mono">
            {pr.sourceBranch} → {pr.targetBranch}
          </span>
        </div>
      </td>
      <td className="px-4 py-3 text-sm text-gray-700">{pr.repositoryName}</td>
      <td className="px-4 py-3">
        <div className="flex flex-col gap-1">
          {pr.createdByMe && (
            <span className="text-xs bg-purple-100 text-purple-700 px-1.5 py-0.5 rounded w-fit">Author</span>
          )}
          {pr.isReviewer && (
            <span className="text-xs bg-blue-100 text-blue-700 px-1.5 py-0.5 rounded w-fit">Reviewer</span>
          )}
        </div>
      </td>
      <td className={`px-4 py-3 text-xs font-medium ${vote.color}`}>{vote.label}</td>
      <td className="px-4 py-3 text-xs text-gray-400">{new Date(pr.createdAt).toLocaleDateString()}</td>
      <td className="px-4 py-3 text-xs text-gray-500">{pr.authorDisplayName}</td>
    </tr>
  );
}

export function PullRequestsPage() {
  const { data: prs = [], isLoading, isError, dataUpdatedAt, refetch } = useQuery({
    queryKey: ['pullrequests'],
    queryFn: pullRequestsApi.getOpen,
    staleTime: 60_000,
    refetchInterval: 120_000,
  });

  const myPrs = prs.filter(p => p.createdByMe);
  const reviewPrs = prs.filter(p => p.isReviewer && !p.createdByMe);

  return (
    <div className="p-6 flex flex-col gap-6">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold text-gray-900">Pull Requests</h1>
        <div className="flex items-center gap-3 text-sm text-gray-400">
          {dataUpdatedAt > 0 && (
            <span>Updated: {new Date(dataUpdatedAt).toLocaleTimeString()}</span>
          )}
          <button onClick={() => refetch()} className="text-blue-500 hover:underline">↻ Refresh</button>
        </div>
      </div>

      {isLoading && <p className="text-gray-500">Loading pull requests…</p>}
      {isError && <p className="text-red-500">Failed to load pull requests. Check Azure DevOps configuration.</p>}

      {!isLoading && prs.length === 0 && (
        <p className="text-gray-500 text-sm">No open pull requests found.</p>
      )}

      {[
        { title: '📝 My Pull Requests', items: myPrs },
        { title: '👀 Reviewing', items: reviewPrs },
      ]
        .filter(s => s.items.length > 0)
        .map(section => (
          <section key={section.title}>
            <h2 className="text-sm font-semibold text-gray-500 uppercase tracking-wide mb-3">{section.title}</h2>
            <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
              <table className="w-full text-sm">
                <thead className="bg-gray-50 text-gray-500 text-xs uppercase">
                  <tr>
                    <th className="px-4 py-2 text-left">Title</th>
                    <th className="px-4 py-2 text-left">Repository</th>
                    <th className="px-4 py-2 text-left">Role</th>
                    <th className="px-4 py-2 text-left">Vote</th>
                    <th className="px-4 py-2 text-left">Created</th>
                    <th className="px-4 py-2 text-left">Author</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-100">
                  {section.items.map(pr => <PrRow key={pr.prId} pr={pr} />)}
                </tbody>
              </table>
            </div>
          </section>
        ))}
    </div>
  );
}
