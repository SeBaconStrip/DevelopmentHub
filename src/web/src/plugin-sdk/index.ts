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
import type { QueryClient, UseQueryResult } from '@tanstack/react-query';
import type { NavigateFunction } from 'react-router-dom';

// ── UI component types ─────────────────────────────────────────────────────

export interface PluginUi {
  /** Themed button. `variant` defaults to `"ghost"`. */
  Button: React.ComponentType<
    React.ButtonHTMLAttributes<HTMLButtonElement> & { variant?: 'primary' | 'secondary' | 'ghost' }
  >;
  /** Themed surface card. */
  Card: React.ComponentType<React.HTMLAttributes<HTMLDivElement>>;
  /** Themed text input, stretches to container width. */
  Input: React.ComponentType<React.InputHTMLAttributes<HTMLInputElement>>;
  /** Search input with inline ✕ clear button. Requires controlled `value` + `onChange`.
   *  Pass `onClear` to handle clearing without a synthetic event; pass `wrapperClassName` to style the outer div. */
  SearchInput: React.ComponentType<
    React.InputHTMLAttributes<HTMLInputElement> & { wrapperClassName?: string; onClear?: () => void }
  >;
  /** Small inline badge/chip. Pass `color` to override background. */
  Chip: React.ComponentType<React.HTMLAttributes<HTMLSpanElement> & { color?: string }>;
  /** Centered muted placeholder for empty lists or states. */
  Empty: React.ComponentType<React.HTMLAttributes<HTMLDivElement>>;
  /** Scrollable page wrapper — use as the root element of plugin pages. */
  PageRoot: React.ComponentType<React.HTMLAttributes<HTMLDivElement>>;
  /** Animated loading spinner. `size` defaults to `20` (px). */
  Spinner: React.ComponentType<{ size?: number }>;
}

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
  /** Base path for API calls, e.g. `/api`. Use `apiFetch(apiBase + '/my-endpoint')`. */
  apiBase: '/api';
  /** Authenticated fetch — always use this instead of fetch() for API calls. */
  apiFetch: (input: RequestInfo | URL, init?: RequestInit) => Promise<Response>;
  /** This plugin's saved settings from user config. */
  settings: Record<string, string>;

  // ── UI framework ──────────────────────────────────────────────────────────
  /** Pre-built, theme-aware React components. Use instead of raw CSS classes. */
  ui: PluginUi;

  // ── Plugin registration ───────────────────────────────────────────────────
  plugin: PluginRegistration;
}

declare global {
  interface Window {
    __dhSdk: DhSdk;
  }
}
