import { Suspense } from 'react';
import { pluginRegistry } from './PluginRegistry';

export function PluginRoute({ path }: { path: string }) {
  const registered = pluginRegistry.routes.find((r) => r.path === path);
  if (!registered) {
    return <div className="empty-msg">Plugin page not found: {path}</div>;
  }

  const Component = registered.component;
  return (
    <Suspense fallback={<div className="empty-msg">Loading…</div>}>
      <Component />
    </Suspense>
  );
}
