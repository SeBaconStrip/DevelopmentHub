import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { repositoriesApi } from '../../api/repositories';
import type { Repository, OpenRepositoryRequest } from '../../types';

function BranchBadge({ branch, ahead, behind }: { branch: string | null; ahead: number; behind: number }) {
  if (!branch) return null;
  return (
    <span className="inline-flex items-center gap-1 text-xs bg-gray-100 text-gray-700 rounded px-2 py-0.5">
      <span className="font-mono">{branch}</span>
      {ahead > 0 && <span className="text-green-600">↑{ahead}</span>}
      {behind > 0 && <span className="text-red-500">↓{behind}</span>}
    </span>
  );
}

function RepositoryCard({ repo }: { repo: Repository }) {
  const queryClient = useQueryClient();

  const favorite = useMutation({
    mutationFn: () => repositoriesApi.toggleFavorite(repo.id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['repositories'] }),
  });

  const open = useMutation({
    mutationFn: (req: OpenRepositoryRequest) => repositoriesApi.open(repo.id, req),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['repositories'] }),
  });

  const sync = useMutation({
    mutationFn: () => repositoriesApi.sync(repo.id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['repositories'] }),
  });

  const slnEntry = repo.entryPoints.find(e => e.type === 'Solution');
  const workspaceEntry = repo.entryPoints.find(e => e.type === 'CodeWorkspace');
  const defaultVsCodeEntry = workspaceEntry ?? repo.entryPoints[0];

  return (
    <div className="bg-white border border-gray-200 rounded-lg p-4 flex flex-col gap-3 shadow-sm hover:shadow-md transition-shadow">
      <div className="flex items-start justify-between gap-2">
        <div className="flex flex-col gap-1 min-w-0">
          <div className="flex items-center gap-2">
            <h3 className="font-semibold text-gray-900 truncate">{repo.name}</h3>
            {repo.isFavorite && <span className="text-yellow-400 text-sm">★</span>}
          </div>
          <p className="text-xs text-gray-400 truncate font-mono">{repo.path}</p>
        </div>
        <BranchBadge branch={repo.currentBranch} ahead={repo.aheadBy} behind={repo.behindBy} />
      </div>

      <div className="text-xs text-gray-500 flex gap-3">
        <span>Opens: {repo.openCount}</span>
        {repo.lastOpenedAt && (
          <span>Last: {new Date(repo.lastOpenedAt).toLocaleDateString()}</span>
        )}
        {repo.lastSyncedAt && (
          <span>Synced: {new Date(repo.lastSyncedAt).toLocaleDateString()}</span>
        )}
      </div>

      <div className="flex flex-wrap gap-2">
        {slnEntry && (
          <button
            onClick={() => open.mutate({ entryPointPath: slnEntry.filePath, openWith: 'VisualStudio' })}
            className="text-xs bg-purple-100 text-purple-700 hover:bg-purple-200 px-2 py-1 rounded font-medium transition-colors"
          >
            VS: {slnEntry.fileName}
          </button>
        )}
        <button
          onClick={() =>
            open.mutate({
              entryPointPath: defaultVsCodeEntry?.filePath,
              openWith: 'VsCode',
            })
          }
          className="text-xs bg-blue-100 text-blue-700 hover:bg-blue-200 px-2 py-1 rounded font-medium transition-colors"
        >
          {defaultVsCodeEntry ? `Code: ${defaultVsCodeEntry.fileName}` : 'Open in VS Code'}
        </button>
        <button
          onClick={() => sync.mutate()}
          disabled={sync.isPending}
          className="text-xs bg-green-100 text-green-700 hover:bg-green-200 px-2 py-1 rounded font-medium transition-colors disabled:opacity-50"
        >
          {sync.isPending ? 'Syncing…' : '↻ Sync'}
        </button>
        <button
          onClick={() => favorite.mutate()}
          className="text-xs bg-gray-100 text-gray-600 hover:bg-yellow-100 hover:text-yellow-700 px-2 py-1 rounded font-medium transition-colors ml-auto"
        >
          {repo.isFavorite ? '★ Unfav' : '☆ Fav'}
        </button>
      </div>

      {sync.isSuccess && (
        <pre className="text-xs bg-gray-50 text-gray-700 rounded p-2 max-h-32 overflow-auto font-mono whitespace-pre-wrap">
          {sync.data.output || (sync.data.success ? 'Already up to date.' : 'Sync failed.')}
        </pre>
      )}
    </div>
  );
}

export function RepositoriesPage() {
  const queryClient = useQueryClient();

  const { data: repos = [], isLoading, isError } = useQuery({
    queryKey: ['repositories'],
    queryFn: repositoriesApi.getAll,
  });

  const scan = useMutation({
    mutationFn: repositoriesApi.scan,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['repositories'] }),
  });

  if (isLoading) return <p className="p-6 text-gray-500">Loading repositories…</p>;
  if (isError) return <p className="p-6 text-red-500">Failed to load repositories.</p>;

  const favorites = repos.filter(r => r.isFavorite);
  const rest = repos.filter(r => !r.isFavorite);

  return (
    <div className="p-6 flex flex-col gap-6">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold text-gray-900">Repositories</h1>
        <button
          onClick={() => scan.mutate()}
          disabled={scan.isPending}
          className="bg-blue-600 hover:bg-blue-700 text-white text-sm font-medium px-4 py-2 rounded-lg transition-colors disabled:opacity-50"
        >
          {scan.isPending ? 'Scanning…' : '🔍 Scan'}
        </button>
      </div>

      {repos.length === 0 && (
        <p className="text-gray-500 text-sm">No repositories found. Configure root directories in Settings and click Scan.</p>
      )}

      {favorites.length > 0 && (
        <section>
          <h2 className="text-sm font-semibold text-gray-500 uppercase tracking-wide mb-3">★ Favorites</h2>
          <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
            {favorites.map(r => <RepositoryCard key={r.id} repo={r} />)}
          </div>
        </section>
      )}

      {rest.length > 0 && (
        <section>
          <h2 className="text-sm font-semibold text-gray-500 uppercase tracking-wide mb-3">All Repositories</h2>
          <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
            {rest.map(r => <RepositoryCard key={r.id} repo={r} />)}
          </div>
        </section>
      )}
    </div>
  );
}
