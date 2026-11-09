import type { StudyDay, StudyWindow } from '../../../core/services/coaching-study-planning.service';
import type { StudyDayBudget, StudyRoutineAnswers, StudySchoolShift, StudyWeekendDays } from '../../../core/services/coaching-study-routine.service';
import { clock, weekDays } from './weekly-calendar';

/**
 * Paket H3/H4: the "günlük düzen" questions of the plan wizard. The bounds and sentences mirror the server's StudyRoutine
 * (Coaching.Application/StudyPlanning/StudyRoutine.cs) so the student rarely meets a server error; keep the two in step.
 */
export type RoutineQuestion = 'shift' | 'school' | 'morning' | 'after' | 'day' | 'dinner' | 'busy' | 'busyStart' | 'weekday'
  | 'weekend' | 'weekendHours' | 'weekendMinutes' | 'review';

export const NOON = 720, LUNCH_START = 780, LUNCH_END = 840, NO_SCHOOL_MORNING_END = 750, MINIMUM_WINDOW = 60;
const WEEKDAYS: StudyDay[] = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday'];
export const WEEKDAY_CHOICES = weekDays.filter(day => WEEKDAYS.includes(day.value as StudyDay)) as { value: StudyDay; label: string }[];

/** School times by shift: the allowed school start and a usual day (start, end, study after). */
export const SHIFT_RULES: Record<Exclude<StudySchoolShift, 'NoSchool'>, { from: number; to: number; text: string; start: number; end: number; after: number }> = {
  Morning: { from: 390, to: 540, text: 'Sabahçıysan okulun 06:30 ile 09:00 arasında başlamalı.', start: 450, end: 750, after: 840 },
  Afternoon: { from: 660, to: 840, text: 'Öğlenciysen okulun 11:00 ile 14:00 arasında başlamalı.', start: 780, end: 1080, after: 1140 },
  FullDay: { from: 420, to: 570, text: 'Tam gün okuyorsan okulun 07:00 ile 09:30 arasında başlamalı.', start: 510, end: 930, after: 990 }
};

export const SHIFT_CHOICES: { value: StudySchoolShift; label: string; hint: string }[] = [
  { value: 'Morning', label: 'Sabahçı', hint: 'Okul sabah başlar, öğleden sonra boşsun.' },
  { value: 'Afternoon', label: 'Öğlenci', hint: 'Okul öğleden sonra; sabahların boş.' },
  { value: 'FullDay', label: 'Tam gün', hint: 'Sabahtan akşamüstüne kadar okuldasın.' },
  { value: 'NoSchool', label: 'Okula gitmiyorum', hint: 'Mezunsun ya da açık öğretimdesin.' }
];
export const WEEKEND_CHOICES: { value: StudyWeekendDays; label: string }[] = [
  { value: 'Both', label: 'İki gün de' }, { value: 'Saturday', label: 'Yalnız cumartesi' }, { value: 'Sunday', label: 'Yalnız pazar' }, { value: 'None', label: 'Hayır, çalışmam' }];

/** Sensible first answers; a YKS student starts with three hours a weekday, others with two. */
export function defaultRoutineAnswers(yks: boolean): StudyRoutineAnswers {
  return { schoolShift: null, schoolStart: null, schoolEnd: null, afterSchoolStart: null, latestEnd: 1320, morningStudy: false, morningStart: null,
    dayStart: null, dinnerStart: 1140, dinnerEnd: 1200, busyDays: [], busyDaysOff: false, busyStart: 1200, weekdayMinutes: yks ? 180 : 120,
    weekend: 'Both', weekendStart: 600, weekendEnd: 1080, weekendMinutes: yks ? 240 : 180, examFocus: null, tempo: null };
}

/** A new school shift brings that shift's usual times (the old ones may be outside its bounds). */
export function withShift(answers: StudyRoutineAnswers, shift: StudySchoolShift): StudyRoutineAnswers {
  if (answers.schoolShift === shift) return answers;
  if (shift === 'NoSchool') return { ...answers, schoolShift: shift, schoolStart: null, schoolEnd: null, afterSchoolStart: null, morningStudy: false, morningStart: null, dayStart: 540 };
  const rule = SHIFT_RULES[shift];
  return { ...answers, schoolShift: shift, schoolStart: rule.start, schoolEnd: rule.end, afterSchoolStart: rule.after, dayStart: null,
    morningStudy: false, morningStart: shift === 'Afternoon' ? 540 : null };
}

