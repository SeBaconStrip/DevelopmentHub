import type { TodoItem } from "../types";
import { apiFetch } from './client';

async function handleResponse<T>(res: Response): Promise<T> {
  if (!res.ok) {
    const text = await res.text().catch(() => "");
    let message = `HTTP ${res.status}`;
    try {
      const json = JSON.parse(text);
      message = json.detail ?? json.title ?? json.error ?? message;
    } catch {
      if (text) message = text;
    }
    if (res.status === 404) message = "This todo no longer exists.";
    throw new Error(message);
  }

  if (res.status === 204) {
    return undefined as T;
  }

  return res.json() as Promise<T>;
}

export const todosApi = {
  getAll: (): Promise<TodoItem[]> =>
    apiFetch("/api/todos").then((r) => handleResponse(r)),

  create: (title: string, linkUrl?: string): Promise<TodoItem> =>
    apiFetch("/api/todos", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ title, linkUrl }),
    }).then((r) => handleResponse(r)),

  update: (id: string, title: string, linkUrl?: string): Promise<TodoItem> =>
    apiFetch(`/api/todos/${id}`, {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ title, linkUrl }),
    }).then((r) => handleResponse(r)),

  setCompleted: (id: string, completed: boolean): Promise<TodoItem> =>
    apiFetch(`/api/todos/${id}/complete?completed=${completed}`, {
      method: "PATCH",
    }).then((r) => handleResponse(r)),

  delete: (id: string): Promise<void> =>
    apiFetch(`/api/todos/${id}`, {
      method: "DELETE",
    }).then((r) => handleResponse(r)),

  clearCompleted: (): Promise<{ removed: number }> =>
    apiFetch("/api/todos/completed", {
      method: "DELETE",
    }).then((r) => handleResponse(r)),
};
