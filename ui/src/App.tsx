import { createBrowserRouter, RouterProvider } from 'react-router-dom'
import { Toaster } from 'sonner'
import { AppShell } from '@/components/layout/app-shell'
import { useUi } from '@/components/providers'
import { DashboardPage } from '@/pages/dashboard'
import { StubsPage } from '@/pages/stubs'
import { JournalPage } from '@/pages/journal'
import { ScenariosPage } from '@/pages/scenarios'
import { RecordingsPage } from '@/pages/recordings'
import { ExtensionsPage } from '@/pages/extensions'
import { SettingsPage } from '@/pages/settings'

const router = createBrowserRouter([
  {
    path: '/',
    element: <AppShell />,
    children: [
      { index: true, element: <DashboardPage /> },
      { path: 'stubs', element: <StubsPage /> },
      { path: 'journal', element: <JournalPage /> },
      { path: 'scenarios', element: <ScenariosPage /> },
      { path: 'recordings', element: <RecordingsPage /> },
      { path: 'extensions', element: <ExtensionsPage /> },
      { path: 'settings', element: <SettingsPage /> },
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
