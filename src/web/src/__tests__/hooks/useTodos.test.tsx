import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook, act, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import React from 'react';
import { useTodos } from '../../hooks/useTodos';

vi.mock('../../api/todos', () => ({
  todosApi: {
    create: vi.fn(),
    update: vi.fn(),
    setCompleted: vi.fn(),
    delete: vi.fn(),
    clearCompleted: vi.fn(),
  },
}));

import { todosApi } from '../../api/todos';

function makeWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return ({ children }: { children: React.ReactNode }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  );
}

describe('useTodos', () => {
  beforeEach(() => {
    vi.mocked(todosApi.create).mockReset();
    vi.mocked(todosApi.update).mockReset();
    vi.mocked(todosApi.setCompleted).mockReset();
    vi.mocked(todosApi.delete).mockReset();
    vi.mocked(todosApi.clearCompleted).mockReset();
  });

  it('exposes all mutation functions', () => {
    const { result } = renderHook(() => useTodos(), { wrapper: makeWrapper() });
    expect(result.current.createTodo).toBeDefined();
    expect(result.current.updateTodo).toBeDefined();
    expect(result.current.toggleTodo).toBeDefined();
    expect(result.current.deleteTodo).toBeDefined();
    expect(result.current.clearCompletedTodos).toBeDefined();
  });

  it('isBusy is false initially', () => {
    const { result } = renderHook(() => useTodos(), { wrapper: makeWrapper() });
    expect(result.current.isBusy).toBe(false);
  });

  it('createTodo calls todosApi.create with title and linkUrl', async () => {
    vi.mocked(todosApi.create).mockResolvedValue({
      id: '1', title: 'New', completed: false, createdAt: '', isSynced: false,
    });

    const { result } = renderHook(() => useTodos(), { wrapper: makeWrapper() });

    await act(async () => {
      await result.current.createTodo.mutateAsync({ title: 'New todo', linkUrl: 'https://x.com' });
    });

    expect(todosApi.create).toHaveBeenCalledWith('New todo', 'https://x.com');
  });

  it('toggleTodo calls todosApi.setCompleted with id and completed flag', async () => {
    vi.mocked(todosApi.setCompleted).mockResolvedValue({
      id: '1', title: 'Task', completed: true, createdAt: '', isSynced: false,
    });

    const { result } = renderHook(() => useTodos(), { wrapper: makeWrapper() });

    await act(async () => {
      await result.current.toggleTodo.mutateAsync({ id: '1', completed: true });
    });

    expect(todosApi.setCompleted).toHaveBeenCalledWith('1', true);
  });

  it('deleteTodo calls todosApi.delete with id', async () => {
    vi.mocked(todosApi.delete).mockResolvedValue(undefined);

    const { result } = renderHook(() => useTodos(), { wrapper: makeWrapper() });

    await act(async () => {
      await result.current.deleteTodo.mutateAsync('todo-id');
    });

    expect(todosApi.delete).toHaveBeenCalledWith('todo-id');
  });

  it('clearCompletedTodos calls todosApi.clearCompleted', async () => {
    vi.mocked(todosApi.clearCompleted).mockResolvedValue({ removed: 2 });

    const { result } = renderHook(() => useTodos(), { wrapper: makeWrapper() });

    await act(async () => {
      await result.current.clearCompletedTodos.mutateAsync();
    });

    expect(todosApi.clearCompleted).toHaveBeenCalled();
  });
});
