const { React, useQuery, apiBase, ui } = window.__dhSdk;
const { useState } = React;
const { PageRoot, Card, Input, Button, Spinner } = ui;

export default function HelloPage() {
  const [name, setName] = useState('');
  const [submitted, setSubmitted] = useState('');

  const { data, isFetching, refetch } = useQuery<{ message: string }>({
    queryKey: ['hello-plugin', 'greet', submitted],
    queryFn: async () => {
      const url = submitted
        ? `${apiBase}/plugins/hello/greet?name=${encodeURIComponent(submitted)}`
        : `${apiBase}/plugins/hello/greet`;
      const res = await fetch(url);
      if (!res.ok) throw new Error('Request failed');
      return res.json();
    },
  });

  function handleGreet() {
    setSubmitted(name);
    refetch();
  }

  return (
    <PageRoot>
      <Card style={{ maxWidth: 480, margin: '2rem auto', padding: '2rem' }}>
        <h2 style={{ margin: '0 0 0.5rem' }}>👋 Hello Plugin Page</h2>
        <p style={{ margin: '0 0 1.5rem', color: 'var(--text-secondary)' }}>
          This page is contributed by the Example Plugin and calls its own backend.
        </p>

        <div style={{ display: 'flex', gap: '0.5rem', marginBottom: '1rem' }}>
          <Input
            placeholder="Enter your name…"
            value={name}
            onChange={(e) => setName(e.target.value)}
            onKeyDown={(e) => e.key === 'Enter' && handleGreet()}
          />
          <Button variant="primary" onClick={handleGreet} style={{ flexShrink: 0 }}>
            Greet
          </Button>
        </div>

        {isFetching && <Spinner />}
        {!isFetching && data && (
          <p style={{ margin: 0, fontStyle: 'italic', color: 'var(--text-soft)' }}>
            {data.message}
          </p>
        )}
      </Card>
    </PageRoot>
  );
}
