import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig(({ command }) => ({
  // Relative base for production builds so the app can be hosted under any virtual path
  // (e.g. "/reactapp"); ReactApp.Api injects <base href> + window.__BASE_PATH__ at serve time.
  base: command === 'build' ? './' : '/',
  plugins: [react()],
  server: {
    port: 5173,
    strictPort: true,
    proxy: {
      // Local dev: the SPA calls relative "/api/..."; forward it to ReactApp.Api (run separately
      // on https://localhost:7262). No CORS needed since the browser sees a same-origin call.
      '/api': { target: 'https://localhost:7262', changeOrigin: true, secure: false },
    },
  },
  preview: {
    port: 4173,
    strictPort: true,
  },
}))
