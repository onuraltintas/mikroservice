import { StudentExamOverviewItem, StudentExamSource, StudentExamSubject, StudentExamTrend } from '../../../core/services/coaching-student-exam-overview.service';
import { ScoreEstimate } from '../../../core/services/coaching-score-estimate.service';
import { CsTone } from '../ui';

/**
 * T6 (31.10.2026): the pure mapping behind Gelişimim — exam sources, the net trend chart, subject bars, the goal gauge,
 * weekly bars and badge progress. Kept free of Angular so the numbers can be tested on their own.
 */

const decimal = new Intl.NumberFormat('tr-TR', { maximumFractionDigits: 2 });
const whole = new Intl.NumberFormat('tr-TR', { maximumFractionDigits: 0 });
const shortDate = new Intl.DateTimeFormat('tr-TR', { day: 'numeric', month: 'short', timeZone: 'Europe/Istanbul' });
export const formatNet = (value: number) => decimal.format(value);
export const formatDay = (iso: string) => shortDate.format(new Date(iso));

const sourceLabels: Record<StudentExamSource, string> = { Student: 'Ben', Teacher: 'Öğretmen', Institution: 'Kurum' };
const sourceTones: Record<StudentExamSource, CsTone> = { Student: 'brand', Teacher: 'info', Institution: 'accent' };
export function examSourceLabel(item: Pick<StudentExamOverviewItem, 'source'>): string { return sourceLabels[item.source] ?? 'Diğer'; }
export function examSourceTone(source: StudentExamSource): CsTone { return sourceTones[source] ?? 'neutral'; }
/** "31 kişide 2." for an institution exam with a place; empty otherwise. */
export function examPlace(item: Pick<StudentExamOverviewItem, 'rank' | 'participants'>): string {
  return item.rank && item.participants ? `${whole.format(item.participants)} kişide ${whole.format(item.rank)}.` : '';
}

export interface TrendDot { x: number; y: number; net: number; date: string; source: StudentExamSource; label: string }
export interface TrendChart { width: number; height: number; line: string; area: string; dots: TrendDot[]; ticks: { y: number; value: number }[];
  change: number; summary: string; baseline: number; left: number; right: number }

/** Lays the trend points out evenly from left (oldest) to right; the y axis starts at 0 and ends at a round value. */
export function trendChart(trend: StudentExamTrend, width: number, height: number): TrendChart {
  const left = 34, right = width - 12, top = 12, bottom = height - 24;
  const points = trend.points;
  const highest = Math.max(10, ...points.map(x => x.net));
  const step = highest > 60 ? 20 : highest > 30 ? 10 : 5;
  const ceiling = Math.min(Math.max(trend.maxNet, step), Math.ceil((highest * 1.1) / step) * step);
  const lowest = Math.min(0, ...points.map(x => x.net));
  const y = (net: number) => bottom - ((net - lowest) / (ceiling - lowest || 1)) * (bottom - top);
  const x = (index: number) => points.length === 1 ? (left + right) / 2 : left + (index / (points.length - 1)) * (right - left);
  const dots = points.map((point, index) => ({ x: round(x(index)), y: round(y(point.net)), net: point.net, date: point.examDate, source: point.source,
    label: `${formatDay(point.examDate)}: ${formatNet(point.net)} net (${sourceLabels[point.source] ?? ''})` }));
  const line = dots.map((dot, index) => `${index ? 'L' : 'M'}${dot.x} ${dot.y}`).join(' ');
  const area = dots.length > 1 ? `${line} L${dots.at(-1)!.x} ${bottom} L${dots[0].x} ${bottom} Z` : '';
  const ticks = [];
  for (let value = 0; value <= ceiling; value += step) ticks.push({ y: round(y(value)), value });
  const change = points.length ? points.at(-1)!.net - points[0].net : 0;
  const summary = points.length === 0 ? 'Henüz net hesaplanan denemen yok.'
    : points.length === 1 ? `İlk denemen: ${formatNet(points[0].net)} net. Bir deneme daha gir, gidişatını görelim.`
    : `${points.length} denemede ${formatNet(points[0].net)} netten ${formatNet(points.at(-1)!.net)} nete ${change >= 0 ? 'çıktın' : 'indin'}.`;
  return { width, height, line, area, dots, ticks, change, summary, baseline: bottom, left, right };
}

const lessonTones: Record<string, number> = { TUR: 0, EDB: 0, MAT: 1, FEN: 2, FIZ: 2, KIM: 3, BIO: 4, TAR: 5, TAR1: 5, TAR2: 5, INK: 5,
  COG: 6, COG1: 6, COG2: 6, DIN: 7, FEL: 7, ENG: 3 };
/** Index into the lesson palette (--coaching-lesson-0…7): fixed for known sections, stable for others. */
export function subjectTone(sectionCode: string): number {
  if (sectionCode in lessonTones) return lessonTones[sectionCode];
  return [...sectionCode].reduce((sum, char) => sum + char.charCodeAt(0), 0) % 8;
}

