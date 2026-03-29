const BASE = "/api/todo-sync";

export interface MicrosoftTodoList {
  id: string;
  displayName: string;
}

export interface StartAuthResponse {
  sessionId: string;
  userCode: string;
  verificationUri: string;
  expiresIn: number;
}

export interface PollAuthResponse {
  status: "pending" | "succeeded" | "failed" | "expired";
  error?: string;
}

export const todoSyncApi = {
  async startMicrosoftAuth(clientId: string): Promise<StartAuthResponse> {
    const res = await fetch(`${BASE}/microsoft-todo/auth/start`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ clientId }),
    });
    if (!res.ok) {
      const body = await res.json().catch(() => ({}));
      throw new Error(body.error ?? "Failed to start authentication.");
    }
    return res.json();
  },

  async pollMicrosoftAuth(sessionId: string): Promise<PollAuthResponse> {
    const res = await fetch(`${BASE}/microsoft-todo/auth/poll/${sessionId}`);
    if (!res.ok) throw new Error("Poll request failed.");
    return res.json();
  },

  async disconnectMicrosoft(): Promise<void> {
    await fetch(`${BASE}/microsoft-todo/auth`, { method: "DELETE" });
  },

  async getMicrosoftLists(): Promise<MicrosoftTodoList[]> {
    const res = await fetch(`${BASE}/microsoft-todo/lists`);
    if (!res.ok) {
      const body = await res.json().catch(() => ({}));
      throw new Error(body.error ?? "Failed to fetch lists.");
    }
    return res.json();
  },

  async triggerSync(): Promise<void> {
    console.log("[TodoSync] Triggering manual sync");
    const res = await fetch(`${BASE}/sync`, { method: "POST" });
    if (!res.ok) throw new Error("Sync failed.");
    console.log("[TodoSync] Manual sync completed");
  },
};
