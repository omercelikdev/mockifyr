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
      signOut: 'Sign out', administrator: 'Administrator' },
    dashboard: { title: 'Dashboard', subtitle: "Here's what's happening across your mock platform.",
      activeStubs: 'Active stubs', requests: 'Requests · 24h', unmatched: 'Unmatched · 24h',
      matchTime: 'Avg match time', thisWeek: 'this week', stable: 'stable', verified: 'Differentially verified' },
  } },
  tr: { translation: {
    brand: { name: 'Mockifyr', sub: 'Mock Platformu' },
    nav: { overview: 'Genel Bakış', mocking: 'Mock', platform: 'Platform',
      dashboard: 'Panel', stubs: "Stub'lar", journal: 'İstek günlüğü', scenarios: 'Senaryolar',
      recordings: 'Kayıtlar', extensions: 'Eklentiler', settings: 'Ayarlar' },
    common: { search: 'Ara…', expand: 'Menüyü genişlet', collapse: 'Menüyü daralt',
      language: 'Dil', darkMode: 'Koyu tema', profile: 'Profil', reportIssue: 'Sorun bildir',
      signOut: 'Çıkış yap', administrator: 'Yönetici' },
    dashboard: { title: 'Panel', subtitle: 'Mock platformunda olan biten.',
      activeStubs: 'Aktif stub', requests: 'İstek · 24s', unmatched: 'Eşleşmeyen · 24s',
      matchTime: 'Ort. eşleşme süresi', thisWeek: 'bu hafta', stable: 'sabit', verified: 'Diferansiyel doğrulandı' },
  } },
  fr: { translation: {
    brand: { name: 'Mockifyr', sub: 'Plateforme Mock' },
    nav: { overview: 'Aperçu', mocking: 'Simulation', platform: 'Plateforme',
      dashboard: 'Tableau de bord', stubs: 'Stubs', journal: 'Journal des requêtes', scenarios: 'Scénarios',
      recordings: 'Enregistrements', extensions: 'Extensions', settings: 'Paramètres' },
    common: { search: 'Rechercher…', expand: 'Développer le menu', collapse: 'Réduire le menu',
      language: 'Langue', darkMode: 'Mode sombre', profile: 'Profil', reportIssue: 'Signaler un problème',
      signOut: 'Se déconnecter', administrator: 'Administrateur' },
    dashboard: { title: 'Tableau de bord', subtitle: 'Voici ce qui se passe sur votre plateforme mock.',
      activeStubs: 'Stubs actifs', requests: 'Requêtes · 24h', unmatched: 'Sans correspondance · 24h',
      matchTime: 'Temps moyen', thisWeek: 'cette semaine', stable: 'stable', verified: 'Vérifié différentiellement' },
  } },
  ar: { translation: {
    brand: { name: 'Mockifyr', sub: 'منصة المحاكاة' },
    nav: { overview: 'نظرة عامة', mocking: 'المحاكاة', platform: 'المنصة',
      dashboard: 'لوحة التحكم', stubs: 'القوالب', journal: 'سجل الطلبات', scenarios: 'السيناريوهات',
      recordings: 'التسجيلات', extensions: 'الإضافات', settings: 'الإعدادات' },
    common: { search: 'بحث…', expand: 'توسيع القائمة', collapse: 'طي القائمة',
      language: 'اللغة', darkMode: 'الوضع الداكن', profile: 'الملف الشخصي', reportIssue: 'الإبلاغ عن مشكلة',
      signOut: 'تسجيل الخروج', administrator: 'مدير' },
    dashboard: { title: 'لوحة التحكم', subtitle: 'إليك ما يحدث عبر منصة المحاكاة.',
      activeStubs: 'القوالب النشطة', requests: 'الطلبات · ٢٤س', unmatched: 'غير مطابق · ٢٤س',
      matchTime: 'متوسط زمن المطابقة', thisWeek: 'هذا الأسبوع', stable: 'مستقر', verified: 'تم التحقق تفاضلياً' },
  } },
  zh: { translation: {
    brand: { name: 'Mockifyr', sub: 'Mock 平台' },
    nav: { overview: '概览', mocking: '模拟', platform: '平台',
      dashboard: '仪表板', stubs: '存根', journal: '请求日志', scenarios: '场景',
      recordings: '录制', extensions: '扩展', settings: '设置' },
    common: { search: '搜索…', expand: '展开侧栏', collapse: '收起侧栏',
      language: '语言', darkMode: '深色模式', profile: '个人资料', reportIssue: '报告问题',
      signOut: '退出登录', administrator: '管理员' },
    dashboard: { title: '仪表板', subtitle: '以下是您的模拟平台的动态。',
      activeStubs: '活动存根', requests: '请求 · 24小时', unmatched: '未匹配 · 24小时',
      matchTime: '平均匹配耗时', thisWeek: '本周', stable: '稳定', verified: '差分验证通过' },
  } },
  ja: { translation: {
    brand: { name: 'Mockifyr', sub: 'モックプラットフォーム' },
    nav: { overview: '概要', mocking: 'モック', platform: 'プラットフォーム',
      dashboard: 'ダッシュボード', stubs: 'スタブ', journal: 'リクエスト履歴', scenarios: 'シナリオ',
      recordings: 'レコーディング', extensions: '拡張機能', settings: '設定' },
    common: { search: '検索…', expand: 'サイドバーを展開', collapse: 'サイドバーを折りたたむ',
      language: '言語', darkMode: 'ダークモード', profile: 'プロフィール', reportIssue: '問題を報告',
      signOut: 'サインアウト', administrator: '管理者' },
    dashboard: { title: 'ダッシュボード', subtitle: 'モックプラットフォームの最新状況です。',
      activeStubs: '有効なスタブ', requests: 'リクエスト · 24h', unmatched: '未マッチ · 24h',
      matchTime: '平均マッチ時間', thisWeek: '今週', stable: '安定', verified: '差分検証済み' },
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
