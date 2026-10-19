import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { QueryClientProvider } from '@tanstack/react-query';
import { Toaster } from 'sonner';
import App from './App.tsx';
import './index.css';

import { queryClient } from './lib/queryClient';

async function enableMocking() {
  if (!import.meta.env.DEV || (import.meta.env.VITE_ENABLE_MOCKS !== 'true' && import.meta.env.VITE_USE_MOCK !== 'true')) {
    return;
  }
  const { worker } = await import('./mocks/browser');
  if (import.meta.env.VITE_USE_MOCK === 'true') {
    const { workspaceHandlers } = await import('./features/workspace/mocks/handlers');
    worker.use(...workspaceHandlers);
    const { recordActivity } = await import('./features/auth/sessionActivity');
    recordActivity();
  }
  // Mock authentication is opt-in; normal development uses the real API.
  return worker.start();
}

enableMocking().then(() => {
  createRoot(document.getElementById('root')!).render(
    <StrictMode>
      <QueryClientProvider client={queryClient}>
        <App />
        <Toaster position="top-right" richColors />
      </QueryClientProvider>
    </StrictMode>
  );
});
