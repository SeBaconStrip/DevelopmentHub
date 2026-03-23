const { React } = window.__dhSdk;

export default function HelloPage() {
  return (
    <div className="page-root">
      <div className="card" style={{ maxWidth: 480, margin: '2rem auto', padding: '2rem' }}>
        <h2>👋 Hello Plugin Page</h2>
        <p>This page is contributed by the Example Plugin.</p>
        <p>
          Plugin pages have access to the full host UI via CSS classes (
          <code>.card</code>, <code>.btn-ghost</code>, etc.) and Tailwind utilities.
        </p>
      </div>
    </div>
  );
}
