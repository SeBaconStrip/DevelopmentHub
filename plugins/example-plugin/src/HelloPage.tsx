const { React, useQuery, apiBase } = window.__dhSdk;
const { useState } = React;

export default function HelloPage() {
  const [name, setName] = useState('');

  const { data, isLoading, refetch } = useQuery<{ message: string }>({
    queryKey: ['hello-plugin', 'greet', name],
    queryFn: async () => {
      const url = name
        ? `${apiBase}/plugins/hello/greet?name=${encodeURIComponent(name)}`
        : `${apiBase}/plugins/hello/greet`;
      const res = await fetch(url);
      if (!res.ok) throw new Error('Request failed');
      return res.json();
    },
    enabled: true,
  });

  return (
    <div className="page-root">
      <div className="card" style={{ maxWidth: 480, margin: '2rem auto', padding: '2rem' }}>
        <h2>👋 Hello Plugin Page</h2>
        <p>This page is contributed by the Example Plugin and calls its own backend.</p>

        <div style={{ display: 'flex', gap: '0.5rem', marginTop: '1rem' }}>
          <input
            className="input"
            type="text"
            placeholder="Enter your name…"
            value={name}
            onChange={(e: React.ChangeEvent<HTMLInputElement>) => setName(e.target.value)}
          />
          <button className="btn-ghost" onClick={() => refetch()}>
            Greet
          </button>
        </div>

        {isLoading && <p style={{ marginTop: '1rem', opacity: 0.6 }}>Loading…</p>}
        {data && (
          <p style={{ marginTop: '1rem', fontStyle: 'italic' }}>{data.message}</p>
        )}
      </div>
    </div>
  );
}
