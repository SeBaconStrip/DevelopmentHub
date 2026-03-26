import * as signalR from "@microsoft/signalr";
import { useEffect } from "react";

export function useRepositoryHub(onRepositoriesUpdated: () => void) {
  useEffect(() => {
    const connection = new signalR.HubConnectionBuilder()
      .withUrl("/hubs/log")
      .withAutomaticReconnect()
      .build();
    connection.on("RepositoriesUpdated", () => {
      onRepositoriesUpdated();
    });
    connection
      .start()
      .catch((err) =>
        console.warn("SignalR (repo updates) connection failed:", err),
      );
    return () => {
      connection.stop();
    };
  }, [onRepositoriesUpdated]);
}
