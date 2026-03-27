import { create } from 'zustand';
import type { LayoutItem } from '../types';

export type WidgetId = string;
export const BUILTIN_WIDGET_IDS = ['repositories', 'pullRequests', 'quickLinks', 'todos', 'workflows'] as const;
export type BuiltinWidgetId = typeof BUILTIN_WIDGET_IDS[number];
export type ThemeId = 'violet' | 'dark' | 'vscode' | 'ocean' | 'orange' | 'nature';

export type { LayoutItem };

export type BreakpointLayouts = Record<string, LayoutItem[]>;

export const DEFAULT_LAYOUTS: BreakpointLayouts = {
  lg: [
    { i: 'repositories', x: 0, y: 0,  w: 8, h: 10 },
    { i: 'pullRequests',  x: 8, y: 0,  w: 4, h: 10 },
    { i: 'quickLinks',   x: 8, y: 10, w: 4, h: 6  },
    { i: 'todos',        x: 0, y: 10, w: 4, h: 6  },
    { i: 'workflows',    x: 4, y: 10, w: 4, h: 6  },
  ],
  md: [
    { i: 'repositories', x: 0, y: 0,  w: 6, h: 10 },
    { i: 'pullRequests',  x: 6, y: 0,  w: 4, h: 10 },
    { i: 'quickLinks',   x: 0, y: 10, w: 5, h: 6  },
    { i: 'todos',        x: 5, y: 10, w: 5, h: 6  },
    { i: 'workflows',    x: 0, y: 16, w: 10, h: 6 },
  ],
  sm: [
    { i: 'repositories', x: 0, y: 0,  w: 6, h: 8 },
    { i: 'pullRequests',  x: 0, y: 8,  w: 6, h: 8 },
    { i: 'quickLinks',   x: 0, y: 16, w: 6, h: 6 },
    { i: 'todos',        x: 0, y: 22, w: 6, h: 6 },
    { i: 'workflows',    x: 0, y: 28, w: 6, h: 6 },
  ],
};

function normalizeLayouts(layouts: BreakpointLayouts): BreakpointLayouts {
  return Object.fromEntries(
    Object.entries(DEFAULT_LAYOUTS).map(([breakpoint, defaults]) => {
      const existing = layouts[breakpoint] ?? [];
      const merged = [...existing];

      defaults.forEach((item) => {
        if (!merged.some((existingItem) => existingItem.i === item.i)) {
          merged.push(item);
        }
      });

      return [breakpoint, merged];
    }),
  );
}

export interface DashboardWidget {
  id: WidgetId;
  label: string;
  icon: string;
  enabled: boolean;
}

const defaultWidgets: DashboardWidget[] = [
  { id: 'repositories', label: 'Repositories',     icon: '\uD83D\uDCC1', enabled: true },
  { id: 'pullRequests', label: 'Pull Requests',     icon: '\uD83D\uDD00', enabled: true },
  { id: 'quickLinks', label: 'Quick Links', icon: '\uD83D\uDD17', enabled: true },
  { id: 'todos', label: 'Todos', icon: '\u2705', enabled: true },
  { id: 'workflows', label: 'Workflows', icon: '\u2699', enabled: true },
];

const THEME_IDS: ThemeId[] = ['violet', 'dark', 'vscode', 'ocean', 'orange', 'nature'];
const LAYOUTS_KEY = 'dh-layouts';
const WIDGETS_KEY = 'dh-widgets';

let _layoutsPersistTimer: ReturnType<typeof setTimeout> | null = null;
function persistLayoutsDebounced(layouts: BreakpointLayouts) {
  if (_layoutsPersistTimer !== null) clearTimeout(_layoutsPersistTimer);
  _layoutsPersistTimer = setTimeout(() => {
    localStorage.setItem(LAYOUTS_KEY, JSON.stringify(layouts));
    _layoutsPersistTimer = null;
  }, 500);
}

function loadTheme(): ThemeId {
  const stored = localStorage.getItem('dh-theme');
  return THEME_IDS.includes(stored as ThemeId) ? (stored as ThemeId) : 'violet';
}

function loadLayouts(): BreakpointLayouts {
  try {
      const stored = localStorage.getItem(LAYOUTS_KEY);
      if (stored) {
        const parsed = JSON.parse(stored) as BreakpointLayouts;
        if (Object.keys(parsed).length > 0) return normalizeLayouts(parsed);
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
  addPluginWidget: (widget: DashboardWidget & { defaultLayout?: { w: number; h: number } }) => void;
  gridLayouts: BreakpointLayouts;
  setGridLayouts: (layouts: BreakpointLayouts) => void;
  resetGridLayouts: () => void;
  theme: ThemeId;
  setTheme: (theme: ThemeId) => void;
}

export const useUiStore = create<UiStore>()((set, get) => ({
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
  addPluginWidget: ({ defaultLayout, ...widget }) => {
    const state = get();
    if (state.dashboardWidgets.some((w) => w.id === widget.id)) return;

    // Restore previously saved enabled state (e.g. user had hidden the widget)
    const storedEnabledMap = (() => {
      try {
        const s = localStorage.getItem(WIDGETS_KEY);
        return s ? (JSON.parse(s) as Record<string, boolean>) : {};
      } catch { return {}; }
    })();
    const resolvedWidget = widget.id in storedEnabledMap
      ? { ...widget, enabled: storedEnabledMap[widget.id] }
      : widget;

    const updatedWidgets = [...state.dashboardWidgets, resolvedWidget];
    const enabledMap = Object.fromEntries(updatedWidgets.map((w) => [w.id, w.enabled]));
    localStorage.setItem(WIDGETS_KEY, JSON.stringify(enabledMap));

    // Extend existing layouts with the plugin widget's default layout
    if (defaultLayout) {
      const updatedLayouts = Object.fromEntries(
        Object.entries(state.gridLayouts).map(([bp, items]) => {
          if (items.some((item) => item.i === widget.id)) return [bp, items];
          const maxY = items.reduce((acc, item) => Math.max(acc, item.y + item.h), 0);
          return [bp, [...items, { i: widget.id, x: 0, y: maxY, w: defaultLayout.w, h: defaultLayout.h }]];
        }),
      );
      persistLayoutsDebounced(updatedLayouts);
      set({ dashboardWidgets: updatedWidgets, gridLayouts: updatedLayouts });
    } else {
      set({ dashboardWidgets: updatedWidgets });
    }
  },
  gridLayouts: loadLayouts(),
  setGridLayouts: (layouts) => {
    set({ gridLayouts: layouts });
    persistLayoutsDebounced(layouts);
  },
  resetGridLayouts: () => {
    if (_layoutsPersistTimer !== null) {
      clearTimeout(_layoutsPersistTimer);
      _layoutsPersistTimer = null;
    }
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
