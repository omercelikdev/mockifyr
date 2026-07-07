import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { QueryClientProvider } from '@tanstack/react-query'
import './index.css'
import './lib/i18n'
import App from './App.tsx'
import { UiProvider } from '@/components/providers'
import { queryClient } from '@/lib/query'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <UiProvider>
        <App />
      </UiProvider>
    </QueryClientProvider>
  </StrictMode>,
)
