/**
 * T2 (31.10.2026): the four student sections that group older pages under one menu item. Each hub shows its tabs as
 * links above the page; `match` lists further paths that belong to a tab (for example a fee detail under Ücretlerim).
 */
export interface StudentHubTab { label: string; path: string; exact?: boolean; match?: string[]; needsFees?: boolean }
export interface StudentHub { label: string; tabs: StudentHubTab[] }

const root = '/coaching-portal';

export const STUDENT_HUBS: Record<'plan' | 'growth' | 'settings' | 'payments', StudentHub> = {
  plan: { label: 'Planım', tabs: [
    { label: 'Planım', path: `${root}/plan`, exact: true, match: [`${root}/plan/preview`] },
    { label: 'Haftalık düzen', path: `${root}/plan/weekly`, match: [`${root}/plan/hours`] },
    { label: 'Plan sihirbazı', path: `${root}/plan/wizard` }
  ] },
  growth: { label: 'Gelişimim', tabs: [
    { label: 'Denemeler', path: `${root}/growth/exams` },
    { label: 'Hedefim', path: `${root}/growth/goals` },
    { label: 'Haftam', path: `${root}/growth/week` }
  ] },
  // T5 (31.10.2026): four sections; Çalışma tercihlerim keeps the hub's own address (the setup reminders link there).
  settings: { label: 'Profil ve ayarlar', tabs: [
    { label: 'Profilim', path: `${root}/account/settings/profile` },
    { label: 'Çalışma tercihlerim', path: `${root}/account/settings`, exact: true },
    { label: 'Bildirimler', path: `${root}/account/settings/notifications` },
    { label: 'Görünüm', path: `${root}/account/settings/theme` }
  ] },
  // T5: Ödemeler is one page (Abonelik durumu, Havale bildirimleri, Ücretler, Faturalar), so it has no tabs.
  payments: { label: 'Ödemeler', tabs: [] }
};
