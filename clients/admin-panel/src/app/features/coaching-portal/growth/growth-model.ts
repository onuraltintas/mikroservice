import { StudentExamOverviewItem, StudentExamSource, StudentExamSubject, StudentExamTrend } from '../../../core/services/coaching-student-exam-overview.service';
import { ESTIMATE_LABEL, EXACT_LABEL, P3_NOTE, ScoreEstimate, notComputedText, scoreGroup } from '../../../core/services/coaching-score-estimate.service';
import { possessive } from '../parent/parent-model';
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
/** V4: on the parent page the child's own entries carry the child's name instead of "Ben". */
function sourceName(source: StudentExamSource, childName?: string | null) { return source === 'Student' && childName ? childName : sourceLabels[source] ?? ''; }
const sourceTones: Record<StudentExamSource, CsTone> = { Student: 'brand', Teacher: 'info', Institution: 'accent' };
export function examSourceLabel(item: Pick<StudentExamOverviewItem, 'source'>, childName?: string | null): string { return sourceName(item.source, childName) || 'Diğer'; }
export function examSourceTone(source: StudentExamSource): CsTone { return sourceTones[source] ?? 'neutral'; }
/** "31 kişide 2." for an institution exam with a place; empty otherwise. */
export function examPlace(item: Pick<StudentExamOverviewItem, 'rank' | 'participants'>): string {
  return item.rank && item.participants ? `${whole.format(item.participants)} kişide ${whole.format(item.rank)}.` : '';
}

export interface TrendDot { x: number; y: number; net: number; date: string; source: StudentExamSource; label: string }
export interface TrendChart { width: number; height: number; line: string; area: string; dots: TrendDot[]; ticks: { y: number; value: number }[];
  change: number; summary: string; baseline: number; left: number; right: number }

