import type { TodoItem } from "../types";

async function handleResponse<T>(res: Response): Promise<T> {
  if (!res.ok) {
    const body = await res.text().catch(() => res.statusText);
    throw new Error(body || `HTTP ${res.status}`);
  }

  if (res.status === 204) {
    return undefined as T;
  }

  return res.json() as Promise<T>;
}

export const todosApi = {
  getAll: (): Promise<TodoItem[]> =>
    fetch("/api/todos").then((r) => handleResponse(r)),

  create: (title: string, linkUrl?: string): Promise<TodoItem> =>
    fetch("/api/todos", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ title, linkUrl }),
    }).then((r) => handleResponse(r)),

  update: (id: string, title: string, linkUrl?: string): Promise<TodoItem> =>
    fetch(`/api/todos/${id}`, {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ title, linkUrl }),
    }).then((r) => handleResponse(r)),

  setCompleted: (id: string, completed: boolean): Promise<TodoItem> =>
    fetch(`/api/todos/${id}/complete?completed=${completed}`, {
      method: "PATCH",
    }).then((r) => handleResponse(r)),

  delete: (id: string): Promise<void> =>
    fetch(`/api/todos/${id}`, {
      method: "DELETE",
    }).then((r) => handleResponse(r)),

  clearCompleted: (): Promise<{ removed: number }> =>
    fetch("/api/todos/completed", {
      method: "DELETE",
    }).then((r) => handleResponse(r)),
};