/** The questions in order for these answers ("Okula gitmiyorum" is offered by the hours step to everyone but a middle-school student). */
export function routineQuestions(a: StudyRoutineAnswers): RoutineQuestion[] {
  const list: RoutineQuestion[] = ['shift'];
  if (a.schoolShift && a.schoolShift !== 'NoSchool') list.push('school');
  if (a.schoolShift === 'Afternoon') list.push('morning');
  list.push(a.schoolShift === 'NoSchool' ? 'day' : 'after', 'dinner', 'busy');
  if (a.busyDays.length) list.push('busyStart');
  list.push('weekday', 'weekend');
  if (a.weekend && a.weekend !== 'None') list.push('weekendHours', 'weekendMinutes');
  list.push('review');
  return list;
}

/** Half-hour steps from..to (inclusive). */
export function halfHours(from: number, to: number): number[] {
  const list: number[] = [];
  for (let m = Math.ceil(from / 30) * 30; m <= to; m += 30) list.push(m);
  return list;
}
export const time = (minute: number) => minute === 1440 ? '24:00' : clock(minute);
/** "3 saat", "1,5 saat". */
export function hoursText(minutes: number) { return `${(minutes / 60).toLocaleString('tr-TR')} saat`; }

/** The choices each time question offers, inside the server's bounds for the answers given so far. */
export function timeChoices(field: 'schoolStart' | 'schoolEnd' | 'afterSchoolStart' | 'latestEnd' | 'morningStart' | 'dayStart' | 'dinnerStart' | 'dinnerEnd'
  | 'busyStart' | 'weekendStart' | 'weekendEnd', a: StudyRoutineAnswers): number[] {
  const shift = a.schoolShift && a.schoolShift !== 'NoSchool' ? SHIFT_RULES[a.schoolShift] : null;
  switch (field) {
    case 'schoolStart': return shift ? halfHours(shift.from, shift.to) : [];
    case 'schoolEnd': return halfHours((a.schoolStart ?? 420) + 180, 1260);
    case 'afterSchoolStart': return halfHours((a.schoolEnd ?? 720) + (a.schoolShift === 'Afternoon' ? 60 : 30), 1380);
    case 'latestEnd': return halfHours(Math.max(1080, studyStart(a) + 60), 1440);
    case 'morningStart': return halfHours(360, Math.max(360, (a.schoolStart ?? 780) - 90 - MINIMUM_WINDOW));
    case 'dayStart': return halfHours(360, 720);
    case 'dinnerStart': return halfHours(1020, 1350);
    case 'dinnerEnd': return halfHours((a.dinnerStart ?? 1020) + 30, Math.min(1380, (a.dinnerStart ?? 1020) + 120));
    case 'busyStart': return halfHours(NOON, 1380);
    case 'weekendStart': return halfHours(420, 1380);
    case 'weekendEnd': return halfHours((a.weekendStart ?? 420) + 60, 1440);
  }
}
/** 1 to 8 hours (10 for a graduate) in half-hour steps. */
export function minuteChoices(limit: number) { return halfHours(60, limit); }

function studyStart(a: StudyRoutineAnswers) { return a.schoolShift === 'NoSchool' ? a.dayStart ?? 360 : a.afterSchoolStart ?? 900; }

