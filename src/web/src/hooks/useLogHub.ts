import { useEffect, useRef } from 'react';
import * as signalR from '@microsoft/signalr';

export function useLogHub(
  executionId: string | null,
  onLogLine: (data: { text: string; stream: string; timestamp: string }) => void,
  onCompleted: (data: { executionId: string; exitCode: number; status: string }) => void,
) {
  const connectionRef = useRef<signalR.HubConnection | null>(null);

  useEffect(() => {
    if (executionId === null) return;

    const connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/log', { accessTokenFactory: () => window.__devHubToken ?? '' })
      .withAutomaticReconnect()
      .build();

    connectionRef.current = connection;

    connection.on('LogLine', onLogLine);
    connection.on('ExecutionCompleted', onCompleted);

    connection
      .start()
      .then(() => connection.invoke('JoinExecution', executionId))
      .catch(err => console.error('SignalR connection error:', err));

    return () => {
      connection.invoke('LeaveExecution', executionId).catch(() => {});
      connection.stop();
    };
  }, [executionId]); // eslint-disable-line react-hooks/exhaustive-deps
}
