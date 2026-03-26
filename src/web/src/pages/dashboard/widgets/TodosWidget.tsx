import { useState } from "react";
import { launcherApi } from "../../../api/launcher";
import type { TodoItem } from "../../../types";
import { Empty } from "./shared";
import { ErrorBar } from "../../../components/ErrorBar";
import { TodoCreateForm } from "./TodoCreateForm";
import { TodoItem as TodoItemComponent } from "./TodoItem";
import { TodosCompletedSection } from "./TodosCompletedSection";
import "./TodosWidget.css";

export function TodosWidget({
  todos,
  onCreate,
  onUpdate,
  onToggleCompleted,
  onDelete,
  onClearCompleted,
  isBusy,
}: {
  todos: TodoItem[];
  onCreate: (title: string, linkUrl?: string) => Promise<unknown>;
  onUpdate: (id: string, title: string, linkUrl?: string) => Promise<unknown>;
  onToggleCompleted: (id: string, completed: boolean) => Promise<unknown>;
  onDelete: (id: string) => Promise<unknown>;
  onClearCompleted: () => Promise<unknown>;
  isBusy: boolean;
}) {
  const [editingId, setEditingId] = useState<string | null>(null);
  const [editingTitle, setEditingTitle] = useState("");
  const [error, setError] = useState<string | null>(null);

  const activeTodos = todos.filter((todo) => !todo.completed);
  const completedTodos = todos.filter((todo) => todo.completed);

  async function handleAction(action: () => Promise<unknown>, fallback: string) {
    try {
      setError(null);
      await action();
    } catch (err) {
      setError(err instanceof Error ? err.message : fallback);
    }
  }

  async function handleCreate(title: string) {
    try {
      setError(null);
      await onCreate(title);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not create todo.");
    }
  }

  async function handleSaveEdit(id: string) {
    const title = editingTitle.trim();
    if (!title) {
      setError("Todo title is required.");
      return;
    }
    try {
      setError(null);
      await onUpdate(id, title);
      setEditingId(null);
      setEditingTitle("");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not update todo.");
    }
  }

  async function handleOpenLink(linkUrl: string) {
    try {
      setError(null);
      await launcherApi.openUrl(linkUrl);
    } catch {
      setError("Could not open todo link.");
    }
  }

  return (
    <div className="todo-widget">
      <ErrorBar message={error} onDismiss={() => setError(null)} />

      <TodoCreateForm onSubmit={handleCreate} isBusy={isBusy} />

      <div className="todo-list">
        {activeTodos.length === 0 ? (
          <Empty text="No open todos. Add one above to get started." />
        ) : (
          activeTodos.map((todo) => (
            <TodoItemComponent
              key={todo.id}
              todo={todo}
              editingId={editingId}
              editingTitle={editingTitle}
              isBusy={isBusy}
              onStartEdit={(id, value) => { setEditingId(id); setEditingTitle(value); }}
              onCancelEdit={() => { setEditingId(null); setEditingTitle(""); }}
              onEditingTitleChange={setEditingTitle}
              onSaveEdit={(id) => void handleSaveEdit(id)}
              onToggleCompleted={(id, completed) =>
                void handleAction(
                  () => onToggleCompleted(id, completed),
                  "Could not complete todo.",
                )
              }
              onDelete={(id) =>
                void handleAction(
                  () => onDelete(id),
                  "Could not delete todo.",
                )
              }
              onOpenLink={(linkUrl) => void handleOpenLink(linkUrl)}
            />
          ))
        )}
      </div>

      <TodosCompletedSection
        completedTodos={completedTodos}
        isBusy={isBusy}
        onClearCompleted={() =>
          void handleAction(onClearCompleted, "Could not clear completed todos.")
        }
        onToggleCompleted={(id, completed) =>
          void handleAction(
            () => onToggleCompleted(id, completed),
            "Could not restore todo.",
          )
        }
        onDelete={(id) =>
          void handleAction(() => onDelete(id), "Could not delete todo.")
        }
        onOpenLink={(linkUrl) => void handleOpenLink(linkUrl)}
      />
    </div>
  );
}
