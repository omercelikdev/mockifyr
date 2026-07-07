import { Outlet } from 'react-router-dom'
import { AppSidebar } from './app-sidebar'
import { CommandPalette } from '@/components/command-palette'
import { ErrorBoundary } from '@/components/error-boundary'
import { useUi } from '@/components/providers'
import { cn } from '@/lib/utils'

// App-shell: the page never scrolls (bg-app ground). The sidebar sits flush; the content lives in a
// fixed, rounded surface and only that surface scrolls (scroll-area = auto-hiding scrollbar). Mirrors
// the Praxis layout so the frame stays put while content moves.
export function AppShell() {
  const { collapsed } = useUi()
  return (
    <div className="flex h-dvh overflow-hidden bg-app">
      <aside
        className={cn(
          'shrink-0 bg-sidebar transition-[width] duration-300 ease-[cubic-bezier(.4,0,.2,1)]',
          collapsed ? 'w-[74px]' : 'w-[252px]',
        )}
      >
        <AppSidebar />
      </aside>
      <div className="min-w-0 flex-1 p-3 pe-3 ps-0">
        <main className="scroll-area h-full overflow-y-auto rounded-2xl border border-border bg-surface p-6 shadow-surface md:p-7">
          <ErrorBoundary>
            <Outlet />
          </ErrorBoundary>
        </main>
      </div>
      <ErrorBoundary>
        <CommandPalette />
      </ErrorBoundary>
    </div>
  )
}
