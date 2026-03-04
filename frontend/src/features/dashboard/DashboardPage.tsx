import { useQuery } from '@tanstack/react-query';
import { repositoriesApi } from '../api/repositories';
import { pullRequestsApi } from '../api/pullRequests';
import { scriptsApi } from '../api/scripts';
import { Link } from 'react-router-dom';

function StatCard({ label, value, to }: { label: string; value: string | number; to: string }) {
  return (
    <Link to={to} className="bg-white border border-gray-200 rounded-xl p-5 flex flex-col gap-1 hover:shadow-md transition-shadow">
      <span className="text-3xl font-bold text-gray-900">{value}</span>
      <span className="text-sm text-gray-500">{label}</span>
    </Link>
  );
}

export function DashboardPage() {
  const { data: repos = [] } = useQuery({ queryKey: ['repositories'], queryFn: repositoriesApi.getAll });
  const { data: prs = [] } = useQuery({ queryKey: ['pullrequests'], queryFn: pullRequestsApi.getOpen, staleTime: 60_000 });
  const { data: executions = [] } = useQuery({ queryKey: ['executions'], queryFn: () => scriptsApi.getHistory(10), refetchInterval: 5000 });

  const runningCount = executions.filter(e => e.status === 'Running').length;
  const favoriteCount = repos.filter(r => r.isFavorite).length;

  return (
    <div className="p-6 flex flex-col gap-6">
      <h1 className="text-2xl font-bold text-gray-900">Dashboard</h1>

      <div className="grid grid-cols-2 sm:grid-cols-4 gap-4">
        <StatCard label="Repositories" value={repos.length} to="/repositories" />
        <StatCard label="Favorites" value={favoriteCount} to="/repositories" />
        <StatCard label="Open PRs" value={prs.length} to="/pull-requests" />
        <StatCard label="Running Scripts" value={runningCount} to="/scripts" />
      </div>

      {repos.slice(0, 5).length > 0 && (
        <section>
          <h2 className="text-sm font-semibold text-gray-500 uppercase tracking-wide mb-3">Recent Repositories</h2>
          <div className="bg-white border border-gray-200 rounded-lg divide-y divide-gray-100">
            {repos.slice(0, 5).map(r => (
              <div key={r.id} className="px-4 py-3 flex items-center justify-between text-sm">
                <div className="flex items-center gap-2">
                  {r.isFavorite && <span className="text-yellow-400">★</span>}
                  <span className="font-medium text-gray-800">{r.name}</span>
                  {r.currentBranch && (
                    <span className="text-xs bg-gray-100 text-gray-600 px-2 py-0.5 rounded font-mono">{r.currentBranch}</span>
                  )}
                </div>
                {r.lastOpenedAt && (
                  <span className="text-xs text-gray-400">{new Date(r.lastOpenedAt).toLocaleDateString()}</span>
                )}
              </div>
            ))}
          </div>
        </section>
      )}

      {executions.length > 0 && (
        <section>
          <h2 className="text-sm font-semibold text-gray-500 uppercase tracking-wide mb-3">Recent Script Runs</h2>
          <div className="bg-white border border-gray-200 rounded-lg divide-y divide-gray-100">
            {executions.slice(0, 5).map(e => (
              <div key={e.id} className="px-4 py-3 flex items-center justify-between text-sm">
                <span className="font-medium text-gray-800">{e.scriptName}</span>
                <div className="flex items-center gap-3">
                  <span className={`text-xs font-medium ${
                    e.status === 'Success' ? 'text-green-600' :
                    e.status === 'Running' ? 'text-blue-600' :
                    e.status === 'Failed' ? 'text-red-500' : 'text-gray-400'
                  }`}>{e.status}</span>
                  <span className="text-xs text-gray-400">{new Date(e.startedAt).toLocaleTimeString()}</span>
                </div>
              </div>
            ))}
          </div>
        </section>
      )}
    </div>
  );
}
