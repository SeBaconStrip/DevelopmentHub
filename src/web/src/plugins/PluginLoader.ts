import React from 'react';
import { useQuery, useMutation, QueryClient } from '@tanstack/react-query';
import { create } from 'zustand';
import { useNavigate, Link } from 'react-router-dom';
import { pluginRegistry } from './PluginRegistry';
import { useUiStore } from '../store/uiStore';
import * as PluginUi from '../plugin-sdk/ui';

export interface PluginManifest {
  id: string;
  version: string;
  name: string;
  contributes: {
    widgets: Array<{
      id: string;
      label: string;
      icon: string;
      defaultLayout: { w: number; h: number };
    }>;
    routes: Array<{ path: string; navLabel: string; navOrder: number }>;
  };
  frontend?: { bundle: string; enabled: boolean; sdkVersion: string };
}

let _queryClient: QueryClient | null = null;

export function initPluginLoader(queryClient: QueryClient) {
  _queryClient = queryClient;
}

export async function loadAllPlugins(): Promise<PluginManifest[]> {
  try {
    const res = await fetch('/api/plugins');
    if (!res.ok) return [];
    const manifests: PluginManifest[] = await res.json();

    for (const manifest of manifests) {
      if (manifest.frontend?.enabled) {
        await loadPluginBundle(manifest).catch((err) =>
          console.error(`[Plugin ${manifest.id}] Failed to load bundle`, err),
        );
      }
    }

    // Register plugin widgets with the uiStore so they appear in the dashboard
    for (const widget of pluginRegistry.widgets) {
      useUiStore.getState().addPluginWidget({
        id: widget.widgetId,
        label: widget.label,
        icon: widget.icon,
        enabled: true,
        defaultLayout: widget.defaultLayout,
      });
    }

    return manifests;
  } catch (err) {
    console.error('[PluginLoader] Failed to fetch plugin manifests', err);
    return [];
  }
}

async function loadPluginBundle(manifest: PluginManifest): Promise<void> {
  // Expose the SDK surface before injecting the plugin script.
  // The plugin's ESM bundle reads from window.__dhSdk instead of importing directly.
  (window as any).__dhSdk = {
    React,
    useState: React.useState,
    useEffect: React.useEffect,
    useMemo: React.useMemo,
    useCallback: React.useCallback,
    useRef: React.useRef,
    useQuery,
    useMutation,
    queryClient: _queryClient,
    createStore: create,
    useNavigate,
    Link,
    apiBase: '/api',
    ui: PluginUi,
    plugin: {
      registerWidget(widgetId: string, component: React.ComponentType) {
        const widgetMeta = manifest.contributes.widgets.find((w) => w.id === widgetId);
        if (!widgetMeta) {
          console.warn(`[Plugin ${manifest.id}] registerWidget: unknown widgetId "${widgetId}"`);
          return;
        }
        pluginRegistry.registerWidget(widgetId, component, {
          pluginId: manifest.id,
          label: widgetMeta.label,
          icon: widgetMeta.icon,
          defaultLayout: widgetMeta.defaultLayout,
        });
      },
      registerRoute(path: string, component: React.ComponentType) {
        const routeMeta = manifest.contributes.routes.find((r) => r.path === path);
        if (!routeMeta) {
          console.warn(`[Plugin ${manifest.id}] registerRoute: unknown path "${path}"`);
          return;
        }
        pluginRegistry.registerRoute(path, component, {
          pluginId: manifest.id,
          navLabel: routeMeta.navLabel,
          navOrder: routeMeta.navOrder,
        });
      },
    },
  };

  await new Promise<void>((resolve, reject) => {
    const script = document.createElement('script');
    script.type = 'module';
    script.src = `/api/plugins/${encodeURIComponent(manifest.id)}/ui/bundle.js`;
    script.onload = () => resolve();
    script.onerror = (e) => reject(new Error(`Plugin bundle load failed: ${manifest.id} — ${e}`));
    document.head.appendChild(script);
  });
}
