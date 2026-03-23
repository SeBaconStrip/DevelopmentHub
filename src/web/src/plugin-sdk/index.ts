/**
 * @developmenthub/plugin-sdk
 *
 * Type definitions for the DevelopmentHub Plugin SDK.
 * Install this package in your plugin project for full TypeScript support.
 *
 * At runtime, all SDK objects are provided by the host via `window.__dhSdk`.
 * Do NOT import these types as values — they are type-only.
 */

import type React from 'react';
import type { QueryClient, UseQueryResult, UseMutationResult } from '@tanstack/react-query';
import type { NavigateFunction } from 'react-router-dom';

export interface PluginRegistration {
  /**
   * Register a React component as a dashboard widget.
   * The widgetId must match an entry in your manifest.json contributes.widgets.
   */
  registerWidget(widgetId: string, component: React.ComponentType): void;

  /**
   * Register a React component as a full page.
   * The path must match an entry in your manifest.json contributes.routes.
   */
  registerRoute(path: string, component: React.ComponentType): void;
}

export interface DhSdk {
  // ── React core ────────────────────────────────────────────────────────────
  React: typeof React;
  useState: typeof React.useState;
  useEffect: typeof React.useEffect;
  useMemo: typeof React.useMemo;
  useCallback: typeof React.useCallback;
  useRef: typeof React.useRef;

  // ── Data fetching (TanStack Query) ────────────────────────────────────────
  useQuery: <TData>(options: Parameters<typeof import('@tanstack/react-query').useQuery<TData>>[0]) => UseQueryResult<TData>;
  useMutation: typeof import('@tanstack/react-query').useMutation;
  queryClient: QueryClient;

  // ── State management ──────────────────────────────────────────────────────
  createStore: typeof import('zustand').create;

  // ── Navigation ────────────────────────────────────────────────────────────
  useNavigate: () => NavigateFunction;
  Link: typeof import('react-router-dom').Link;

  // ── Host API ──────────────────────────────────────────────────────────────
  /** Base path for API calls, e.g. `/api`. Use `fetch(apiBase + '/my-endpoint')`. */
  apiBase: '/api';

  // ── Plugin registration ───────────────────────────────────────────────────
  plugin: PluginRegistration;
}

declare global {
  interface Window {
    __dhSdk: DhSdk;
  }
}
