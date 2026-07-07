import { lazy, Suspense } from 'react'
import { createBrowserRouter, RouterProvider } from 'react-router-dom'
import { Toaster } from 'sonner'
import { AppShell } from '@/components/layout/app-shell'
import { useUi } from '@/components/providers'

// Route pages are code-split so the initial bundle stays lean; each screen loads on first visit.
const DashboardPage = lazy(() => import('@/pages/dashboard').then((m) => ({ default: m.DashboardPage })))
const StubsPage = lazy(() => import('@/pages/stubs').then((m) => ({ default: m.StubsPage })))
const JournalPage = lazy(() => import('@/pages/journal').then((m) => ({ default: m.JournalPage })))
const ScenariosPage = lazy(() => import('@/pages/scenarios').then((m) => ({ default: m.ScenariosPage })))
const RecordingsPage = lazy(() => import('@/pages/recordings').then((m) => ({ default: m.RecordingsPage })))
const ExtensionsPage = lazy(() => import('@/pages/extensions').then((m) => ({ default: m.ExtensionsPage })))
const SettingsPage = lazy(() => import('@/pages/settings').then((m) => ({ default: m.SettingsPage })))

function Page({ children }: { children: React.ReactNode }) {
  return <Suspense fallback={<div className="h-40 animate-pulse rounded-2xl bg-muted" />}>{children}</Suspense>
}

// The base path the app is served under — '/' in dev, or the embedded prefix (e.g. '/__mockifyr/')
// when the .NET host serves the built dashboard. Vite injects it from its `base` config.
const router = createBrowserRouter([
  {
    path: '/',
    element: <AppShell />,
    children: [
      { index: true, element: <Page><DashboardPage /></Page> },
      { path: 'stubs', element: <Page><StubsPage /></Page> },
      { path: 'journal', element: <Page><JournalPage /></Page> },
      { path: 'scenarios', element: <Page><ScenariosPage /></Page> },
      { path: 'recordings', element: <Page><RecordingsPage /></Page> },
      { path: 'extensions', element: <Page><ExtensionsPage /></Page> },
      { path: 'settings', element: <Page><SettingsPage /></Page> },
    ],
  },
], { basename: import.meta.env.BASE_URL.replace(/\/$/, '') || '/' })

export default function App() {
  const { theme } = useUi()
  return (
    <>
      <RouterProvider router={router} />
      <Toaster theme={theme} position="bottom-right" toastOptions={{ style: { borderRadius: '10px' } }} />
    </>
  )
}