/** The server's check of one question; null when the answer is fine. */
export function questionIssue(q: RoutineQuestion, a: StudyRoutineAnswers, limit: number): string | null {
  const half = (v: number | null) => v !== null && v >= 0 && v <= 1440 && v % 30 === 0;
  switch (q) {
    case 'shift': return a.schoolShift ? null : 'Okul düzenini seç.';
    case 'school': {
      const rule = a.schoolShift && a.schoolShift !== 'NoSchool' ? SHIFT_RULES[a.schoolShift] : null;
      if (!half(a.schoolStart)) return 'Okulun kaçta başladığını seç.';
      if (rule && (a.schoolStart! < rule.from || a.schoolStart! > rule.to)) return rule.text;
      if (!half(a.schoolEnd)) return 'Okulun kaçta bittiğini seç.';
      return a.schoolEnd! - a.schoolStart! < 180 ? 'Okulun çıkış saati, giriş saatinden en az 3 saat sonra olmalı.' : null;
    }
    case 'morning':
      if (!a.morningStudy) return null;
      if (!half(a.morningStart)) return 'Sabah kaçta çalışmaya başlayacağını seç.';
      if (a.morningStart! < 360) return 'Sabah başlama saatin 06:00 ya da daha geç olmalı.';
      return (a.schoolStart ?? 0) - 90 - a.morningStart! < MINIMUM_WINDOW ? 'Sabah çalışman okuldan 1,5 saat önce bitiyor; en az 1 saat çalışabilmen için daha erken başla.' : null;
    case 'after':
      if (!half(a.afterSchoolStart)) return 'Okuldan sonra kaçta çalışmaya başlayacağını seç.';
      if (a.schoolShift === 'Afternoon' && a.afterSchoolStart! < (a.schoolEnd ?? 0) + 60) return 'Öğlenciysen okul çıkışından en az 1 saat sonrasını seç.';
      if (a.afterSchoolStart! < (a.schoolEnd ?? 0) + 30) return 'Okuldan sonra çalışmaya, okulun bitişinden en az yarım saat sonra başlayabilirsin.';
      return latestIssue(a);
    case 'day':
      if (!half(a.dayStart)) return 'Güne kaçta başlayacağını seç.';
      if (a.dayStart! < 360 || a.dayStart! > 720) return 'Güne başlama saatin 06:00 ile 12:00 arasında olmalı.';
      return latestIssue(a);
    case 'dinner':
      if (!half(a.dinnerStart)) return 'Akşam yemeğinin kaçta başladığını seç.';
      if (!half(a.dinnerEnd)) return 'Akşam yemeğinin kaçta bittiğini seç.';
      if (a.dinnerStart! < 1020 || a.dinnerEnd! > 1380) return 'Akşam yemeğin 17:00 ile 23:00 arasında olmalı.';
      return a.dinnerEnd! - a.dinnerStart! < 30 || a.dinnerEnd! - a.dinnerStart! > 120 ? 'Akşam yemeğin en az yarım saat, en çok 2 saat sürmeli.' : null;
    case 'busy': return a.busyDays.some(day => !WEEKDAYS.includes(day)) ? 'Kurs, etüt ya da antrenman günlerini pazartesi ile cuma arasından seç.' : null;
    case 'busyStart': return a.busyDays.length && !a.busyDaysOff && !half(a.busyStart) ? 'Kurs olan günlerde kaçtan sonra çalışabileceğini seç.' : null;
    case 'weekday': return minutesIssue(a.weekdayMinutes, limit, 'Hafta içi günde toplam ne kadar çalışacağını seç.', 'Hafta içi günlük süren');
    case 'weekend': return a.weekend ? null : 'Hafta sonu hangi günler çalışacağını seç.';
    case 'weekendHours':
      if (!half(a.weekendStart)) return 'Hafta sonu kaçta çalışmaya başlayacağını seç.';
      if (a.weekendStart! < 420) return 'Hafta sonu başlama saatin 07:00 ya da daha geç olmalı.';
      if (!half(a.weekendEnd)) return 'Hafta sonu en geç kaça kadar çalışabileceğini seç.';
      if (a.weekendEnd! - a.weekendStart! < 60) return 'Hafta sonu bitiş saatin, başlama saatinden en az 1 saat sonra olmalı.';
      return routineWindows(a).windows.some(w => w.day === 'Saturday' || w.day === 'Sunday') ? null
        : 'Hafta sonu seçtiğin saatlerde öğle arası ve akşam yemeği dışında en az 1 saatlik bir aralık kalmıyor; hafta sonu saatlerini değiştir.';
    case 'weekendMinutes': return minutesIssue(a.weekendMinutes, limit, 'Hafta sonu günde toplam ne kadar çalışacağını seç.', 'Hafta sonu günlük süren');
    case 'review': return routineWindows(a).windows.length ? null
      : 'Bu cevaplarla en az 1 saatlik bir çalışma aralığı kalmıyor; başlama ya da bitiş saatini, yemek saatini veya kurs günlerini değiştir.';
  }
}
function latestIssue(a: StudyRoutineAnswers) {
  if (a.latestEnd === null || a.latestEnd % 30 !== 0) return 'En geç kaça kadar çalışabileceğini seç.';
  if (a.latestEnd < 1080) return 'En geç çalışma saatin 18:00 ile 24:00 arasında olmalı.';
  return a.latestEnd - studyStart(a) < 60 ? 'En geç çalışma saatin, çalışmaya başladığın saatten en az 1 saat sonra olmalı.' : null;
}
function minutesIssue(value: number | null, limit: number, missing: string, field: string) {
  if (value === null) return missing;
  if (value < 60 || value > 600 || value % 30 !== 0) return `${field} 1 ile 10 saat arasında, yarım saatlik adımlarla olmalı.`;
  return value > limit ? `${field} en çok ${limit / 60} saat olabilir.` : null;
}

