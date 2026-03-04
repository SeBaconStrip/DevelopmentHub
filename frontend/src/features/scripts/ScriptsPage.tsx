import { useState, useRef, useCallback } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { scriptsApi } from '../../api/scripts';
import { useLogHub } from '../../hooks/useLogHub';
import type { Execution } from '../../types';

function StatusBadge({ status }: { status: Execution['status'] }) {
  const colors: Record<string, string> = {
    Running: 'bg-blue-100 text-blue-700',
    Success: 'bg-green-100 text-green-700',
    Failed: 'bg-red-100 text-red-700',
    Cancelled: 'bg-gray-100 text-gray-600',
  };
  return (
    <span className={`text-xs px-2 py-0.5 rounded font-medium ${colors[status] ?? ''}`}>
      {status}
    </span>
  );
}

function LogViewer({ executionId }: { executionId: string }) {
  const [lines, setLines] = useState<string[]>([]);
  const [completed, setCompleted] = useState(false);
  const bottomRef = useRef<HTMLDivElement>(null);
  const queryClient = useQueryClient();

  const addLine = useCallback((data: { text: string }) => {
    setLines(prev => [...prev, data.text]);
    bottomRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, []);

  const onCompleted = useCallback(() => {
    setCompleted(true);
    queryClient.invalidateQueries({ queryKey: ['executions'] });
  }, [queryClient]);

  useLogHub(completed ? null : executionId, addLine, onCompleted);

  const { data: detail } = useQuery({
    queryKey: ['execution-detail', executionId],
    queryFn: () => scriptsApi.getDetail(executionId),
    enabled: completed,
  });

  const displayLines = completed && detail
    ? detail.outputLog.split('\n')
    : lines;

  return (
    <div className="bg-gray-950 text-gray-100 rounded-lg p-3 font-mono text-xs max-h-64 overflow-auto">
      {displayLines.map((line, i) => (
        <div key={i} className="whitespace-pre-wrap leading-5">{line}</div>
      ))}
      {!completed && <div className="animate-pulse text-gray-400">▮</div>}
      <div ref={bottomRef} />
    </div>
  );
}

export function ScriptsPage() {
  const [activeExecutionId, setActiveExecutionId] = useState<string | null>(null);

  const { data: scripts = [], isLoading } = useQuery({
    queryKey: ['scripts'],
    queryFn: scriptsApi.getAll,
  });

  const { data: history = [] } = useQuery({
    queryKey: ['executions'],
    queryFn: () => scriptsApi.getHistory(20),
    refetchInterval: 5000,
  });

  const execute = useMutation({
    mutationFn: (scriptId: string) => scriptsApi.execute(scriptId),
    onSuccess: (exec) => setActiveExecutionId(exec.id),
  });

  const cancel = useMutation({
    mutationFn: (id: string) => scriptsApi.cancel(id),
  });

  if (isLoading) return <p className="p-6 text-gray-500">Loading scripts…</p>;

  return (
    <div className="p-6 flex flex-col gap-6">
      <h1 className="text-2xl font-bold text-gray-900">Scripts</h1>

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
        {scripts.map(script => (
          <div key={script.id} className="bg-white border border-gray-200 rounded-lg p-4 flex flex-col gap-3 shadow-sm">
            <div>
              <h3 className="font-semibold text-gray-900">{script.name}</h3>
              {script.description && <p className="text-sm text-gray-500 mt-0.5">{script.description}</p>}
              <p className="text-xs font-mono text-gray-400 mt-1">
                {script.command} {script.arguments.join(' ')}
              </p>
            </div>
            <button
              onClick={() => execute.mutate(script.id)}
              disabled={execute.isPending}
              className="bg-blue-600 hover:bg-blue-700 text-white text-sm font-medium px-3 py-1.5 rounded-md transition-colors disabled:opacity-50 w-fit"
            >
              ▶ Run
            </button>
          </div>
        ))}
      </div>

      {activeExecutionId !== null && (
        <section>
          <div className="flex items-center justify-between mb-2">
            <h2 className="text-sm font-semibold text-gray-700">Live Output — Execution #{activeExecutionId}</h2>
            <button
              onClick={() => cancel.mutate(activeExecutionId)}
              className="text-xs text-red-500 hover:text-red-700"
            >
              ✕ Cancel
            </button>
          </div>
          <LogViewer executionId={activeExecutionId} />
        </section>
      )}

      {history.length > 0 && (
        <section>
          <h2 className="text-sm font-semibold text-gray-500 uppercase tracking-wide mb-3">Recent Executions</h2>
          <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
            <table className="w-full text-sm">
              <thead className="bg-gray-50 text-gray-500 text-xs uppercase">
                <tr>
                  <th className="px-4 py-2 text-left">Script</th>
                  <th className="px-4 py-2 text-left">Started</th>
                  <th className="px-4 py-2 text-left">Status</th>
                  <th className="px-4 py-2 text-left">Exit</th>
                  <th className="px-4 py-2 text-left">Logs</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {history.map(exec => (
                  <tr key={exec.id} className="hover:bg-gray-50">
                    <td className="px-4 py-2 font-medium text-gray-800">{exec.scriptName}</td>
                    <td className="px-4 py-2 text-gray-500">{new Date(exec.startedAt).toLocaleString()}</td>
                    <td className="px-4 py-2"><StatusBadge status={exec.status} /></td>
                    <td className="px-4 py-2 font-mono text-gray-600">{exec.exitCode ?? '—'}</td>
                    <td className="px-4 py-2">
                      <button
                        onClick={() => setActiveExecutionId(exec.id)}
                        className="text-blue-500 hover:underline text-xs"
                      >
                        View
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      )}
    </div>
  );
}
