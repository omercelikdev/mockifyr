import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import path from 'node:path'

// The dashboard is a static SPA that talks only to the mock server's /__admin/* REST API.
// In dev, proxy those calls to a running Mockifyr host (default :8080). In prod it is built to
// static assets served by the host, so a relative base keeps asset paths portable.
export default defineConfig({
  base: './',
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: { '@': path.resolve(__dirname, './src') },
  },
  server: {
    port: 5173,
    proxy: {
      '/__admin': { target: 'http://localhost:8080', changeOrigin: true },
    },
  },
})
