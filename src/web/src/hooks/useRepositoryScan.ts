import { useMutation, useQueryClient } from "@tanstack/react-query";
import { repositoriesApi } from "../api/repositories";

export function useRepositoryScan() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: repositoriesApi.scan,
    onSuccess: (data) => queryClient.setQueryData(["repositories"], data),
  });
}
