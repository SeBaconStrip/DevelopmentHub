const { React } = window.__dhSdk;
const { useState } = React;

export default function HelloWidget() {
  const [count, setCount] = useState(0);

  return (
    <div style={{ padding: '1rem' }}>
      <p>👋 Hello from the Example Plugin!</p>
      <p>Clicked {count} times.</p>
      <button className="btn-ghost" onClick={() => setCount((c) => c + 1)}>
        Click me
      </button>
    </div>
  );
}
