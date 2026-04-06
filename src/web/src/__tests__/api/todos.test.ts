import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { todosApi } from '../../api/todos';

// Mock the client module so we don't need a live server
vi.mock('../../api/client', () => ({
  apiFetch: vi.fn(),
}));

import { apiFetch } from '../../api/client';

function mockResponse(body: unknown, status = 200): Response {
  return new Response(
    status === 204 ? null : JSON.stringify(body),
    { status, headers: { 'Content-Type': 'application/json' } }
  );
}

describe('todosApi', () => {
  beforeEach(() => {
    vi.mocked(apiFetch).mockReset();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  describe('getAll', () => {
    it('calls GET /api/todos', async () => {
      vi.mocked(apiFetch).mockResolvedValue(mockResponse([]));
      await todosApi.getAll();
      expect(apiFetch).toHaveBeenCalledWith('/api/todos');
    });

    it('returns parsed todos', async () => {
      const todos = [{ id: '1', title: 'Test', completed: false }];
      vi.mocked(apiFetch).mockResolvedValue(mockResponse(todos));
      const result = await todosApi.getAll();
      expect(result).toEqual(todos);
    });
  });

  describe('create', () => {
    it('calls POST /api/todos with title and linkUrl', async () => {
      vi.mocked(apiFetch).mockResolvedValue(mockResponse({ id: '1', title: 'New' }));
      await todosApi.create('New todo', 'https://example.com');
      expect(apiFetch).toHaveBeenCalledWith('/api/todos', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ title: 'New todo', linkUrl: 'https://example.com' }),
      });
    });

    it('calls POST /api/todos with only title when no linkUrl', async () => {
      vi.mocked(apiFetch).mockResolvedValue(mockResponse({ id: '1', title: 'New' }));
      await todosApi.create('New todo');
      const body = JSON.parse(
        (vi.mocked(apiFetch).mock.calls[0][1] as RequestInit).body as string
      );
      expect(body.title).toBe('New todo');
    });
  });

  describe('setCompleted', () => {
    it('passes completed=true as query param', async () => {
      vi.mocked(apiFetch).mockResolvedValue(mockResponse({ id: '1', completed: true }));
      await todosApi.setCompleted('abc', true);
      expect(apiFetch).toHaveBeenCalledWith(
        '/api/todos/abc/complete?completed=true',
        { method: 'PATCH' }
      );
    });

    it('passes completed=false as query param', async () => {
      vi.mocked(apiFetch).mockResolvedValue(mockResponse({ id: '1', completed: false }));
      await todosApi.setCompleted('abc', false);
      expect(apiFetch).toHaveBeenCalledWith(
        '/api/todos/abc/complete?completed=false',
        { method: 'PATCH' }
      );
    });
  });

  describe('delete', () => {
    it('calls DELETE /api/todos/:id', async () => {
      vi.mocked(apiFetch).mockResolvedValue(mockResponse(null, 204));
      await todosApi.delete('xyz');
      expect(apiFetch).toHaveBeenCalledWith('/api/todos/xyz', { method: 'DELETE' });
    });
  });

  describe('clearCompleted', () => {
    it('calls DELETE /api/todos/completed', async () => {
      vi.mocked(apiFetch).mockResolvedValue(mockResponse({ removed: 3 }));
      const result = await todosApi.clearCompleted();
      expect(apiFetch).toHaveBeenCalledWith('/api/todos/completed', { method: 'DELETE' });
      expect(result).toEqual({ removed: 3 });
    });
  });

  describe('error handling', () => {
    it('throws with message from json detail field on non-ok response', async () => {
      vi.mocked(apiFetch).mockResolvedValue(
        new Response(JSON.stringify({ detail: 'Not found' }), { status: 404 })
      );
      await expect(todosApi.getAll()).rejects.toThrow('This todo no longer exists.');
    });

    it('throws with HTTP status on non-ok response with no body', async () => {
      vi.mocked(apiFetch).mockResolvedValue(new Response('', { status: 500 }));
      await expect(todosApi.getAll()).rejects.toThrow('HTTP 500');
    });
  });
});
