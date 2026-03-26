import { useMutation, useQueryClient } from "@tanstack/react-query";
import { todosApi } from "../api/todos";

export function useTodos() {
  const queryClient = useQueryClient();
  const invalidate = () => queryClient.invalidateQueries({ queryKey: ["todos"] });

  const createTodo = useMutation({
    mutationFn: ({ title, linkUrl }: { title: string; linkUrl?: string }) =>
      todosApi.create(title, linkUrl),
    onSuccess: invalidate,
  });

  const updateTodo = useMutation({
    mutationFn: ({ id, title, linkUrl }: { id: string; title: string; linkUrl?: string }) =>
      todosApi.update(id, title, linkUrl),
    onSuccess: invalidate,
  });

  const toggleTodo = useMutation({
    mutationFn: ({ id, completed }: { id: string; completed: boolean }) =>
      todosApi.setCompleted(id, completed),
    onSuccess: invalidate,
  });

  const deleteTodo = useMutation({
    mutationFn: (id: string) => todosApi.delete(id),
    onSuccess: invalidate,
  });

  const clearCompletedTodos = useMutation({
    mutationFn: todosApi.clearCompleted,
    onSuccess: invalidate,
  });

  const isBusy =
    createTodo.isPending ||
    updateTodo.isPending ||
    toggleTodo.isPending ||
    deleteTodo.isPending ||
    clearCompletedTodos.isPending;

  return { createTodo, updateTodo, toggleTodo, deleteTodo, clearCompletedTodos, isBusy };
}
