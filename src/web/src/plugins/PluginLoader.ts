import React from 'react';
import { useQuery, useMutation, QueryClient } from '@tanstack/react-query';
import { create } from 'zustand';
import { useNavigate, Link } from 'react-router-dom';
import { pluginRegistry } from './PluginRegistry';
import { useUiStore } from '../store/uiStore';
import * as PluginUi from '../plugin-sdk/ui';
import { apiFetch } from '../api/client';

export interface PluginSettingDefinition {
  key: string;
  label: string;
  /** "text" | "bool" | "select" */
  type: string;
  defaultValue: string;
  options?: string[];
}

export interface PluginManifest {
  id: string;
  version: string;
  name: string;
  description?: string;
  bundleMtime?: number;
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
  settings?: PluginSettingDefinition[];
}

let _queryClient: QueryClient | null = null;

export function initPluginLoader(queryClient: QueryClient) {
  _queryClient = queryClient;
}

export async function loadAllPlugins(): Promise<PluginManifest[]> {
  try {
    const [pluginsRes, configRes] = await Promise.all([
      apiFetch('/api/plugins/enabled'),
      apiFetch('/api/config'),
    ]);
    if (!pluginsRes.ok) return [];
    const manifests: PluginManifest[] = await pluginsRes.json();
    const config = configRes.ok ? await configRes.json() : {};
    const allPluginSettings: Record<string, Record<string, string>> = config.pluginSettings ?? {};

    for (const manifest of manifests) {
      if (manifest.frontend?.enabled) {
        const pluginSettings = allPluginSettings[manifest.id] ?? {};
        await loadPluginBundle(manifest, pluginSettings).catch((err) =>
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

async function loadPluginBundle(manifest: PluginManifest, pluginSettings: Record<string, string>): Promise<void> {
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
    apiFetch,
    settings: pluginSettings,
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

  const cacheBuster = manifest.bundleMtime ?? manifest.version;
  const bundleUrl = `/api/plugins/${encodeURIComponent(manifest.id)}/ui/bundle.js?v=${encodeURIComponent(cacheBuster)}`;

  // Remove any previously injected script for this plugin so a reload always
  // re-executes the bundle (important during development / hot reload).
  document.head.querySelectorAll(`script[data-plugin-id="${manifest.id}"]`)
    .forEach((el) => el.remove());

  await new Promise<void>((resolve, reject) => {
    const script = document.createElement('script');
    script.type = 'module';
    script.src = bundleUrl;
    script.dataset.pluginId = manifest.id;
    script.onload = () => resolve();
    script.onerror = (e) => reject(new Error(`Plugin bundle load failed: ${manifest.id} — ${e}`));
    document.head.appendChild(script);
  });
}