/** Lays the trend points out evenly from left (oldest) to right; the y axis starts at 0 and ends at a round value. */
export function trendChart(trend: StudentExamTrend, width: number, height: number, childName?: string | null): TrendChart {
  const left = 34, right = width - 12, top = 12, bottom = height - 24;
  const points = trend.points;
  const highest = Math.max(10, ...points.map(x => x.net));
  const step = highest > 60 ? 20 : highest > 30 ? 10 : 5;
  const ceiling = Math.min(Math.max(trend.maxNet, step), Math.ceil((highest * 1.1) / step) * step);
  const lowest = Math.min(0, ...points.map(x => x.net));
  const y = (net: number) => bottom - ((net - lowest) / (ceiling - lowest || 1)) * (bottom - top);
  const x = (index: number) => points.length === 1 ? (left + right) / 2 : left + (index / (points.length - 1)) * (right - left);
  const dots = points.map((point, index) => ({ x: round(x(index)), y: round(y(point.net)), net: point.net, date: point.examDate, source: point.source,
    label: `${formatDay(point.examDate)}: ${formatNet(point.net)} net (${sourceName(point.source, childName)})` }));
  const line = dots.map((dot, index) => `${index ? 'L' : 'M'}${dot.x} ${dot.y}`).join(' ');
  const area = dots.length > 1 ? `${line} L${dots.at(-1)!.x} ${bottom} L${dots[0].x} ${bottom} Z` : '';
  const ticks = [];
  for (let value = 0; value <= ceiling; value += step) ticks.push({ y: round(y(value)), value });
  const change = points.length ? points.at(-1)!.net - points[0].net : 0;
  const summary = childName ? (points.length === 0 ? 'Henüz net hesaplanan deneme yok.'
    : points.length === 1 ? `İlk denemesi: ${formatNet(points[0].net)} net.`
    : `${childName} ${points.length} denemede ${formatNet(points[0].net)} netten ${formatNet(points.at(-1)!.net)} nete ${change >= 0 ? 'çıktı' : 'indi'}.`)
    : points.length === 0 ? 'Henüz net hesaplanan denemen yok.'
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
/** Review 06.11.2026: `label` is set for approximate models with a range (LGS, MEB-AGS); `status` adds "borderline". */
export interface GoalGauge { targetName: string; scoreType: string; headline: string; rows: GaugeRow[]; reached: boolean; missingData: boolean;
  status: 'reached' | 'borderline' | 'below' | null; label: string | null }

/** Now → target from the score estimate. A rank is better when lower, so its share is target / current. */
export function goalGauge(estimate: ScoreEstimate, childName: string | null = null): GoalGauge | null {
  const target = estimate.target;
  if (!target) return null;
  const now = estimate.estimates.find(x => x.scoreType === target.scoreType) ?? null;
  const missingData = target.targetScore === null && target.targetRank === null;
  const rows: GaugeRow[] = [];
  // 01.11.2026: ÖSYM does not compute the target's score type (0,5 ham puan, 2026 YKS Kılavuzu Tablo 1D): say which tests.
  // Re-check 06.11.2026: approximate by exam group (LGS, MEB-AGS), so the label stays at full net where the margin is 0.
  const approximate = !!now && scoreGroup(now) !== 'YKS';
  const range = now ? `${decimal.format(now.scoreLow)}–${decimal.format(now.scoreHigh)}` : '';
  // A null status from the server means no gap can be computed; only older responses (no status) fall back to `reached`.
  const status = target.status !== undefined ? target.status : now ? (target.reached ? 'reached' : 'below') : null;
  const label = approximate ? now?.label ?? ESTIMATE_LABEL : null;
  if (target.notComputedReason)
    return { targetName: target.targetName, scoreType: target.scoreType, rows, reached: false, missingData: false, status: null, label: null,
      headline: notComputedText(estimate.notComputed?.find(x => x.scoreType === target.scoreType) ?? { notComputedReason: target.notComputedReason, failedConditions: [] }) };
  if (!missingData && now) {
    // Review 06.11.2026: an approximate score is shown with the same range as its estimate card.
    const current = approximate ? `${decimal.format(now.score)} (${range})` : decimal.format(now.score);
    if (target.targetScore !== null) rows.push({ label: 'Puan', current, target: decimal.format(target.targetScore),
      percent: share(now.score, target.targetScore), text: `Şu an ${current}, hedef ${decimal.format(target.targetScore)}` });
    if (target.targetRank !== null && now.rank !== null) rows.push({ label: 'Sıralama', current: whole.format(now.rank), target: whole.format(target.targetRank),
      percent: share(target.targetRank, now.rank), text: `Şu an ${whole.format(now.rank)}., hedef ${whole.format(target.targetRank)}.` });
  }
  const headline = missingData ? 'Bu hedefin taban puanı ya da sıralaması yok; farkı hesaplayamıyoruz.'
    : !now ? (childName ? 'Bu puan türünde deneme sonucu girildiğinde hedefe ne kadar kaldığı burada görünür.' : 'Bu puan türünde deneme gir; hedefe ne kadar kaldığını hesaplayalım.')
    : status === 'borderline' ? `Hedef, tahmini puan aralığının içinde (${range}): ${childName ? 'sınırda' : 'sınırdasın'}.`
    : target.reached ? (childName ? `${childName} için tahmini puan hedefe ulaşıyor.` : 'Tahmini puanın hedefe ulaşıyor. Bu çizgiyi koru.')
    : target.netGap !== null ? `Hedefe ${decimal.format(target.netGap)} net kaldı`
    : target.rankGap !== null ? `Hedef sıralamaya ${whole.format(target.rankGap)} kişi kaldı` : 'Hedefe olan farkı henüz hesaplayamıyoruz.';
  return { targetName: target.targetName, scoreType: target.scoreType, headline, rows, reached: target.reached, missingData, status, label };
}
export interface GoalScoreCard { scoreType: string; title: string; group: string; headline: string; label: string; exact: boolean; note: string | null }

/**
 * Paket B (05.11.2026): without a target school or programme (MEB-AGS, YDS, YÖKDİL, or before one is chosen) Hedefim shows the
 * score of the exam the student works towards: the estimate with its ± range, or the exact YDS / YÖKDİL practice score.
 */
export function goalScore(estimate: ScoreEstimate, childName: string | null = null): GoalScoreCard | null {
  const goal = estimate.goal;
  if (!goal || estimate.target) return null;
  const label = goal.label || (goal.exact ? EXACT_LABEL : ESTIMATE_LABEL);
  const note = goal.scoreType === 'P1' ? P3_NOTE : null;
  const card = (headline: string) => ({ scoreType: goal.scoreType, title: goal.name, group: goal.group, headline, label, exact: goal.exact, note });
  if (goal.notComputedReason)
    return card(notComputedText(estimate.notComputed?.find(x => x.scoreType === goal.scoreType) ?? { notComputedReason: goal.notComputedReason, failedConditions: [] }));
  // Review 06.11.2026: say why there is no score.
  if (goal.score === null && goal.reason === 'noModel') return card('Bu sınav için puan tahmini henüz hazır değil.');
  if (goal.score === null && goal.reason === 'missingExam' && goal.missingExams?.length) {
    const exams = goal.missingExams.join(' ve ');
    return card(childName ? `Bu puan için ${exams} denemesi de gerekiyor.` : `Bu puan için ${exams} denemesi de gir.`);
  }
  if (goal.score === null)
    return card(childName ? 'Deneme sonucu girildiğinde puanı burada görünür.' : 'Bu sınavda bir deneme gir; puanını burada gösterelim.');
  const score = decimal.format(goal.score);
  if (goal.exact) {
    const level = goal.level ? ` · Seviye ${goal.level}` : '';
    return card(childName ? `${possessive(childName)} deneme puanı ${score}${level}` : `Deneme puanın ${score}${level}`);
  }
  // The same range as the estimate card (practice spread and model ±).
  const range = goal.scoreLow !== null && goal.scoreHigh !== null ? ` (${decimal.format(goal.scoreLow)}–${decimal.format(goal.scoreHigh)})` : '';
  return card(childName ? `Tahmini puan: ${score}${range}` : `Tahmini puanın ${score}${range}`);
}

/** Paket C (#4): the "Bugün" goal card. */
export interface HomeGoalCard {
  state: 'none' | 'pending' | 'estimate' | 'reason';
  headline: string; targetName: string | null; detail: string | null; label: string | null; note: string | null;
  status: 'reached' | 'borderline' | 'below' | null;
}
export const HOME_LGS_NOTE = "LGS'de hem puan tahmini hem okulun taban puanı yaklaşıktır.";

/**
 * Paket C (#4, approved 05.11.2026): what the home goal card says. No target: "Hedefini belirle". A target without a practice
 * exam: its name and when the gap appears. With an estimate: the score gap ("Hedefine yaklaşık X puan kaldı", "Hedef puanını
 * geçtin", or borderline as in #112), the target's name, its taban and "Yaklaşık tahmin" (not for exact YDS / YÖKDİL). When no
 * score can be computed the card says why, with #112's reason texts. The net line is shown under it by the page.
 */
export function homeGoalCard(estimate: ScoreEstimate | null, childName: string | null = null): HomeGoalCard {
  const target = estimate?.target ?? null;
  if (!estimate || !target)
    return { state: 'none', headline: 'Hedefini belirle', targetName: null, detail: 'Okulunu, bölümünü ya da hedef puanını seç; ne kadar kaldığını burada gösterelim.',
      label: null, note: null, status: null };
  const exact = target.scoreType === 'YDS_EN' || target.scoreType.startsWith('YOKDIL_');
  const lgs = target.scoreType === 'LGS';
  const now = estimate.estimates.find(x => x.scoreType === target.scoreType) ?? null;
  const language = estimate.languageScores?.find(x => x.examCode === target.scoreType) ?? null;
  const kind = target.kind ?? 'program';
  const targetName = kind === 'score' ? `${target.targetName} hedef puanın` : target.targetName;
  const taban = target.targetScore === null ? null
    : kind === 'score' || target.ownTargetScore ? `Hedef puanın ${decimal.format(target.targetScore)}`
    : `Taban puanı ${decimal.format(target.targetScore)}${lgs ? ' (yaklaşık)' : ''}${target.scoreYear ? ` · ${target.scoreYear}` : ''}`;
  const note = lgs && kind === 'school' ? HOME_LGS_NOTE : null;
  const card = (state: HomeGoalCard['state'], headline: string, label: string | null = null, status: HomeGoalCard['status'] = null): HomeGoalCard =>
    ({ state, headline, targetName, detail: taban, label, note, status });

  if (target.notComputedReason)
    return card('reason', notComputedText(estimate.notComputed?.find(x => x.scoreType === target.scoreType) ?? { notComputedReason: target.notComputedReason, failedConditions: [] }));
  if (!now && !language) {
    const goal = estimate.goal;
    if (goal?.reason === 'noModel') return card('reason', 'Bu sınav için puan tahmini henüz hazır değil.');
    if (goal?.reason === 'missingExam' && goal.missingExams?.length) {
      const exams = goal.missingExams.join(' ve ');
      return card('reason', childName ? `Bu puan için ${exams} denemesi de gerekiyor.` : `Bu puan için ${exams} denemesi de gir.`);
    }
    return card('pending', childName ? 'İlk deneme girildiğinde hedefe ne kadar kaldığı burada görünür.' : 'İlk denemeni girince hedefe ne kadar kaldığını burada göreceksin.');
  }
  if (target.targetScore === null && target.targetRank === null)
    return card('reason', 'Bu hedefin taban puanı yok; farkı hesaplayamıyoruz.');
  const label = exact ? (language?.label ?? EXACT_LABEL) : (now?.label ?? ESTIMATE_LABEL);
  const status = target.status ?? (target.reached ? 'reached' : target.scoreGap !== null || target.rankGap !== null ? 'below' : null);
  const you = childName ? 'Hedefe' : 'Hedefine';
  if (status === 'reached') return card('estimate', childName ? 'Hedef puanı geçildi' : 'Hedef puanını geçtin', label, status);
  if (status === 'borderline' && now)
    return card('estimate', `Hedef, tahmini puan aralığının içinde (${decimal.format(now.scoreLow)}–${decimal.format(now.scoreHigh)}): ${childName ? 'sınırda' : 'sınırdasın'}.`, label, status);
  if (target.scoreGap !== null && target.scoreGap > 0)
    return card('estimate', `${you} ${exact ? '' : 'yaklaşık '}${decimal.format(target.scoreGap)} puan kaldı`, label, 'below');
  if (target.rankGap !== null && target.rankGap > 0)
    return card('estimate', `Hedef sıralamaya yaklaşık ${whole.format(target.rankGap)} kişi kaldı`, label, 'below');
  return card('estimate', 'Hedefe olan farkı henüz hesaplayamıyoruz.', label, null);
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
