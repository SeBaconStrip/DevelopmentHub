import type { ComponentType } from 'react';

export interface RegisteredWidget {
  pluginId: string;
  widgetId: string;
  label: string;
  icon: string;
  defaultLayout: { w: number; h: number };
  component: ComponentType;
}

export interface RegisteredRoute {
  pluginId: string;
  path: string;
  navLabel: string;
  navOrder: number;
  component: ComponentType;
}

class PluginRegistry {
  private _widgets = new Map<string, RegisteredWidget>();
  private _routes = new Map<string, RegisteredRoute>();

  registerWidget(
    widgetId: string,
    component: ComponentType,
    meta: Omit<RegisteredWidget, 'widgetId' | 'component'>,
  ) {
    this._widgets.set(widgetId, { widgetId, component, ...meta });
  }

  registerRoute(
    path: string,
    component: ComponentType,
    meta: Omit<RegisteredRoute, 'path' | 'component'>,
  ) {
    this._routes.set(path, { path, component, ...meta });
  }

  get widgets(): RegisteredWidget[] {
    return [...this._widgets.values()];
  }

  get routes(): RegisteredRoute[] {
    return [...this._routes.values()].sort((a, b) => a.navOrder - b.navOrder);
  }
}

export const pluginRegistry = new PluginRegistry();
