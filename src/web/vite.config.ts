import { defineConfig, loadEnv } from 'vite'
import react from '@vitejs/plugin-react-swc'
import tailwindcss from '@tailwindcss/vite'

// https://vite.dev/config/
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd())
  const port = parseInt(env.VITE_PORT ?? '5173')

  return {
    plugins: [react(), tailwindcss()],
    server: {
      port,
      proxy: {
        '/api': {
          target: 'http://localhost:7131',
          changeOrigin: true,
        },
        '/hubs': {
          target: 'http://localhost:7131',
          changeOrigin: true,
          ws: true,
        },
      },
    },
  }
})
