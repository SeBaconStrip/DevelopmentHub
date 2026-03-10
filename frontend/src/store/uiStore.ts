import { create } from 'zustand';
import type { LayoutItem } from '../types';

export type WidgetId = 'repositories' | 'pullRequests';
export type ThemeId = 'violet' | 'dark' | 'ocean' | 'orange' | 'nature';

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
  { id: 'repositories', label: 'Repositories',     icon: '\uD83D\uDCC1', enabled: true },
  { id: 'pullRequests', label: 'Pull Requests',     icon: '\uD83D\uDD00', enabled: true },
];

const THEME_IDS: ThemeId[] = ['violet', 'dark', 'ocean', 'orange', 'nature'];
const LAYOUTS_KEY = 'dh-layouts';
const WIDGETS_KEY = 'dh-widgets';

function loadTheme(): ThemeId {
  const stored = localStorage.getItem('dh-theme');
  return THEME_IDS.includes(stored as ThemeId) ? (stored as ThemeId) : 'violet';
}

function loadLayouts(): BreakpointLayouts {
  try {
    const stored = localStorage.getItem(LAYOUTS_KEY);
    if (stored) {
      const parsed = JSON.parse(stored) as BreakpointLayouts;
      if (Object.keys(parsed).length > 0) return parsed;
    }
  } catch { /* fall through to default */ }
  return DEFAULT_LAYOUTS;
}

function loadWidgets(): DashboardWidget[] {
  try {
    const stored = localStorage.getItem(WIDGETS_KEY);
    if (stored) {
      const enabled = JSON.parse(stored) as Record<string, boolean>;
      return defaultWidgets.map((w) => ({
        ...w,
        enabled: w.id in enabled ? enabled[w.id] : w.enabled,
      }));
    }
  } catch { /* fall through to default */ }
  return defaultWidgets;
}

interface UiStore {
  dashboardWidgets: DashboardWidget[];
  toggleWidget: (id: WidgetId) => void;
  gridLayouts: BreakpointLayouts;
  setGridLayouts: (layouts: BreakpointLayouts) => void;
  resetGridLayouts: () => void;
  theme: ThemeId;
  setTheme: (theme: ThemeId) => void;
}

export const useUiStore = create<UiStore>()((set) => ({
  dashboardWidgets: loadWidgets(),
  toggleWidget: (id) =>
    set((state) => {
      const updated = state.dashboardWidgets.map((w) =>
        w.id === id ? { ...w, enabled: !w.enabled } : w
      );
      const enabledMap = Object.fromEntries(updated.map((w) => [w.id, w.enabled]));
      localStorage.setItem(WIDGETS_KEY, JSON.stringify(enabledMap));
      return { dashboardWidgets: updated };
    }),
  gridLayouts: loadLayouts(),
  setGridLayouts: (layouts) => {
    localStorage.setItem(LAYOUTS_KEY, JSON.stringify(layouts));
    set({ gridLayouts: layouts });
  },
  resetGridLayouts: () => {
    localStorage.setItem(LAYOUTS_KEY, JSON.stringify(DEFAULT_LAYOUTS));
    set({ gridLayouts: DEFAULT_LAYOUTS });
  },
  theme: loadTheme(),
  setTheme: (theme) => {
    localStorage.setItem('dh-theme', theme);
    document.documentElement.setAttribute('data-theme', theme);
    set({ theme });
  },
}));