/** The question a server sentence belongs to, so it is shown next to that question. */
export function questionForMessage(message: string, shift: StudySchoolShift | null = null): RoutineQuestion {
  const rules: [RegExp, RoutineQuestion][] = [
    [/Okul düzen/, 'shift'], [/Okuldan sonra|okul çıkışından/, 'after'], [/okulun .*başlamalı|Okulun/, 'school'], [/^Sabah/, 'morning'],
    // Review of PR 135 (S13): the latest end is asked with the day's start for a student who does not go to school.
    [/Güne/, 'day'], [/En geç/, shift === 'NoSchool' ? 'day' : 'after'], [/Akşam yemeğ/, 'dinner'], [/Kurs olan günlerde/, 'busyStart'], [/Kurs, etüt/, 'busy'],
    [/Hafta içi/, 'weekday'], [/Hafta sonu günde|Hafta sonu günlük/, 'weekendMinutes'], [/Hafta sonu hangi/, 'weekend'], [/Hafta sonu/, 'weekendHours']];
  return rules.find(([pattern]) => pattern.test(message))?.[1] ?? 'review';
}

/** The server's ToWindows: weekly windows of at least an hour without dinner and the weekend lunch, and the daily budgets. */
export function routineWindows(a: StudyRoutineAnswers): { windows: StudyWindow[]; budgets: StudyDayBudget[]; daysWithoutWindows: StudyDay[] } {
  const windows: StudyWindow[] = [], budgets: StudyDayBudget[] = [];
  const dinner: [number, number] | null = a.dinnerStart !== null && a.dinnerEnd !== null ? [a.dinnerStart, a.dinnerEnd] : null;
  const add = (day: StudyDay, [start, end]: [number, number]) => {
    const from = Math.max(0, start), to = Math.min(1440, end);
    if (to - from >= MINIMUM_WINDOW) windows.push({ day, startMinute: from, endMinute: to });
  };
  for (const day of WEEKDAYS) {
    const busy = a.busyDays.includes(day);
    if (busy && a.busyDaysOff) continue;
    if (a.weekdayMinutes !== null) budgets.push({ day, minutes: a.weekdayMinutes });
    for (const [start, end, morning] of weekdayPieces(a)) {
      let from = start;
      if (busy && a.busyStart !== null && !morning && end > NOON) from = Math.max(from, a.busyStart);
      for (const piece of subtract([from, end], dinner)) add(day, piece);
    }
  }
  if (a.weekend && a.weekend !== 'None' && a.weekendStart !== null && a.weekendEnd !== null) {
    const days: StudyDay[] = a.weekend === 'Saturday' ? ['Saturday'] : a.weekend === 'Sunday' ? ['Sunday'] : ['Saturday', 'Sunday'];
    for (const day of days) {
      if (a.weekendMinutes !== null) budgets.push({ day, minutes: a.weekendMinutes });
      for (const free of subtract([a.weekendStart, a.weekendEnd], [LUNCH_START, LUNCH_END]))
        for (const piece of subtract(free, dinner)) add(day, piece);
    }
  }
  const order = (day: StudyDay) => weekDays.findIndex(x => x.value === day);
  windows.sort((x, y) => order(x.day) - order(y.day) || x.startMinute - y.startMinute);
  return { windows, budgets, daysWithoutWindows: budgets.map(x => x.day).filter(day => !windows.some(w => w.day === day)) };
}
function weekdayPieces(a: StudyRoutineAnswers): [number, number, boolean][] {
  const latest = a.latestEnd;
  if (latest === null) return [];
  switch (a.schoolShift) {
    case 'Morning': case 'FullDay': return a.afterSchoolStart !== null ? [[a.afterSchoolStart, latest, false]] : [];
    case 'Afternoon': {
      if (a.schoolStart === null || a.schoolEnd === null || a.afterSchoolStart === null) return [];
      const pieces: [number, number, boolean][] = [];
      if (a.morningStudy && a.morningStart !== null) pieces.push([a.morningStart, a.schoolStart - 90, true]);
      pieces.push([Math.max(a.afterSchoolStart, a.schoolEnd + 60), latest, false]);
      return pieces;
    }
    case 'NoSchool': return a.dayStart !== null ? [[a.dayStart, NO_SCHOOL_MORNING_END, false], [LUNCH_END, latest, false]] : [];
    default: return [];
  }
}
function subtract([start, end]: [number, number], cut: [number, number] | null): [number, number][] {
  if (end <= start) return [];
  if (!cut || cut[1] <= start || cut[0] >= end) return [[start, end]];
  const pieces: [number, number][] = [];
  if (cut[0] > start) pieces.push([start, cut[0]]);
  if (cut[1] < end) pieces.push([cut[1], end]);
  return pieces;
}

