import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
  // Use classic JSX runtime so JSX compiles to React.createElement(...)
  // instead of importing from react/jsx-runtime.
  // React is provided at runtime via window.__dhSdk.React.
  plugins: [react({ jsxRuntime: 'classic' })],
  build: {
    lib: {
      entry: 'src/index.ts',
      formats: ['es'],
      fileName: () => 'index.js',
    },
    outDir: 'ui',
    emptyOutDir: true,
    rollupOptions: {
      // All of these are provided by the host via window.__dhSdk — do not bundle them.
      external: ['react', 'react-dom', 'react/jsx-runtime', '@tanstack/react-query', 'zustand', 'react-router-dom'],
    },
  },
});
