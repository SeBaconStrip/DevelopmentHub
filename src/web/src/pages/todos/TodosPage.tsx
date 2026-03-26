import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { todosApi } from "../../api/todos";
import { TodosWidget } from "../dashboard/widgets/TodosWidget";
import type { TodoItem } from "../../types";
import "./TodosPage.css";

type Filter = "all" | "active" | "completed";

export default function TodosPage() {
  const queryClient = useQueryClient();
  const [search, setSearch] = useState("");
  const [filter, setFilter] = useState<Filter>("all");

  const { data: todos = [], isLoading } = useQuery<TodoItem[]>({
    queryKey: ["todos"],
    queryFn: todosApi.getAll,
  });

  const createTodo = useMutation({
    mutationFn: ({ title, linkUrl }: { title: string; linkUrl?: string }) =>
      todosApi.create(title, linkUrl),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["todos"] }),
  });
  const updateTodo = useMutation({
    mutationFn: ({ id, title, linkUrl }: { id: string; title: string; linkUrl?: string }) =>
      todosApi.update(id, title, linkUrl),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["todos"] }),
  });
  const toggleTodo = useMutation({
    mutationFn: ({ id, completed }: { id: string; completed: boolean }) =>
      todosApi.setCompleted(id, completed),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["todos"] }),
  });
  const deleteTodo = useMutation({
    mutationFn: (id: string) => todosApi.delete(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["todos"] }),
  });
  const clearCompletedTodos = useMutation({
    mutationFn: todosApi.clearCompleted,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["todos"] }),
  });

  const isBusy =
    createTodo.isPending ||
    updateTodo.isPending ||
    toggleTodo.isPending ||
    deleteTodo.isPending ||
    clearCompletedTodos.isPending;

  const activeCount = todos.filter((t) => !t.completed).length;
  const completedCount = todos.filter((t) => t.completed).length;

  const filtered = todos
    .filter((t) => {
      if (filter === "active") return !t.completed;
      if (filter === "completed") return t.completed;
      return true;
    })
    .filter((t) => !search || t.title.toLowerCase().includes(search.toLowerCase()));

  return (
    <div className="todos-page">
      <div className="todos-page-card">
        <div className="todos-page-toolbar">
          <div className="todos-page-toolbar-left">
            <input
              className="todos-page-search"
              type="search"
              placeholder="Search todos…"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
            <div className="todos-page-filters">
              {(["all", "active", "completed"] as Filter[]).map((f) => (
                <button
                  key={f}
                  className={`todos-page-filter-btn${filter === f ? " todos-page-filter-btn--active" : ""}`}
                  onClick={() => setFilter(f)}
                >
                  {f === "all" && `All (${todos.length})`}
                  {f === "active" && `Active${activeCount > 0 ? ` (${activeCount})` : ""}`}
                  {f === "completed" && `Completed${completedCount > 0 ? ` (${completedCount})` : ""}`}
                </button>
              ))}
            </div>
          </div>
          {filtered.length !== todos.length && (
            <span className="todos-page-count">{filtered.length} shown</span>
          )}
        </div>
      </div>

      {isLoading ? (
        <p className="todos-page-empty">Loading…</p>
      ) : (
        <div className="todos-page-content-card">
          <TodosWidget
            todos={filtered}
            onCreate={(title, linkUrl) => createTodo.mutateAsync({ title, linkUrl })}
            onUpdate={(id, title, linkUrl) => updateTodo.mutateAsync({ id, title, linkUrl })}
            onToggleCompleted={(id, completed) => toggleTodo.mutateAsync({ id, completed })}
            onDelete={(id) => deleteTodo.mutateAsync(id)}
            onClearCompleted={() => clearCompletedTodos.mutateAsync()}
            isBusy={isBusy}
          />
        </div>
      )}
    </div>
  );
}
