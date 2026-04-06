import { describe, it, expect, vi, beforeEach } from 'vitest';
import { repositoriesApi } from '../../api/repositories';

vi.mock('../../api/client', () => ({
  apiFetch: vi.fn(),
}));

import { apiFetch } from '../../api/client';

function mockResponse(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  });
}

describe('repositoriesApi', () => {
  beforeEach(() => {
    vi.mocked(apiFetch).mockReset();
  });

  describe('getAll', () => {
    it('calls GET /api/repositories', async () => {
      vi.mocked(apiFetch).mockResolvedValue(mockResponse([]));
      await repositoriesApi.getAll();
      expect(apiFetch).toHaveBeenCalledWith('/api/repositories');
    });
  });

  describe('scan', () => {
    it('calls POST /api/repositories/scan', async () => {
      vi.mocked(apiFetch).mockResolvedValue(mockResponse([]));
      await repositoriesApi.scan();
      expect(apiFetch).toHaveBeenCalledWith('/api/repositories/scan', { method: 'POST' });
    });
  });

  describe('toggleFavorite', () => {
    it('calls PATCH /api/repositories/:id/favorite', async () => {
      vi.mocked(apiFetch).mockResolvedValue(mockResponse({ id: 'r1', isFavorite: true }));
      await repositoriesApi.toggleFavorite('r1');
      expect(apiFetch).toHaveBeenCalledWith('/api/repositories/r1/favorite', { method: 'PATCH' });
    });
  });

  describe('open', () => {
    it('calls POST /api/repositories/:id/open with request body', async () => {
      vi.mocked(apiFetch).mockResolvedValue(mockResponse({ id: 'r1' }));
      const req = { openerId: 'vscode', entryPointPath: '/path/to/file.code-workspace' };
      await repositoriesApi.open('r1', req);
      expect(apiFetch).toHaveBeenCalledWith('/api/repositories/r1/open', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(req),
      });
    });
  });

  describe('updateTags', () => {
    it('calls PATCH /api/repositories/:id/tags with tags array', async () => {
      vi.mocked(apiFetch).mockResolvedValue(mockResponse({ id: 'r1' }));
      await repositoriesApi.updateTags('r1', ['dotnet', 'react']);
      expect(apiFetch).toHaveBeenCalledWith('/api/repositories/r1/tags', {
        method: 'PATCH',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ tags: ['dotnet', 'react'] }),
      });
    });
  });

  describe('openWorkspace', () => {
    it('calls POST /api/repositories/open-workspace with repository IDs', async () => {
      vi.mocked(apiFetch).mockResolvedValue(new Response(null, { status: 200 }));
      await repositoriesApi.openWorkspace(['r1', 'r2']);
      expect(apiFetch).toHaveBeenCalledWith('/api/repositories/open-workspace', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ repositoryIds: ['r1', 'r2'] }),
      });
    });
  });

  describe('error handling', () => {
    it('throws on non-ok response', async () => {
      vi.mocked(apiFetch).mockResolvedValue(new Response('Internal error', { status: 500 }));
      await expect(repositoriesApi.getAll()).rejects.toThrow();
    });
  });
});
