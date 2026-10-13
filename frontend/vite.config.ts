import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'
import { readFileSync } from 'node:fs'
import { Agent } from 'node:https'

// https://vite.dev/config/
export default defineConfig(({ command }) => {
  // Production assets are built without access to local TLS private keys.
  if (command === 'build') return { plugins: [react(), tailwindcss()] }

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
