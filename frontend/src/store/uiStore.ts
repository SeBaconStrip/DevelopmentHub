import { create } from 'zustand';

interface UiState {
  selectedExecutionId: number | null;
  setSelectedExecution: (id: number | null) => void;
  sidebarOpen: boolean;
  toggleSidebar: () => void;
}

export const useUiStore = create<UiState>(set => ({
  selectedExecutionId: null,
  setSelectedExecution: (id) => set({ selectedExecutionId: id }),
  sidebarOpen: true,
  toggleSidebar: () => set(s => ({ sidebarOpen: !s.sidebarOpen })),
}));
