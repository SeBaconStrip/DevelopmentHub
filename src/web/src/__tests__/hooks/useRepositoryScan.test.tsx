import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import React from 'react';
import { useRepositoryScan } from '../../hooks/useRepositoryScan';

vi.mock('../../api/repositories', () => ({
  repositoriesApi: {
    scan: vi.fn(),
  },
}));

import { repositoriesApi } from '../../api/repositories';

function makeWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return { wrapper: ({ children }: { children: React.ReactNode }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  ), queryClient };
}

describe('useRepositoryScan', () => {
  beforeEach(() => {
    vi.mocked(repositoriesApi.scan).mockReset();
  });

  it('returns a mutation object', () => {
    const { wrapper } = makeWrapper();
    const { result } = renderHook(() => useRepositoryScan(), { wrapper });
    expect(result.current.mutate).toBeDefined();
    expect(result.current.mutateAsync).toBeDefined();
  });

  it('calls repositoriesApi.scan when mutated', async () => {
    const repos = [{ id: 'r1', name: 'Repo', path: '/path' }];
    vi.mocked(repositoriesApi.scan).mockResolvedValue(repos as any);

    const { wrapper } = makeWrapper();
    const { result } = renderHook(() => useRepositoryScan(), { wrapper });

    await act(async () => {
      await result.current.mutateAsync();
    });

    expect(repositoriesApi.scan).toHaveBeenCalledOnce();
  });

  it('invalidates repositories query on success', async () => {
    vi.mocked(repositoriesApi.scan).mockResolvedValue(undefined as any);

    const { wrapper, queryClient } = makeWrapper();
    const invalidateSpy = vi.spyOn(queryClient, 'invalidateQueries');

    const { result } = renderHook(() => useRepositoryScan(), { wrapper });

    await act(async () => {
      await result.current.mutateAsync();
    });

    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ['repositories'] });
  });
});
