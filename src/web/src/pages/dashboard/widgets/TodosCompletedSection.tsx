import { useState } from "react";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import {
  faArrowUpRightFromSquare,
  faTrashArrowUp,
  faTrashCan,
} from "@fortawesome/free-solid-svg-icons";
import type { TodoItem } from "../../../types";

interface TodosCompletedSectionProps {
  completedTodos: TodoItem[];
  isBusy: boolean;
  onClearCompleted: () => void;
  onToggleCompleted: (id: string, completed: boolean) => void;
  onDelete: (id: string) => void;
  onOpenLink: (linkUrl: string) => void;
}

export function TodosCompletedSection({
  completedTodos,
  isBusy,
  onClearCompleted,
  onToggleCompleted,
  onDelete,
  onOpenLink,
}: TodosCompletedSectionProps) {
  const [showCompleted, setShowCompleted] = useState(false);

  return (
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
              onClick={onClearCompleted}
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
                  onChange={() => onToggleCompleted(todo.id, false)}
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
                    onClick={() => onOpenLink(todo.linkUrl!)}
                    disabled={isBusy}
                    title="Open todo link"
                    type="button"
                  >
                    <FontAwesomeIcon icon={faArrowUpRightFromSquare} className="todo-action-icon" />
                  </button>
                )}
                <button
                  className="todo-icon-btn"
                  onClick={() => onToggleCompleted(todo.id, false)}
                  disabled={isBusy}
                  title="Restore todo"
                  type="button"
                >
                  <FontAwesomeIcon icon={faTrashArrowUp} className="todo-action-icon" />
                </button>
                <button
                  className="todo-icon-btn todo-icon-btn--danger"
                  onClick={() => onDelete(todo.id)}
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
  );
}
