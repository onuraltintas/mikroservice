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
  settings: { label: 'Profil ve ayarlar', tabs: [
    { label: 'Tercihlerim', path: `${root}/account/settings`, exact: true },
    { label: 'Profilim', path: `${root}/account/settings/profile` },
    { label: 'Ayarlar', path: `${root}/account/settings/general` },
    { label: 'Görünüm', path: `${root}/account/settings/theme` }
  ] },
  payments: { label: 'Ödemeler', tabs: [
    { label: 'Abonelik', path: `${root}/account/payments`, exact: true },
    { label: 'Ücretlerim', path: `${root}/account/payments/fees`, needsFees: true }
  ] }
};
