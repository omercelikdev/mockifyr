import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Camera, Circle, Play, Square } from 'lucide-react'
import { toast } from 'sonner'
import { cn } from '@/lib/utils'
import { useUi } from '@/components/providers'
import {
  fetchRecordingStatus, snapshotRecording, startRecording, stopRecording, type CapturedStub,
} from '@/lib/api'
import { MethodChip } from '@/components/ui/badges'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/field'

export function RecordingsPage() {
  const { t } = useTranslation()
  const { tenant } = useUi()
  const queryClient = useQueryClient()
  const [target, setTarget] = useState('https://api.example.com')
  const [captured, setCaptured] = useState<CapturedStub[]>([])

  const { data } = useQuery({ queryKey: ['recording-status', tenant], queryFn: () => fetchRecordingStatus(tenant), refetchInterval: (q) => (q.state.data?.mock ? false : 4000) })
  const recording = data?.status === 'Recording'
  const refreshStatus = () => void queryClient.invalidateQueries({ queryKey: ['recording-status', tenant] })

  const start = useMutation({
    mutationFn: () => startRecording(tenant, target.trim()),
    onSuccess: ({ mock }) => { toast[mock ? 'message' : 'success'](mock ? t('editor.savedSample') : t('recordings.started')); refreshStatus() },
  })
  const snapshot = useMutation({
    mutationFn: () => snapshotRecording(tenant),
    onSuccess: ({ stubs, mock }) => { setCaptured(stubs); toast[mock ? 'message' : 'success'](mock ? t('editor.savedSample') : t('recordings.snapshotTaken', { count: stubs.length })) },
  })
  const stop = useMutation({
    mutationFn: () => stopRecording(tenant),
    onSuccess: ({ stubs, mock }) => { setCaptured(stubs); toast[mock ? 'message' : 'success'](mock ? t('editor.savedSample') : t('recordings.stopped', { count: stubs.length })); refreshStatus() },
  })

  return (
    <div className="mx-auto max-w-[1360px]">
      <header className="mb-6">
        <h1 className="text-[22px] font-bold tracking-tight">{t('nav.recordings')}</h1>
        <p className="mt-1 max-w-[62ch] text-sm text-muted-foreground">{t('recordings.subtitle')}</p>
      </header>

      {/* Session control */}
      <div className="rounded-2xl border border-border bg-background p-5 shadow-surface">
        <div className="flex items-center gap-2">
          <span className={cn('inline-flex items-center gap-2 rounded-full border px-3 py-1 text-xs font-semibold',
            recording ? 'border-danger-border bg-danger-bg text-danger' : 'border-border bg-muted text-muted-foreground')}>
            <Circle className={cn('size-2.5', recording ? 'animate-pulse fill-danger' : 'fill-faint')} />
            {recording ? t('recordings.recording') : t('recordings.stopped_')}
          </span>
          {data?.mock && (
            <span className="rounded-full border border-warning-border bg-warning-bg px-2.5 py-0.5 text-[11.5px] font-medium text-warning">{t('stubs.sample')}</span>
          )}
        </div>

        <div className="mt-4 flex flex-wrap items-end gap-3">
          <div className="min-w-[280px] flex-1">
            <label className="mb-1.5 block text-xs font-semibold text-muted-foreground">{t('recordings.target')}</label>
            <Input value={target} onChange={(e) => setTarget(e.target.value)} disabled={recording} className="font-mono" placeholder="https://api.example.com" />
          </div>
          {recording ? (
            <div className="flex gap-2">
              <Button variant="outline" onClick={() => snapshot.mutate()} disabled={snapshot.isPending}><Camera />{t('recordings.snapshot')}</Button>
              <Button variant="danger" onClick={() => stop.mutate()} disabled={stop.isPending}><Square />{t('recordings.stop')}</Button>
            </div>
          ) : (
            <Button variant="primary" onClick={() => start.mutate()} disabled={start.isPending || !target.trim()}><Play />{t('recordings.start')}</Button>
          )}
        </div>
        <p className="mt-3 text-xs text-muted-foreground">{t('recordings.hint')}</p>
      </div>

      {/* Captured stubs */}
      <div className="mt-4 overflow-hidden rounded-2xl border border-border bg-background shadow-surface">
        <div className="flex items-center gap-2 border-b border-border px-4 py-3">
          <h2 className="text-sm font-semibold">{t('recordings.captured')}</h2>
          <span className="text-xs text-muted-foreground tabular-nums">· {captured.length}</span>
        </div>
        {captured.length === 0 ? (
          <div className="px-4 py-14 text-center text-sm text-muted-foreground">{t('recordings.captureEmpty')}</div>
        ) : (
          <ul className="divide-y divide-border">
            {captured.map((s, i) => (
              <li key={i} className="flex items-center gap-3 px-4 py-3">
                <MethodChip method={s.method} />
                <span className="min-w-0 flex-1 truncate font-mono text-[12.5px]">{s.url}</span>
                <details className="shrink-0">
                  <summary className="cursor-pointer list-none text-xs font-semibold text-muted-foreground hover:text-foreground">{t('recordings.viewJson')}</summary>
                </details>
              </li>
            ))}
          </ul>
        )}
      </div>
    </div>
  )
}
