/// <reference types="vite/client" />

import type { DhSdk } from '../../../src/web/src/plugin-sdk/index';

declare global {
  interface Window {
    __dhSdk: DhSdk;
  }
}
