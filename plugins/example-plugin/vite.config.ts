import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react-swc';

export default defineConfig({
  plugins: [react()],
  build: {
    lib: {
      entry: 'src/index.ts',
      formats: ['es'],
      fileName: () => 'index.js',
    },
    outDir: 'ui',
    emptyOutDir: true,
    rollupOptions: {
      // These are provided by the host via window.__dhSdk — do not bundle them.
      external: ['react', 'react-dom', '@tanstack/react-query', 'zustand', 'react-router-dom'],
    },
  },
});
