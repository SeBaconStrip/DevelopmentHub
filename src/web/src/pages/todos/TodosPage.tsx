import { useState, useCallback } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { todosApi } from "../../api/todos";
import { configApi } from "../../api/config";
import { useTodos } from "../../hooks/useTodos";
import { useTodoSyncHub } from "../../hooks/useTodoSyncHub";
import { TodosWidget } from "../dashboard/widgets/TodosWidget";
import { FilterToolbar } from "../../components/FilterToolbar";
import type { TodoItem } from "../../types";
import "./TodosPage.css";

type Filter = "all" | "active" | "completed";

export default function TodosPage() {
  const [search, setSearch] = useState("");
  const [filter, setFilter] = useState<Filter>("all");
  const queryClient = useQueryClient();

  const handleTodosUpdated = useCallback(() => {
    queryClient.invalidateQueries({ queryKey: ["todos"] });
  }, [queryClient]);
  useTodoSyncHub(handleTodosUpdated);

  const { data: config } = useQuery({ queryKey: ["config"], queryFn: configApi.get });

  const { data: todos = [], isLoading } = useQuery<TodoItem[]>({
    queryKey: ["todos"],
    queryFn: todosApi.getAll,
    refetchInterval: (config?.todoSyncIntervalSeconds ?? 300) * 1000,
  });

  const { createTodo, updateTodo, toggleTodo, deleteTodo, clearCompletedTodos, isBusy } = useTodos();

  const activeCount = todos.filter((t) => !t.completed).length;
  const completedCount = todos.filter((t) => t.completed).length;

  const filtered = todos
    .filter((t) => {
      if (filter === "active") return !t.completed;
      if (filter === "completed") return t.completed;
      return true;
    })
    .filter((t) => !search || t.title.toLowerCase().includes(search.toLowerCase()));

  const todoFilters = [
    { value: "all", label: `All (${todos.length})` },
    { value: "active", label: `Active${activeCount > 0 ? ` (${activeCount})` : ""}` },
    { value: "completed", label: `Completed${completedCount > 0 ? ` (${completedCount})` : ""}` },
  ];

  return (
    <div className="todos-page">
      <div className="todos-page-card">
        <FilterToolbar
          search={search}
          onSearchChange={setSearch}
          filter={filter}
          onFilterChange={(f) => setFilter(f as Filter)}
          filters={todoFilters}
          searchPlaceholder="Search todos…"
          shownCount={filtered.length}
          totalCount={todos.length}
        />
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
