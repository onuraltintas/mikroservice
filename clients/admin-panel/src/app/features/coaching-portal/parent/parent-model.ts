import { validCoachingId } from '../../../../../shared/coaching-notification-target';

/** V4 (01.11.2026): the two products a parent may follow, in the order the panel shows them. */
export type ParentProduct = 'Coaching' | 'SpeedReading';
export const PARENT_PRODUCTS: ParentProduct[] = ['Coaching', 'SpeedReading'];

/** One entry of `ParentLinkView.products` (Identity V1). */
export interface ParentLinkProduct { product: ParentProduct; studentHasAccess: boolean; visibleToParent: boolean }

/** `GET api/coaching/parents/me/children/{id}/week` (Coaching V3). */
export interface ParentCoachingWeek {
  weekStart: string; planned: number; completed: number;
  overdue: { title: string; date: string; kind: string }[];
  nextSession: { startsAt: string; title: string; teacherName: string } | null;
  /** G2 review (M3): estimated is true when the year has no exam date yet (shown as "tahmini"). */
  examCountdown: { name: string; date: string; daysLeft: number; source?: string; estimated?: boolean } | null;
  attention: { kind: string; text: string; link?: string | null }[];
}
/** `GET api/coaching/parents/me/children/{id}/plan?weekStart=` — the child's active plan in one week, for reading only (live test 16.11.2026, #49). */
export interface ParentPlanTask { date: string; startMinute: number | null; minutes: number; title: string; lesson: string | null; questions: number | null; completed: boolean; continuation: boolean }
export interface ParentPlanWeek { weekStart: string; today: string; hasActivePlan: boolean; tasks: ParentPlanTask[] }
export interface ParentPlanDay { date: string; label: string; isToday: boolean; tasks: ParentPlanTask[] }
const planDayName = new Intl.DateTimeFormat('tr-TR', { timeZone: 'UTC', weekday: 'long', day: 'numeric', month: 'long' });
/** The seven days of the week, each with its work in the order of the day; a day without work stays in the list. */
export function parentPlanDays(week: ParentPlanWeek): ParentPlanDay[] {
  const start = new Date(week.weekStart + 'T00:00:00Z');
  return Array.from({ length: 7 }, (_, index) => {
    const day = new Date(start.getTime() + index * 86_400_000);
    const date = day.toISOString().slice(0, 10);
    return { date, label: planDayName.format(day), isToday: date === week.today, tasks: week.tasks.filter(task => task.date.slice(0, 10) === date) };
  });
}
/** Canlı test (17.11.2026): a past day's unfinished study is late, not waiting. */
export function parentTaskState(task: { completed: boolean; date: string }, today: string): 'Tamamlandı' | 'Gecikti' | 'Bekliyor' {
  if (task.completed) return 'Tamamlandı';
  return task.date.slice(0, 10) < today ? 'Gecikti' : 'Bekliyor';
}
export function planClock(startMinute: number | null, minutes: number): string {
  if (startMinute === null) return `${minutes} dk`;
  const clock = (value: number) => `${String(Math.floor(value / 60)).padStart(2, '0')}:${String(value % 60).padStart(2, '0')}`;
  return `${clock(startMinute)}–${clock(startMinute + minutes)} · ${minutes} dk`;
}
export function shiftWeek(weekStart: string, weeks: number): string {
  return new Date(new Date(weekStart + 'T00:00:00Z').getTime() + weeks * 7 * 86_400_000).toISOString().slice(0, 10);
}
/** `GET api/coaching/parents/me/children/{id}/adherence?weeks=8` (Coaching V3). */
export interface ParentAdherenceWeek { weekStart: string; planned: number; completed: number; minutes: number }
/**
 * `GET api/coaching/parents/me/children/{id}/weekly-review` — proposed for Coaching V3: a read-only projection of the
 * student's latest weekly review (teacher notes and decisions). Until it exists the section stays hidden.
 */
export interface ParentWeeklyReview {
  weekStart: string;
  reviews: { note: string; reviewedAt: string }[];
  decisions: { title: string; note?: string | null; dueOn: string; status: string; isOverdue: boolean }[];
}
/** `GET api/speed-reading/parents/me/children/{id}/overview` (Speed reading V2). */
export interface ParentSpeedReadingOverview {
  studentId: string; lastActivityAt: string | null;
  program: { name: string; level: number | string; currentDay: number; totalDays: number; completedDays: number; completedTasks: number; totalTasks: number; status: string } | null;
  thisWeek: { minutes: number; activeDays: number; exercisesCompleted: number };
  weeks: { weekStart: string; minutes: number; activeDays: number }[];
  reading: { latestWpm: number | null; firstWpm: number | null; latestComprehension: number | null;
    trend: { date: string; wpm: number | null; comprehension: number | null }[] };
  /** null when the child has no XP and no badge yet; recentBadges holds at most 5. */
  gamification: { level: number; xp: number; streakDays: number; badgesEarned: number; recentBadges: { name: string; earnedAt: string }[] } | null;
  recentResults: { date: string; exerciseType: string; exerciseName: string; score: number | null; wpm: number | null; comprehension: number | null }[];
}

