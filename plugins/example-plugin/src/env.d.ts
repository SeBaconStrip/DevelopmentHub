/// <reference types="vite/client" />

import type { DhSdk } from '@developmenthub/plugin-sdk';

declare global {
  interface Window {
    __dhSdk: DhSdk;
  }
}