/** "Pazartesi: 14:00–19:00, 20:00–22:00", one line per day that has windows, Monday first. */
export function windowLines(windows: StudyWindow[]): { day: string; text: string }[] {
  return weekDays.map(day => ({ day: day.label, list: windows.filter(w => w.day === day.value).sort((x, y) => x.startMinute - y.startMinute) }))
    .filter(x => x.list.length).map(x => ({ day: x.day, text: x.list.map(w => `${time(w.startMinute)}–${time(w.endMinute)}`).join(', ') }));
}

const dayLabel = (day: string) => weekDays.find(x => x.value === day)?.label ?? day;
export function joinTurkish(items: string[]) { return items.length > 1 ? `${items.slice(0, -1).join(', ')} ve ${items.at(-1)}` : items[0] ?? ''; }

/** A short summary of the answers; staff reads it in third person. */
export function routineSummary(a: StudyRoutineAnswers, staff = false): { label: string; text: string }[] {
  const lines: { label: string; text: string }[] = [];
  const shift = SHIFT_CHOICES.find(x => x.value === a.schoolShift)?.label ?? '';
  if (a.schoolShift === 'NoSchool') {
    lines.push({ label: 'Okul', text: staff ? 'Okula gitmiyor' : 'Okula gitmiyorsun' });
    if (a.dayStart !== null && a.latestEnd !== null) lines.push({ label: 'Çalışma saatleri', text: `${time(a.dayStart)}–${time(a.latestEnd)}` });
  } else if (a.schoolShift) {
    lines.push({ label: 'Okul', text: `${shift}, ${time(a.schoolStart ?? 0)}–${time(a.schoolEnd ?? 0)}` });
    if (a.morningStudy && a.morningStart !== null) lines.push({ label: 'Sabah', text: `${time(a.morningStart)}–${time((a.schoolStart ?? 0) - 90)}` });
    if (a.afterSchoolStart !== null && a.latestEnd !== null) lines.push({ label: 'Okuldan sonra', text: `${time(a.afterSchoolStart)}–${time(a.latestEnd)}` });
  }
  if (a.dinnerStart !== null && a.dinnerEnd !== null) lines.push({ label: 'Akşam yemeği', text: `${time(a.dinnerStart)}–${time(a.dinnerEnd)}` });
  if (a.busyDays.length) {
    const days = joinTurkish(a.busyDays.map(dayLabel));
    lines.push({ label: 'Kurs, etüt, antrenman', text: a.busyDaysOff ? `${days} (${staff ? 'o günler çalışmıyor' : 'o günler çalışmıyorsun'})`
      : `${days} (${time(a.busyStart ?? 0)} sonrası)` });
  }
  if (a.weekdayMinutes !== null) lines.push({ label: 'Hafta içi', text: `günde ${hoursText(a.weekdayMinutes)}` });
  if (a.weekend === 'None') lines.push({ label: 'Hafta sonu', text: staff ? 'çalışmıyor' : 'çalışmıyorsun' });
  else if (a.weekend) {
    const days = WEEKEND_CHOICES.find(x => x.value === a.weekend)!.label.toLocaleLowerCase('tr-TR');
    lines.push({ label: 'Hafta sonu', text: `${days}, ${time(a.weekendStart ?? 0)}–${time(a.weekendEnd ?? 0)}, günde ${hoursText(a.weekendMinutes ?? 0)}` });
  }
  return lines;
}
