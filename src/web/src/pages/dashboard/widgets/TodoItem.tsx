import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import {
  faArrowUpRightFromSquare,
  faCheck,
  faPen,
  faTrashCan,
  faXmark,
} from "@fortawesome/free-solid-svg-icons";
import type { TodoItem as TodoItemType } from "../../../types";

interface TodoItemProps {
  todo: TodoItemType;
  editingId: string | null;
  editingTitle: string;
  isBusy: boolean;
  onStartEdit: (id: string, currentValue: string) => void;
  onCancelEdit: () => void;
  onEditingTitleChange: (value: string) => void;
  onSaveEdit: (id: string) => void;
  onToggleCompleted: (id: string, completed: boolean) => void;
  onDelete: (id: string) => void;
  onOpenLink: (linkUrl: string) => void;
}

export function TodoItem({
  todo,
  editingId,
  editingTitle,
  isBusy,
  onStartEdit,
  onCancelEdit,
  onEditingTitleChange,
  onSaveEdit,
  onToggleCompleted,
  onDelete,
  onOpenLink,
}: TodoItemProps) {
  const isEditing = editingId === todo.id;

  return (
    <div className="todo-item">
      <label className="todo-check">
        <input
          type="checkbox"
          checked={todo.completed}
          onChange={() => onToggleCompleted(todo.id, true)}
          disabled={isBusy}
        />
      </label>
      <div className="todo-copy">
        {isEditing ? (
          <input
            className="todo-input todo-input--inline"
            value={editingTitle}
            onChange={(e) => onEditingTitleChange(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === "Enter") {
                e.preventDefault();
                onSaveEdit(todo.id);
              }
              if (e.key === "Escape") {
                onCancelEdit();
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
              onClick={() => onSaveEdit(todo.id)}
              disabled={isBusy}
              title="Save todo"
              type="button"
            >
              <FontAwesomeIcon icon={faCheck} className="todo-action-icon" />
            </button>
            <button
              className="todo-icon-btn"
              onClick={onCancelEdit}
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
              onClick={() =>
                onStartEdit(
                  todo.id,
                  todo.linkUrl ? `${todo.title} ${todo.linkUrl}` : todo.title,
                )
              }
              disabled={isBusy}
              title="Edit todo"
              type="button"
            >
              <FontAwesomeIcon icon={faPen} className="todo-action-icon" />
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
          </>
        )}
      </div>
    </div>
  );
}
