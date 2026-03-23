import { Suspense } from 'react';
import { pluginRegistry } from './PluginRegistry';

export function PluginWidget({ widgetId }: { widgetId: string }) {
  const registered = pluginRegistry.widgets.find((w) => w.widgetId === widgetId);
  if (!registered) {
    return <div className="empty-msg">Plugin widget not loaded: {widgetId}</div>;
  }

  const Component = registered.component;
  return (
    <Suspense fallback={<div className="empty-msg">Loading…</div>}>
      <Component />
    </Suspense>
  );
}
