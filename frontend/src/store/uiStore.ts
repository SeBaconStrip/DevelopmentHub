import { create } from 'zustand';
import type { LayoutItem } from '../types';

export type WidgetId = 'repositories' | 'pullRequests';

export type { LayoutItem };

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
  hydrate: (widgets: { id: string; enabled: boolean }[], layouts: BreakpointLayouts) => void;
}

export const useUiStore = create<UiStore>()((set) => ({
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
  hydrate: (serverWidgets, layouts) =>
    set((state) => ({
      dashboardWidgets: state.dashboardWidgets.map((w) => {
        const match = serverWidgets.find((sw) => sw.id === w.id);
        return match !== undefined ? { ...w, enabled: match.enabled } : w;
      }),
      gridLayouts: Object.keys(layouts).length > 0 ? layouts : DEFAULT_LAYOUTS,
    })),
}));