interface LinkLike { studentName: string; products?: ParentLinkProduct[] | null }

export const PRODUCT_LABELS: Record<ParentProduct, string> = { Coaching: 'Koçluk', SpeedReading: 'Hızlı okuma' };
const SLUGS: Record<ParentProduct, string> = { Coaching: 'kocluk', SpeedReading: 'hizli-okuma' };

/** Products the child uses and left visible. A link without the list (before Identity V1) keeps the old Koçluk view. */
export function visibleProducts(link: LinkLike): ParentProduct[] {
  if (!link.products) return ['Coaching'];
  return PARENT_PRODUCTS.filter(product => link.products!.some(x => x.product === product && x.studentHasAccess && x.visibleToParent));
}

/** Why a linked child shows nothing: they turned sharing off, or use neither product. */
export function hiddenProductsText(link: LinkLike): string {
  const name = firstName(link.studentName);
  return link.products?.some(x => x.studentHasAccess && !x.visibleToParent)
    ? `${name} ilerlemesini seninle paylaşmayı kapattı. İstersen bunu ${name} ile konuşabilirsin; paylaşımı kendi hesabından yeniden açabilir.`
    : `${name} şu an Koçluk ya da Hızlı Okuma kullanmıyor. Kullanmaya başladığında ilerlemesi burada görünecek.`;
}

export function productSlug(product: ParentProduct): string { return SLUGS[product]; }
export function productFromSlug(slug: string | null | undefined): ParentProduct | null {
  return PARENT_PRODUCTS.find(product => SLUGS[product] === slug) ?? null;
}

export function firstName(fullName: string | null | undefined): string {
  return fullName?.trim().split(/\s+/)[0] || 'Çocuğun';
}

const ROUND = 'ouöü'; const VOWELS = 'aeıioöuü';
/** "Elif'in", "Can'ın", "Ayşe'nin", "Umut'un", "Gül'ün": genitive by Turkish vowel harmony. */
export function possessive(name: string): string {
  const lower = name.toLocaleLowerCase('tr-TR');
  const last = [...lower].reverse().find(char => VOWELS.includes(char)) ?? 'e';
  const back = 'aıou'.includes(last);
  const vowel = ROUND.includes(last) ? (back ? 'u' : 'ü') : (back ? 'ı' : 'i');
  const buffer = VOWELS.includes(lower.at(-1) ?? '') ? 'n' : '';
  return `${name}'${buffer}${vowel}n`;
}

export function coachingStatusLine(week: Pick<ParentCoachingWeek, 'planned' | 'completed'>): string {
  return week.planned > 0 ? `Bu hafta ${week.completed}/${week.planned} görev` : 'Bu hafta planlı görev yok';
}
export function speedReadingStatusLine(view: Pick<ParentSpeedReadingOverview, 'program' | 'thisWeek'>): string {
  if (view.program?.status?.toLowerCase() === 'completed') return 'Program tamamlandı';
  if (view.program) return `Program ${view.program.currentDay}/${view.program.totalDays}. gün`;
  return view.thisWeek?.minutes > 0 ? `Bu hafta ${view.thisWeek.minutes} dk okuma` : 'Bu hafta henüz çalışmadı';
}

const NOTIFICATION_LABELS: Record<string, string> = {
  ParentWeeklySummary: 'Haftalık özet', CoachingMessage: 'Mesaj',
  ParentLinkRequested: 'Veli bağlantısı', ParentLinkApproved: 'Veli bağlantısı', ParentLinkRemoved: 'Veli bağlantısı', ParentLinkRevoked: 'Veli bağlantısı',
  SessionScheduled: 'Seans', SessionUpdated: 'Seans', SessionCancelled: 'Seans', SessionReminder: 'Seans',
  FeeOverdueReminder: 'Ödeme', SubscriptionExpiring: 'Abonelik', SubscriptionExpired: 'Abonelik'
};
export function parentNotificationLabel(type: string): string { return NOTIFICATION_LABELS[type] ?? 'Bildirim'; }

/** Where a parent notification leads. Unknown types and malformed ids lead nowhere — never guess. */
export function parentNotificationTarget(item: { type?: string | number; relatedEntityId?: string | null }): { path: string; label: string } | null {
  const type = String(item.type ?? ''); const id = item.relatedEntityId;
  if (type === 'ParentWeeklySummary') return validCoachingId(id) ? { path: `/coaching-portal/parent/children/${id}`, label: 'Özeti aç' } : null;
  if (type === 'CoachingMessage') return validCoachingId(id) ? { path: `/coaching-portal/messages/${id}`, label: 'Mesajı aç' } : null;
  if (type.startsWith('ParentLink')) return { path: '/coaching-portal/parent', label: 'Çocuklarım' };
  return null;
}

/** Monday dates as "26 Eki". */
const shortDay = new Intl.DateTimeFormat('tr-TR', { day: 'numeric', month: 'short', timeZone: 'UTC' });
export function shortDate(isoDay: string): string {
  const date = new Date(isoDay.length === 10 ? `${isoDay}T12:00:00Z` : isoDay);
  return Number.isFinite(date.getTime()) ? shortDay.format(date) : isoDay;
}
