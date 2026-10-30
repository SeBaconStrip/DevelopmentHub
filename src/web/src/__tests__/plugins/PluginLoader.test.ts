import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { loadAllPlugins, getPluginLoadErrors } from '../../plugins/PluginLoader';

const manifest = {
  id: 'com.test.plugin',
  version: '1.0.0',
  name: 'Test',
  contributes: { widgets: [], routes: [] },
  frontend: { bundle: 'ui/index.js', enabled: true, sdkVersion: '1' },
};

type ScriptOutcome = (script: HTMLScriptElement) => void;

/** Replaces script injection: jsdom does not run module scripts, so each test decides what the "browser" reports. */
function simulateScript(outcome: ScriptOutcome) {
  const original = document.head.appendChild.bind(document.head);
  return vi.spyOn(document.head, 'appendChild').mockImplementation(<T extends Node>(node: T): T => {
    if (node instanceof HTMLScriptElement) {
      queueMicrotask(() => outcome(node));
      return node;
    }
    return original(node);
  });
}

function windowError(message: string, filename: string) {
  window.dispatchEvent(new ErrorEvent('error', { message, filename }));
}

describe('loadAllPlugins', () => {
  const originalFetch = global.fetch;

  beforeEach(() => {
    global.fetch = vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      if (url === '/api/plugins/enabled') return new Response(JSON.stringify([manifest]), { status: 200 });
      if (url === '/api/config') return new Response('{}', { status: 200 });
      return new Response('', { status: 200 });
    }) as typeof fetch;
  });

  afterEach(() => {
    global.fetch = originalFetch;
    vi.restoreAllMocks();
  });

  it('records no error when the bundle loads cleanly', async () => {
    simulateScript((s) => s.onload?.(new Event('load')));

    await loadAllPlugins();

    expect(getPluginLoadErrors().size).toBe(0);
  });

  it('reports an exception thrown while the bundle evaluates, even though load fires', async () => {
    simulateScript((s) => {
      windowError('Uncaught Error: boom', `http://localhost/api/plugins/${manifest.id}/ui/bundle.js?v=1`);
      s.onload?.(new Event('load'));
    });

    await loadAllPlugins();

    expect(getPluginLoadErrors().get(manifest.id)).toContain('Uncaught Error: boom');
  });

  it('reports an unresolvable import, which the browser attributes to the document', async () => {
    simulateScript((s) => {
      windowError('Uncaught TypeError: Failed to resolve module specifier "react".', 'http://localhost/settings');
      s.onload?.(new Event('load'));
    });

    await loadAllPlugins();

    expect(getPluginLoadErrors().get(manifest.id)).toContain('Failed to resolve module specifier "react"');
  });

  it('ignores errors from other scripts while the bundle loads', async () => {
    simulateScript((s) => {
      windowError('Uncaught Error: host bug', 'http://localhost/assets/index-abc.js');
      s.onload?.(new Event('load'));
    });

    await loadAllPlugins();

    expect(getPluginLoadErrors().size).toBe(0);
  });

  it('asks the server why the bundle failed when the script errors', async () => {
    vi.mocked(global.fetch).mockImplementation(async (input: RequestInfo | URL) => {
      const url = String(input);
      if (url === '/api/plugins/enabled') return new Response(JSON.stringify([manifest]), { status: 200 });
      if (url === '/api/config') return new Response('{}', { status: 200 });
      return new Response('', { status: 404, statusText: 'Not Found' });
    });
    simulateScript((s) => s.onerror?.(new Event('error')));

    await loadAllPlugins();

    expect(getPluginLoadErrors().get(manifest.id)).toContain('HTTP 404');
  });
});
