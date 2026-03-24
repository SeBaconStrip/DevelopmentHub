const { React, ui } = window.__dhSdk;
const { useState } = React;
const { Button, Chip, Empty } = ui;

export default function HelloWidget() {
  const [count, setCount] = useState(0);

  return (
    <div style={{ padding: '1rem', display: 'flex', flexDirection: 'column', gap: '0.75rem' }}>
      {count === 0 ? (
        <Empty>Click the button to get started.</Empty>
      ) : (
        <p style={{ margin: 0, color: 'var(--text-body)', fontSize: 13 }}>
          Clicked <Chip>{count}</Chip> {count === 1 ? 'time' : 'times'}.
        </p>
      )}
      <div>
        <Button variant="primary" onClick={() => setCount((c) => c + 1)}>
          👋 Click me
        </Button>
      </div>
    </div>
  );
}
