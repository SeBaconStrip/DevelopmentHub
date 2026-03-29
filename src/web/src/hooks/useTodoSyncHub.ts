import * as signalR from "@microsoft/signalr";
import { useEffect } from "react";

export function useTodoSyncHub(onTodosUpdated: () => void) {
  useEffect(() => {
    const connection = new signalR.HubConnectionBuilder()
      .withUrl("/hubs/log")
      .withAutomaticReconnect()
      .build();
    connection.on("TodosUpdated", () => {
      onTodosUpdated();
    });
    connection
      .start()
      .catch((err) =>
        console.warn("SignalR (todo sync updates) connection failed:", err),
      );
    return () => {
      connection.stop();
    };
  }, [onTodosUpdated]);
}
