import { createBrowserRouter, RouterProvider } from 'react-router-dom'
import { Toaster } from 'sonner'
import { AppShell } from '@/components/layout/app-shell'
import { useUi } from '@/components/providers'
import { DashboardPage } from '@/pages/dashboard'
import { StubsPage } from '@/pages/stubs'
import { JournalPage } from '@/pages/journal'
import { PlaceholderPage } from '@/pages/placeholder'

const router = createBrowserRouter([
  {
    path: '/',
    element: <AppShell />,
    children: [
      { index: true, element: <DashboardPage /> },
      { path: 'stubs', element: <StubsPage /> },
      { path: 'journal', element: <JournalPage /> },
      { path: 'scenarios', element: <PlaceholderPage titleKey="nav.scenarios" /> },
      { path: 'recordings', element: <PlaceholderPage titleKey="nav.recordings" /> },
      { path: 'extensions', element: <PlaceholderPage titleKey="nav.extensions" /> },
      { path: 'settings', element: <PlaceholderPage titleKey="nav.settings" /> },
    ],
  },
])

export default function App() {
  const { theme } = useUi()
  return (
    <>
      <RouterProvider router={router} />
      <Toaster theme={theme} position="bottom-right" toastOptions={{ style: { borderRadius: '10px' } }} />
    </>
  )
}