export interface SubjectBar { code: string; name: string; averageNet: string; questionCount: number; percent: number; tone: number; weakest: boolean }
export function subjectBars(subjects: StudentExamSubject[], examCode: string): SubjectBar[] {
  return subjects.filter(x => x.examCode === examCode).map(x => ({ code: x.sectionCode, name: x.name, averageNet: formatNet(x.averageNet),
    questionCount: x.questionCount, percent: Math.round(Math.max(0, Math.min(1, x.successRatio)) * 100), tone: subjectTone(x.sectionCode), weakest: x.weakest }));
}

export interface GaugeRow { label: string; current: string; target: string; percent: number; text: string }
export interface GoalGauge { targetName: string; scoreType: string; headline: string; rows: GaugeRow[]; reached: boolean; missingData: boolean }

/** Now → target from the score estimate. A rank is better when lower, so its share is target / current. */
export function goalGauge(estimate: ScoreEstimate): GoalGauge | null {
  const target = estimate.target;
  if (!target) return null;
  const now = estimate.estimates.find(x => x.scoreType === target.scoreType) ?? null;
  const missingData = target.targetScore === null && target.targetRank === null;
  const rows: GaugeRow[] = [];
  if (!missingData && now) {
    if (target.targetScore !== null) rows.push({ label: 'Puan', current: decimal.format(now.score), target: decimal.format(target.targetScore),
      percent: share(now.score, target.targetScore), text: `Şu an ${decimal.format(now.score)}, hedef ${decimal.format(target.targetScore)}` });
    if (target.targetRank !== null) rows.push({ label: 'Sıralama', current: whole.format(now.rank), target: whole.format(target.targetRank),
      percent: share(target.targetRank, now.rank), text: `Şu an ${whole.format(now.rank)}., hedef ${whole.format(target.targetRank)}.` });
  }
  const headline = missingData ? 'Bu hedefin taban puanı ya da sıralaması yok; farkı hesaplayamıyoruz.'
    : !now ? 'Bu puan türünde deneme gir; hedefe ne kadar kaldığını hesaplayalım.'
    : target.reached ? 'Tahmini puanın hedefe ulaşıyor. Bu çizgiyi koru.'
    : target.netGap !== null ? `Hedefe ${decimal.format(target.netGap)} net kaldı`
    : target.rankGap !== null ? `Hedef sıralamaya ${whole.format(target.rankGap)} kişi kaldı` : 'Hedefe olan farkı henüz hesaplayamıyoruz.';
  return { targetName: target.targetName, scoreType: target.scoreType, headline, rows, reached: target.reached, missingData };
}
function share(value: number, goal: number) { return goal > 0 && value > 0 ? Math.round(Math.min(1, value / goal) * 100) : 0; }

export interface WeekRange { from: string; to: string; label: string }
/** The last `count` Monday–Sunday weeks ending with the week of `today` (YYYY-MM-DD, Türkiye date). */
export function weekRanges(today: string, count: number): WeekRange[] {
  const date = new Date(`${today}T00:00:00Z`);
  const monday = new Date(date.getTime() - ((date.getUTCDay() + 6) % 7) * 86_400_000);
  const day = (offset: number) => new Date(monday.getTime() + offset * 86_400_000).toISOString().slice(0, 10);
  return Array.from({ length: count }, (_, index) => {
    const back = (count - 1 - index) * 7;
    const from = day(-back);
    return { from, to: day(-back + 6), label: back === 0 ? 'Bu hafta' : shortDate.format(new Date(`${from}T12:00:00Z`)) };
  });
}

export interface WeekInput { label: string; minutes: number; tasks: number; scheduled?: number }
export interface WeekBar extends WeekInput { minutesPercent: number; minutesText: string }
export function weekBars(weeks: WeekInput[]): WeekBar[] {
  const most = Math.max(0, ...weeks.map(x => x.minutes));
  return weeks.map(week => ({ ...week, minutesPercent: most ? Math.round((week.minutes / most) * 100) : 0, minutesText: hours(week.minutes) }));
}
export function hours(minutes: number): string {
  if (minutes <= 0) return '0 sa';
  if (minutes < 60) return `${minutes} dk`;
  return `${decimal.format(Math.round((minutes / 60) * 10) / 10)} sa`;
}

export interface BadgeInput { code: string; label: string; description?: string; earnedOn: string | null; progress?: number; target?: number }
export interface BadgeProgress { code: string; label: string; earned: boolean; progress: number; target: number; text: string }
/** Unearned badges first, closest to done first; earned ones after. */
export function badgeProgress(badges: BadgeInput[]): BadgeProgress[] {
  return badges.map(badge => {
    const target = Math.max(1, badge.target ?? 1), earned = !!badge.earnedOn;
    const progress = earned ? target : Math.min(target, Math.max(0, badge.progress ?? 0));
    return { code: badge.code, label: badge.label, earned, progress, target, text: earned ? `${badge.label}: kazandın` : `${badge.label}: ${progress}/${target}` };
  }).sort((a, b) => Number(a.earned) - Number(b.earned) || b.progress / b.target - a.progress / a.target);
}

function round(value: number) { return Math.round(value * 10) / 10; }
