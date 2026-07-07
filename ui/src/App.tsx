import { createBrowserRouter, RouterProvider } from 'react-router-dom'
import { AppShell } from '@/components/layout/app-shell'
import { DashboardPage } from '@/pages/dashboard'
import { StubsPage } from '@/pages/stubs'
import { PlaceholderPage } from '@/pages/placeholder'

const router = createBrowserRouter([
  {
    path: '/',
    element: <AppShell />,
    children: [
      { index: true, element: <DashboardPage /> },
      { path: 'stubs', element: <StubsPage /> },
      { path: 'journal', element: <PlaceholderPage titleKey="nav.journal" /> },
      { path: 'scenarios', element: <PlaceholderPage titleKey="nav.scenarios" /> },
      { path: 'recordings', element: <PlaceholderPage titleKey="nav.recordings" /> },
      { path: 'extensions', element: <PlaceholderPage titleKey="nav.extensions" /> },
      { path: 'settings', element: <PlaceholderPage titleKey="nav.settings" /> },
    ],
  },
])

export default function App() {
  return <RouterProvider router={router} />
}
