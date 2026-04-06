import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { apiFetch } from '../../api/client';

describe('apiFetch', () => {
  const originalFetch = global.fetch;

  beforeEach(() => {
    global.fetch = vi.fn();
    (window as any).__devHubToken = 'test-token-123';
  });

  afterEach(() => {
    global.fetch = originalFetch;
    delete (window as any).__devHubToken;
  });

  it('attaches X-Dev-Hub-Token header from window.__devHubToken', async () => {
    vi.mocked(global.fetch).mockResolvedValue(new Response('{}', { status: 200 }));

    await apiFetch('/api/test');

    expect(global.fetch).toHaveBeenCalledOnce();
    const [, init] = vi.mocked(global.fetch).mock.calls[0];
    const headers = init?.headers as Headers;
    expect(headers.get('X-Dev-Hub-Token')).toBe('test-token-123');
  });

  it('sends empty token when window.__devHubToken is undefined', async () => {
    delete (window as any).__devHubToken;
    vi.mocked(global.fetch).mockResolvedValue(new Response('{}', { status: 200 }));

    await apiFetch('/api/test');

    const [, init] = vi.mocked(global.fetch).mock.calls[0];
    const headers = init?.headers as Headers;
    expect(headers.get('X-Dev-Hub-Token')).toBe('');
  });

  it('passes through the request URL', async () => {
    vi.mocked(global.fetch).mockResolvedValue(new Response('{}', { status: 200 }));

    await apiFetch('/api/repositories');

    const [url] = vi.mocked(global.fetch).mock.calls[0];
    expect(url).toBe('/api/repositories');
  });

  it('merges caller-provided headers with the token header', async () => {
    vi.mocked(global.fetch).mockResolvedValue(new Response('{}', { status: 200 }));

    await apiFetch('/api/test', {
      headers: { 'Content-Type': 'application/json' },
    });

    const [, init] = vi.mocked(global.fetch).mock.calls[0];
    const headers = init?.headers as Headers;
    expect(headers.get('Content-Type')).toBe('application/json');
    expect(headers.get('X-Dev-Hub-Token')).toBe('test-token-123');
  });

  it('passes through method and body from init', async () => {
    vi.mocked(global.fetch).mockResolvedValue(new Response('{}', { status: 200 }));

    await apiFetch('/api/todos', { method: 'POST', body: JSON.stringify({ title: 'test' }) });

    const [, init] = vi.mocked(global.fetch).mock.calls[0];
    expect(init?.method).toBe('POST');
    expect(init?.body).toBe(JSON.stringify({ title: 'test' }));
  });
});
