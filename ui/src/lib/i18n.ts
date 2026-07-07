import i18n from 'i18next'
import { initReactI18next } from 'react-i18next'

// The six shipping locales. `dir` drives RTL for Arabic; `native` labels the language switcher.
export const LOCALES = [
  { code: 'en', name: 'English', native: 'English', dir: 'ltr' },
  { code: 'tr', name: 'Türkçe', native: 'Türkçe', dir: 'ltr' },
  { code: 'fr', name: 'Français', native: 'Français', dir: 'ltr' },
  { code: 'ar', name: 'العربية', native: 'العربية', dir: 'rtl' },
  { code: 'zh', name: '中文', native: '中文', dir: 'ltr' },
  { code: 'ja', name: '日本語', native: '日本語', dir: 'ltr' },
] as const

export type LocaleCode = (typeof LOCALES)[number]['code']

const resources = {
  en: { translation: {
    brand: { name: 'Mockifyr', sub: 'Mock Platform' },
    nav: { overview: 'Overview', mocking: 'Mocking', platform: 'Platform',
      dashboard: 'Dashboard', stubs: 'Stubs', journal: 'Request journal', scenarios: 'Scenarios',
      recordings: 'Recordings', extensions: 'Extensions', settings: 'Settings' },
    common: { search: 'Search…', expand: 'Expand sidebar', collapse: 'Collapse sidebar',
      language: 'Language', darkMode: 'Dark mode', profile: 'Profile', reportIssue: 'Report an issue',
      signOut: 'Sign out', administrator: 'Administrator', tenant: 'Tenant', switchTenant: 'Switch tenant' },
    dashboard: { title: 'Dashboard', subtitle: "Here's what's happening across your mock platform.",
      activeStubs: 'Active stubs', requests: 'Requests · 24h', unmatched: 'Unmatched · 24h',
      matchTime: 'Avg match time', thisWeek: 'this week', stable: 'stable', verified: 'Differentially verified' },
    status: { live: 'Live', proxy: 'Proxying', draft: 'Draft' },
    stubs: { subtitle: 'Request-matching rules served by this tenant. Every change is verified against the reference oracle.',
      newStub: 'New stub', import: 'Import', filter: 'Filter by URL…', all: 'All', method: 'Method', url: 'URL pattern',
      priority: 'Priority', scenario: 'Scenario', persistence: 'Persistence', lastMatched: 'Last matched', status: 'Status',
      density: 'Density', selected: '{{count}} selected', delete: 'Delete', showing: 'Showing', of: 'of',
      empty: 'No stubs match your filters.', sample: 'Sample data — no host connected', never: 'never', edit: 'Edit', duplicate: 'Duplicate' },
    editor: { newTitle: 'New stub', editTitle: 'Edit stub', form: 'Form', request: 'Request', response: 'Response',
      behavior: 'Behavior', urlMatch: 'URL match', headerMatchers: 'Header matchers', bodyMatchers: 'Body matchers',
      value: 'Value', statusCode: 'Status code', priority: 'Priority', responseHeaders: 'Response headers', body: 'Body',
      templating: 'Apply response templating', delay: 'Fixed delay (ms)', fault: 'Fault', none: 'None', proxy: 'Proxy base URL',
      requiredState: 'Required state', newState: 'New state', cancel: 'Cancel', save: 'Save stub',
      saved: 'Stub saved', savedSample: 'Saved (sample mode — no host)', invalidJson: 'Invalid JSON', deleted: 'Stub deleted' },
    journal: { subtitle: 'Every request served to this tenant — matched or not. Unmatched requests are the near-miss candidates.',
      allRequests: 'All requests', unmatchedOnly: 'Unmatched', status: 'Status', result: 'Result', matched: 'Matched',
      unmatched: 'Unmatched', filter: 'Filter by URL…', empty: 'No requests recorded yet.' },
  } },
  tr: { translation: {
    brand: { name: 'Mockifyr', sub: 'Mock Platformu' },
    nav: { overview: 'Genel Bakış', mocking: 'Mock', platform: 'Platform',
      dashboard: 'Panel', stubs: "Stub'lar", journal: 'İstek günlüğü', scenarios: 'Senaryolar',
      recordings: 'Kayıtlar', extensions: 'Eklentiler', settings: 'Ayarlar' },
    common: { search: 'Ara…', expand: 'Menüyü genişlet', collapse: 'Menüyü daralt',
      language: 'Dil', darkMode: 'Koyu tema', profile: 'Profil', reportIssue: 'Sorun bildir',
      signOut: 'Çıkış yap', administrator: 'Yönetici', tenant: 'Kiracı', switchTenant: 'Kiracı değiştir' },
    dashboard: { title: 'Panel', subtitle: 'Mock platformunda olan biten.',
      activeStubs: 'Aktif stub', requests: 'İstek · 24s', unmatched: 'Eşleşmeyen · 24s',
      matchTime: 'Ort. eşleşme süresi', thisWeek: 'bu hafta', stable: 'sabit', verified: 'Diferansiyel doğrulandı' },
    status: { live: 'Canlı', proxy: 'Proxy', draft: 'Taslak' },
    stubs: { subtitle: "Bu kiracının sunduğu istek-eşleştirme kuralları. Her değişiklik referans oracle'a karşı doğrulanır.",
      newStub: 'Yeni stub', import: 'İçe aktar', filter: "URL'ye göre filtrele…", all: 'Tümü', method: 'Metot', url: 'URL deseni',
      priority: 'Öncelik', scenario: 'Senaryo', persistence: 'Kalıcılık', lastMatched: 'Son eşleşme', status: 'Durum',
      density: 'Yoğunluk', selected: '{{count}} seçili', delete: 'Sil', showing: 'Gösterilen', of: '/',
      empty: 'Filtrelerinize uyan stub yok.', sample: 'Örnek veri — host bağlı değil', never: 'hiç', edit: 'Düzenle', duplicate: 'Çoğalt' },
    editor: { newTitle: 'Yeni stub', editTitle: 'Stub düzenle', form: 'Form', request: 'İstek', response: 'Yanıt',
      behavior: 'Davranış', urlMatch: 'URL eşleşmesi', headerMatchers: 'Header eşleştiricileri', bodyMatchers: 'Gövde eşleştiricileri',
      value: 'Değer', statusCode: 'Durum kodu', priority: 'Öncelik', responseHeaders: "Yanıt header'ları", body: 'Gövde',
      templating: 'Yanıt templating uygula', delay: 'Sabit gecikme (ms)', fault: 'Hata (fault)', none: 'Yok', proxy: 'Proxy base URL',
      requiredState: 'Gerekli durum', newState: 'Yeni durum', cancel: 'İptal', save: "Stub'ı kaydet",
      saved: 'Stub kaydedildi', savedSample: 'Kaydedildi (örnek mod — host yok)', invalidJson: 'Geçersiz JSON', deleted: 'Stub silindi' },
    journal: { subtitle: 'Bu kiracıya sunulan her istek — eşleşen ya da eşleşmeyen. Eşleşmeyenler near-miss adayları.',
      allRequests: 'Tüm istekler', unmatchedOnly: 'Eşleşmeyen', status: 'Durum', result: 'Sonuç', matched: 'Eşleşti',
      unmatched: 'Eşleşmedi', filter: "URL'ye göre filtrele…", empty: 'Henüz kayıtlı istek yok.' },
  } },
  fr: { translation: {
    brand: { name: 'Mockifyr', sub: 'Plateforme Mock' },
    nav: { overview: 'Aperçu', mocking: 'Simulation', platform: 'Plateforme',
      dashboard: 'Tableau de bord', stubs: 'Stubs', journal: 'Journal des requêtes', scenarios: 'Scénarios',
      recordings: 'Enregistrements', extensions: 'Extensions', settings: 'Paramètres' },
    common: { search: 'Rechercher…', expand: 'Développer le menu', collapse: 'Réduire le menu',
      language: 'Langue', darkMode: 'Mode sombre', profile: 'Profil', reportIssue: 'Signaler un problème',
      signOut: 'Se déconnecter', administrator: 'Administrateur', tenant: 'Locataire', switchTenant: 'Changer de locataire' },
    dashboard: { title: 'Tableau de bord', subtitle: 'Voici ce qui se passe sur votre plateforme mock.',
      activeStubs: 'Stubs actifs', requests: 'Requêtes · 24h', unmatched: 'Sans correspondance · 24h',
      matchTime: 'Temps moyen', thisWeek: 'cette semaine', stable: 'stable', verified: 'Vérifié différentiellement' },
    status: { live: 'Actif', proxy: 'Proxy', draft: 'Brouillon' },
  } },
  ar: { translation: {
    brand: { name: 'Mockifyr', sub: 'منصة المحاكاة' },
    nav: { overview: 'نظرة عامة', mocking: 'المحاكاة', platform: 'المنصة',
      dashboard: 'لوحة التحكم', stubs: 'القوالب', journal: 'سجل الطلبات', scenarios: 'السيناريوهات',
      recordings: 'التسجيلات', extensions: 'الإضافات', settings: 'الإعدادات' },
    common: { search: 'بحث…', expand: 'توسيع القائمة', collapse: 'طي القائمة',
      language: 'اللغة', darkMode: 'الوضع الداكن', profile: 'الملف الشخصي', reportIssue: 'الإبلاغ عن مشكلة',
      signOut: 'تسجيل الخروج', administrator: 'مدير', tenant: 'المستأجر', switchTenant: 'تبديل المستأجر' },
    dashboard: { title: 'لوحة التحكم', subtitle: 'إليك ما يحدث عبر منصة المحاكاة.',
      activeStubs: 'القوالب النشطة', requests: 'الطلبات · ٢٤س', unmatched: 'غير مطابق · ٢٤س',
      matchTime: 'متوسط زمن المطابقة', thisWeek: 'هذا الأسبوع', stable: 'مستقر', verified: 'تم التحقق تفاضلياً' },
    status: { live: 'نشط', proxy: 'وسيط', draft: 'مسودة' },
  } },
  zh: { translation: {
    brand: { name: 'Mockifyr', sub: 'Mock 平台' },
    nav: { overview: '概览', mocking: '模拟', platform: '平台',
      dashboard: '仪表板', stubs: '存根', journal: '请求日志', scenarios: '场景',
      recordings: '录制', extensions: '扩展', settings: '设置' },
    common: { search: '搜索…', expand: '展开侧栏', collapse: '收起侧栏',
      language: '语言', darkMode: '深色模式', profile: '个人资料', reportIssue: '报告问题',
      signOut: '退出登录', administrator: '管理员', tenant: '租户', switchTenant: '切换租户' },
    dashboard: { title: '仪表板', subtitle: '以下是您的模拟平台的动态。',
      activeStubs: '活动存根', requests: '请求 · 24小时', unmatched: '未匹配 · 24小时',
      matchTime: '平均匹配耗时', thisWeek: '本周', stable: '稳定', verified: '差分验证通过' },
    status: { live: '运行中', proxy: '代理中', draft: '草稿' },
  } },
  ja: { translation: {
    brand: { name: 'Mockifyr', sub: 'モックプラットフォーム' },
    nav: { overview: '概要', mocking: 'モック', platform: 'プラットフォーム',
      dashboard: 'ダッシュボード', stubs: 'スタブ', journal: 'リクエスト履歴', scenarios: 'シナリオ',
      recordings: 'レコーディング', extensions: '拡張機能', settings: '設定' },
    common: { search: '検索…', expand: 'サイドバーを展開', collapse: 'サイドバーを折りたたむ',
      language: '言語', darkMode: 'ダークモード', profile: 'プロフィール', reportIssue: '問題を報告',
      signOut: 'サインアウト', administrator: '管理者', tenant: 'テナント', switchTenant: 'テナントを切り替え' },
    dashboard: { title: 'ダッシュボード', subtitle: 'モックプラットフォームの最新状況です。',
      activeStubs: '有効なスタブ', requests: 'リクエスト · 24h', unmatched: '未マッチ · 24h',
      matchTime: '平均マッチ時間', thisWeek: '今週', stable: '安定', verified: '差分検証済み' },
    status: { live: '稼働中', proxy: 'プロキシ', draft: '下書き' },
  } },
}

void i18n.use(initReactI18next).init({
  resources,
  lng: 'en',
  fallbackLng: 'en',
  interpolation: { escapeValue: false },
})

/** Apply a locale: switch strings and flip document direction for RTL. */
export function applyLocale(code: LocaleCode) {
  const locale = LOCALES.find((l) => l.code === code) ?? LOCALES[0]
  void i18n.changeLanguage(code)
  document.documentElement.dir = locale.dir
  document.documentElement.lang = code
}

export default i18n
