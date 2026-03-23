/// <reference types="vite/client" />

// Import type-only SDK definitions so the plugin gets full TypeScript support.
// At runtime the objects come from window.__dhSdk — never import them as values.
import type { DhSdk } from '../../../src/web/src/plugin-sdk/index';

declare global {
  interface Window {
    __dhSdk: DhSdk;
  }
}
