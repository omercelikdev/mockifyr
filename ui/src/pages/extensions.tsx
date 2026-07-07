import { useTranslation } from 'react-i18next'
import { Braces, Filter, Network, Puzzle } from 'lucide-react'

// The Extensions screen documents the engine's built-in capabilities and extension seams. These are
// compiled/registered at the host (not admin-mutable), so this is a reference, not an editor.
const GROUPS = [
  { icon: Braces, key: 'templating', items: [
    'Handlebars helpers (string · number · date · json · xml · array · logic)',
    'parseJson (inline + block)', 'jwt · jwks (RS256/HS256)', 'random (Datafaker)', 'now / date matchers',
  ] },
  { icon: Filter, key: 'matchers', items: [
    'equalTo · contains · matches · absent', 'equalToJson · matchesJsonPath · matchesJsonSchema',
    'equalToXml · matchesXPath (XMLUnit)', 'date/time · logic (and/or/not) · basicAuth · multipart', 'priority / near-miss',
  ] },
  { icon: Network, key: 'protocols', items: [
    'HTTP / HTTPS / HTTP2 · mTLS', 'gRPC (protobuf ↔ JSON codec)', 'GraphQL (query/variables/operationName)',
    'WebSocket message serving', 'Multi-domain (host/port/scheme)',
  ] },
  { icon: Puzzle, key: 'seams', items: [
    'Custom matchers (customMatcher)', 'Response transformers', 'Admin API extensions (/__admin/ext/*)',
    'Serve-event listeners (webhooks)', 'Persistence providers',
  ] },
]

export function ExtensionsPage() {
  const { t } = useTranslation()
  return (
    <div className="mx-auto max-w-[1360px]">
      <header className="mb-6">
        <h1 className="text-[22px] font-bold tracking-tight">{t('nav.extensions')}</h1>
        <p className="mt-1 max-w-[62ch] text-sm text-muted-foreground">{t('extensions.subtitle')}</p>
      </header>

      <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
        {GROUPS.map(({ icon: Icon, key, items }) => (
          <section key={key} className="rounded-2xl border border-border bg-background p-5 shadow-surface">
            <div className="mb-3 flex items-center gap-2.5">
              <span className="flex size-8 items-center justify-center rounded-lg bg-muted text-muted-foreground"><Icon className="size-4" /></span>
              <h2 className="font-semibold">{t(`extensions.${key}`)}</h2>
            </div>
            <ul className="space-y-1.5 text-sm text-muted-foreground">
              {items.map((i) => <li key={i} className="flex gap-2"><span className="mt-2 size-1 shrink-0 rounded-full bg-faint" />{i}</li>)}
            </ul>
          </section>
        ))}
      </div>
    </div>
  )
}
