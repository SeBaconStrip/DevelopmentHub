import * as signalR from "@microsoft/signalr";
import { useEffect, useState } from "react";

export function useRepositoryHub(onRepositoriesUpdated: () => void) {
  const [isScanning, setIsScanning] = useState(false);

  useEffect(() => {
    const connection = new signalR.HubConnectionBuilder()
      .withUrl("/hubs/log", { accessTokenFactory: () => window.__devHubToken ?? '' })
      .withAutomaticReconnect()
      .build();
    connection.on("ScanStarted", () => {
      setIsScanning(true);
    });
    connection.on("RepositoriesUpdated", () => {
      setIsScanning(false);
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

  return { isScanning };
}
