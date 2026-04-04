const { React, useQuery, useMutation, queryClient, apiBase, apiFetch, ui } = window.__dhSdk;
const { Button, Spinner } = ui;

const PLUGIN_ID = 'com.example.counter-plugin';

interface Props {
  compact?: boolean;
}

export default function CounterView({ compact }: Props) {
  const { data: settingsData } = useQuery<Record<string, string>>({
    queryKey: [PLUGIN_ID, 'settings'],
    queryFn: () => apiFetch(`${apiBase}/plugins/${encodeURIComponent(PLUGIN_ID)}/settings`).then(r => r.json()),
    staleTime: 0,
  });

  const step = parseInt(settingsData?.['step'] ?? '1', 10) || 1;

  const { data, isLoading } = useQuery<{ count: number }>({
    queryKey: ['counter-plugin', 'count'],
    queryFn: () => apiFetch(`${apiBase}/plugins/counter/count`).then(r => r.json()),
  });

  const add = useMutation({
    mutationFn: () =>
      apiFetch(`${apiBase}/plugins/counter/add?step=${step}`, { method: 'POST' }).then(r => r.json()),
    onSuccess: (result: { count: number }) => {
      queryClient.setQueryData(['counter-plugin', 'count'], result);
    },
  });

  if (isLoading) return React.createElement(Spinner, null);

  return React.createElement(
    'div',
    { style: { display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', gap: compact ? '0.5rem' : '1rem', padding: compact ? '1rem' : '2rem', height: '100%' } },
    React.createElement(
      'span',
      { style: { fontSize: compact ? 40 : 64, fontWeight: 700, color: 'var(--text)', lineHeight: 1 } },
      data?.count ?? 0,
    ),
    React.createElement(
      Button,
      { variant: 'primary', onClick: () => add.mutate(), disabled: add.isPending, style: compact ? {} : { minWidth: 120 } },
      add.isPending ? '…' : `+${step}`,
    ),
  );
}
