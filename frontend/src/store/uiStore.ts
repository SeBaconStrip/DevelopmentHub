import { create } from 'zustand';
import { persist } from 'zustand/middleware';

export type WidgetId = 'repositories' | 'pullRequests';

export interface LayoutItem {
  i: string;
  x: number;
  y: number;
  w: number;
  h: number;
  minW?: number;
  minH?: number;
}

export type BreakpointLayouts = Record<string, LayoutItem[]>;

export const DEFAULT_LAYOUTS: BreakpointLayouts = {
  lg: [
    { i: 'repositories', x: 0, y: 0,  w: 8, h: 10, minW: 3, minH: 4 },
    { i: 'pullRequests',  x: 8, y: 0,  w: 4, h: 10, minW: 3, minH: 4 },
  ],
  md: [
    { i: 'repositories', x: 0, y: 0,  w: 6, h: 10, minW: 3, minH: 4 },
    { i: 'pullRequests',  x: 6, y: 0,  w: 4, h: 10, minW: 3, minH: 4 },
  ],
  sm: [
    { i: 'repositories', x: 0, y: 0,  w: 6, h: 8,  minW: 3, minH: 4 },
    { i: 'pullRequests',  x: 0, y: 8,  w: 6, h: 8,  minW: 3, minH: 4 },
  ],
};

export interface DashboardWidget {
  id: WidgetId;
  label: string;
  icon: string;
  enabled: boolean;
}

const defaultWidgets: DashboardWidget[] = [
  { id: 'repositories', label: 'Repositories',     icon: '📁', enabled: true },
  { id: 'pullRequests', label: 'Pull Requests',     icon: '🔀', enabled: true },
];

interface UiStore {
  dashboardWidgets: DashboardWidget[];
  toggleWidget: (id: WidgetId) => void;
  gridLayouts: BreakpointLayouts;
  setGridLayouts: (layouts: BreakpointLayouts) => void;
  resetGridLayouts: () => void;
}

export const useUiStore = create<UiStore>()(
  persist(
    (set) => ({
      dashboardWidgets: defaultWidgets,
      toggleWidget: (id) =>
        set((state) => ({
          dashboardWidgets: state.dashboardWidgets.map((w) =>
            w.id === id ? { ...w, enabled: !w.enabled } : w
          ),
        })),
      gridLayouts: DEFAULT_LAYOUTS,
      setGridLayouts: (layouts) => set({ gridLayouts: layouts }),
      resetGridLayouts: () => set({ gridLayouts: DEFAULT_LAYOUTS }),
    }),
    { name: 'devhub-ui' }
  )
);
