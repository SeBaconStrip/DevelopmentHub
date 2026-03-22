import { useState } from "react";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import {
  faArrowUpRightFromSquare,
  faCheck,
  faPen,
  faTrashArrowUp,
  faTrashCan,
  faXmark,
} from "@fortawesome/free-solid-svg-icons";
import { launcherApi } from "../../../api/launcher";
import type { TodoItem } from "../../../types";
import { Empty } from "./shared";
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
  const [newTitle, setNewTitle] = useState("");
  const [editingId, setEditingId] = useState<string | null>(null);
  const [editingTitle, setEditingTitle] = useState("");
  const [showCompleted, setShowCompleted] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const activeTodos = todos.filter((todo) => !todo.completed);
  const completedTodos = todos.filter((todo) => todo.completed);

  async function handleCreate(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const title = newTitle.trim();
    if (!title) {
      return;
    }

    try {
      setError(null);
      await onCreate(title);
      setNewTitle("");
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

  async function handleAction(action: () => Promise<unknown>, fallback: string) {
    try {
      setError(null);
      await action();
    } catch (err) {
      setError(err instanceof Error ? err.message : fallback);
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
      {error && (
        <div className="panel-error-bar">
          <span>⚠ {error}</span>
          <button onClick={() => setError(null)}>✕</button>
        </div>
      )}

      <form className="todo-create-row" onSubmit={handleCreate}>
        <input
          className="todo-input"
          value={newTitle}
          onChange={(e) => setNewTitle(e.target.value)}
          placeholder="Add a todo, optionally with a link..."
          disabled={isBusy}
        />
        <button className="btn-ghost todo-add-btn" type="submit" disabled={isBusy || !newTitle.trim()}>
          Add
        </button>
      </form>

      <div className="todo-list">
        {activeTodos.length === 0 ? (
          <Empty text="No open todos. Add one above to get started." />
        ) : (
          activeTodos.map((todo) => {
            const isEditing = editingId === todo.id;
            return (
              <div key={todo.id} className="todo-item">
                <label className="todo-check">
                  <input
                    type="checkbox"
                    checked={todo.completed}
                    onChange={() =>
                      handleAction(
                        () => onToggleCompleted(todo.id, true),
                        "Could not complete todo.",
                      )
                    }
                    disabled={isBusy}
                  />
                </label>
                <div className="todo-copy">
                  {isEditing ? (
                    <input
                      className="todo-input todo-input--inline"
                      value={editingTitle}
                      onChange={(e) => setEditingTitle(e.target.value)}
                      onKeyDown={(e) => {
                        if (e.key === "Enter") {
                          e.preventDefault();
                          void handleSaveEdit(todo.id);
                        }
                        if (e.key === "Escape") {
                          setEditingId(null);
                          setEditingTitle("");
                        }
                      }}
                      placeholder="Todo text, optionally with a link..."
                      autoFocus
                    />
                  ) : (
                    <>
                      <span className="todo-title">{todo.title}</span>
                      {todo.linkUrl && (
                        <span className="item-meta todo-link-text">{todo.linkUrl}</span>
                      )}
                    </>
                  )}
                </div>
                <div className="todo-actions">
                  {isEditing ? (
                    <>
                      <button
                        className="todo-icon-btn"
                        onClick={() => void handleSaveEdit(todo.id)}
                        disabled={isBusy}
                        title="Save todo"
                        type="button"
                      >
                        <FontAwesomeIcon icon={faCheck} className="todo-action-icon" />
                      </button>
                      <button
                        className="todo-icon-btn"
                        onClick={() => {
                          setEditingId(null);
                          setEditingTitle("");
                        }}
                        disabled={isBusy}
                        title="Cancel editing"
                        type="button"
                      >
                        <FontAwesomeIcon icon={faXmark} className="todo-action-icon" />
                      </button>
                    </>
                  ) : (
                    <>
                      {todo.linkUrl && (
                        <button
                          className="todo-icon-btn"
                          onClick={() => void handleOpenLink(todo.linkUrl!)}
                          disabled={isBusy}
                          title="Open todo link"
                          type="button"
                        >
                          <FontAwesomeIcon icon={faArrowUpRightFromSquare} className="todo-action-icon" />
                        </button>
                      )}
                      <button
                        className="todo-icon-btn"
                        onClick={() => {
                          setEditingId(todo.id);
                          setEditingTitle(
                            todo.linkUrl ? `${todo.title} ${todo.linkUrl}` : todo.title,
                          );
                        }}
                        disabled={isBusy}
                        title="Edit todo"
                        type="button"
                      >
                        <FontAwesomeIcon icon={faPen} className="todo-action-icon" />
                      </button>
                      <button
                        className="todo-icon-btn todo-icon-btn--danger"
                        onClick={() =>
                          void handleAction(
                            () => onDelete(todo.id),
                            "Could not delete todo.",
                          )
                        }
                        disabled={isBusy}
                        title="Delete todo"
                        type="button"
                      >
                        <FontAwesomeIcon icon={faTrashCan} className="todo-action-icon" />
                      </button>
                    </>
                  )}
                </div>
              </div>
            );
          })
        )}
      </div>

      <div className="todo-completed">
        <button
          className="todo-completed-toggle"
          onClick={() => setShowCompleted((value) => !value)}
          disabled={completedTodos.length === 0}
        >
          <span>{showCompleted ? "▾" : "▸"} Done</span>
          <span className="panel-badge">{completedTodos.length}</span>
        </button>

        {showCompleted && completedTodos.length > 0 && (
          <div className="todo-list todo-list--completed">
            <div className="todo-completed-actions">
              <button
                className="todo-icon-btn todo-icon-btn--danger"
                onClick={() =>
                  void handleAction(
                    onClearCompleted,
                    "Could not clear completed todos.",
                  )
                }
                disabled={isBusy}
                title="Clear completed todos"
                type="button"
              >
                <FontAwesomeIcon icon={faTrashCan} className="todo-action-icon" />
              </button>
            </div>
            {completedTodos.map((todo) => (
              <div key={todo.id} className="todo-item todo-item--completed">
                <label className="todo-check">
                  <input
                    type="checkbox"
                    checked={todo.completed}
                    onChange={() =>
                      handleAction(
                        () => onToggleCompleted(todo.id, false),
                        "Could not restore todo.",
                      )
                    }
                    disabled={isBusy}
                  />
                </label>
                <div className="todo-copy">
                  <span className="todo-title">{todo.title}</span>
                  {todo.linkUrl && (
                    <span className="item-meta todo-link-text">{todo.linkUrl}</span>
                  )}
                  {todo.completedAt && (
                    <span className="item-meta">
                      Done {new Date(todo.completedAt).toLocaleDateString()}
                    </span>
                  )}
                </div>
                <div className="todo-actions">
                  {todo.linkUrl && (
                    <button
                      className="todo-icon-btn"
                      onClick={() => void handleOpenLink(todo.linkUrl!)}
                      disabled={isBusy}
                      title="Open todo link"
                      type="button"
                    >
                      <FontAwesomeIcon icon={faArrowUpRightFromSquare} className="todo-action-icon" />
                    </button>
                  )}
                  <button
                    className="todo-icon-btn"
                    onClick={() =>
                      void handleAction(
                        () => onToggleCompleted(todo.id, false),
                        "Could not restore todo.",
                      )
                    }
                    disabled={isBusy}
                    title="Restore todo"
                    type="button"
                  >
                    <FontAwesomeIcon icon={faTrashArrowUp} className="todo-action-icon" />
                  </button>
                  <button
                    className="todo-icon-btn todo-icon-btn--danger"
                    onClick={() =>
                      void handleAction(
                        () => onDelete(todo.id),
                        "Could not delete todo.",
                      )
                    }
                    disabled={isBusy}
                    title="Delete todo"
                    type="button"
                  >
                    <FontAwesomeIcon icon={faTrashCan} className="todo-action-icon" />
                  </button>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
