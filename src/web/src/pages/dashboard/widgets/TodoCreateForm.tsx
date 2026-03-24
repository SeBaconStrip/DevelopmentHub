import { useState } from "react";

interface TodoCreateFormProps {
  onSubmit: (title: string) => Promise<void>;
  isBusy: boolean;
}

export function TodoCreateForm({ onSubmit, isBusy }: TodoCreateFormProps) {
  const [newTitle, setNewTitle] = useState("");

  async function handleCreate(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const title = newTitle.trim();
    if (!title) return;
    await onSubmit(title);
    setNewTitle("");
  }

  return (
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
  );
}
