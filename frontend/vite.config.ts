import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig, loadEnv } from 'vite'
import { readFileSync } from 'node:fs'
import { Agent } from 'node:https'

// https://vite.dev/config/
export default defineConfig(({ command, mode }) => {
  if (command === 'build') {
    return {
      plugins: [react(), tailwindcss()],
      build: {
        rollupOptions: {
          output: {
            manualChunks(id) {
              if (id.includes('node_modules')) {
                if (id.includes('/react/') || id.includes('/react-dom/')) {
                  return 'vendor-react';
                }
                if (id.includes('react-router') || id.includes('react-router-dom')) {
                  return 'vendor-router';
                }
                if (id.includes('@tanstack/react-query') || id.includes('@tanstack/query-core')) {
                  return 'vendor-query';
                }
                if (id.includes('monaco-editor') || id.includes('@monaco-editor')) {
                  return 'vendor-monaco';
                }
                if (id.includes('@codemirror') || id.includes('@uiw/react-codemirror')) {
                  return 'vendor-codemirror';
                }
                if (id.includes('lucide-react')) {
                  return 'vendor-icons';
                }
              }
            },
          },
        },
        chunkSizeWarningLimit: 800,
      },
    };
  }

  const env = loadEnv(mode, process.cwd(), 'VITE_')
  if (process.env.VITE_USE_MOCK === 'true' || env.VITE_USE_MOCK === 'true') {
    return { plugins: [react(), tailwindcss()], server: { host: 'localhost', port: 5174, strictPort: true } }
  }

  const cert = readFileSync(new URL('../api/secrets/dev/localhost.pem', import.meta.url))
  const key = readFileSync(new URL('../api/secrets/dev/localhost.key', import.meta.url))
  const proxy = Object.fromEntries(['/api', '/health', '/swagger'].map(path => [path, {
    target: 'https://localhost:5001',
    changeOrigin: true,
    secure: true,
    agent: new Agent({ ca: cert }),
  }]))

  return {
  plugins: [
    react(),
    tailwindcss(),
  ],
  server: { host: 'localhost', port: 5173, strictPort: true, https: { cert, key }, proxy },
  preview: { host: 'localhost', port: 4173, strictPort: true, https: { cert, key }, proxy },
  }
})
