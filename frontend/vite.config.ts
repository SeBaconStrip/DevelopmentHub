import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react-swc'
import tailwindcss from '@tailwindcss/vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: 'http://localhost:5131',
        changeOrigin: true,
      },
      '/hubs': {
        target: 'http://localhost:5131',
        changeOrigin: true,
        ws: true,
      },
    },
  },
})
