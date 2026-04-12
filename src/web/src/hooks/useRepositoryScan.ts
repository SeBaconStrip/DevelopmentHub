import { useMutation, useQueryClient } from "@tanstack/react-query";
import { repositoriesApi } from "../api/repositories";

export function useRepositoryScan() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: repositoriesApi.scan,
    onSuccess: () => {
      // Scan accepted — actual results will arrive via SignalR RepositoriesUpdated.
      // Invalidate so React Query refetches once the event fires.
      queryClient.invalidateQueries({ queryKey: ["repositories"] });
    },
  });
}
