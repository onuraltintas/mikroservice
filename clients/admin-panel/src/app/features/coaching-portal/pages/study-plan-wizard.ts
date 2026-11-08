import { isDevMode } from '@angular/core';
import type { WeeklyCourseRank, WeeklyLesson } from '../../../core/services/coaching-weekly-draft.service';
import { DEFAULT_MAX_DAY_MINUTES, WeeklySlot, Weekday, weekDays } from './weekly-calendar';

/**
 * Paket G #41: priority is the student's (Derslerim › Öncelik, 1 highest, 3 by default); examQuestions is the course's question
 * count in her exam (sent by the server, from the practice exam sections); topicCount stands in when that is not known.
 */
export interface StudyPlanWizardLesson { id: string; name: string; examCode: string | null; priority?: number; examQuestions?: number | null; topicCount?: number }
export interface StudyPlanWizardInput { days: Weekday[]; startMinute: number; minutesPerDay: 30 | 60 | 90 | 120 }

export function createStudyPlanProposal(input: StudyPlanWizardInput, lessons: StudyPlanWizardLesson[], existing: WeeklySlot[]): WeeklySlot[] {
  if (!input.days.length || !lessons.length) return [];
  if (!Number.isInteger(input.startMinute) || input.startMinute < 0 || input.startMinute % 30 !== 0
    || input.startMinute + input.minutesPerDay > 1440) return [];
  return createProposalFromWindows(input.days.map(day => ({ day, startMinute: input.startMinute, endMinute: input.startMinute + input.minutesPerDay })), lessons, existing);
}

/** A study window of the saved availability (Çalışma tercihlerim › Müsaitliğim); its day uses the same names as the week. */
export interface StudyPlanWindow { day: string; startMinute: number; endMinute: number }

/**
 * E6 #29: the weekly proposal is made from the saved availability, so the student gives her study times once.
 * The rules are the wizard's: 60-minute cards (30 when only half an hour is left) on the half hour, Monday first,
 * and saved cards are never moved. Paket G #41: the courses take the hours in the order of {@link rankLessons}.
 * Review of PR #129 (K5): a day is filled only until its saved and proposed cards reach `maxDayMinutes`.
 */
export function createProposalFromWindows(windows: StudyPlanWindow[], lessons: StudyPlanWizardLesson[], existing: WeeklySlot[],
  maxDayMinutes = DEFAULT_MAX_DAY_MINUTES): WeeklySlot[] {
  if (!windows.length || !lessons.length) return [];
  const order = weekDays.map(day => day.value as string);
  const ordered = windows.filter(window => order.includes(window.day))
    .map(window => ({ day: window.day as Weekday, startMinute: Math.max(0, Math.ceil(window.startMinute / 30) * 30), endMinute: Math.min(1440, window.endMinute) }))
    .sort((a, b) => order.indexOf(a.day) - order.indexOf(b.day) || a.startMinute - b.startMinute);
  const proposal: WeeklySlot[] = [];
  const orderedLessons = rankLessons(lessons, existing);
  let lessonIndex = 0;
  for (const { day, startMinute, endMinute } of ordered) {
    let minute = startMinute;
    while (minute + 30 <= endMinute) {
      const left = maxDayMinutes - [...existing, ...proposal].filter(slot => slot.day === day).reduce((sum, slot) => sum + slot.durationMinutes, 0);
      if (left < 30) break;
      if (overlaps(day, minute, 30, existing) || overlaps(day, minute, 30, proposal)) { minute += 30; continue; }
      const durationMinutes = left >= 60 && minute + 60 <= endMinute && !overlaps(day, minute, 60, existing) && !overlaps(day, minute, 60, proposal) ? 60 : 30;
      const lesson = orderedLessons[lessonIndex++ % orderedLessons.length];
      proposal.push({ id: crypto.randomUUID(), lessonId: lesson.id, lessonName: lesson.name,
        examCode: lesson.examCode, day, startMinute: minute, durationMinutes });
      minute += durationMinutes;
    }
  }
  return proposal;
}

function overlaps(day: Weekday, startMinute: number, durationMinutes: number, slots: WeeklySlot[]) {
  return slots.some(slot => slot.day === day && startMinute < slot.startMinute + slot.durationMinutes
    && startMinute + durationMinutes > slot.startMinute);
}

const DEFAULT_PRIORITY = 3;
const nameOrder = new Intl.Collator('tr', { sensitivity: 'base', numeric: true });

/**
 * Paket G #41 (live 07.11): the order in which the courses get the hours. First the student's priority (1 highest); among
 * the same priority the course with the least weekly time already saved (as before, so hours added later go to the courses
 * still without time); then the course's weight in the exam: courses with a known question count come first, by that
 * count; courses without one come after them, by their topic count (review of PR 126: decided per course, so one unknown
 * course does not push Türkçe down); then the name. Before, the catalog's alphabetical order decided, so with five hours
 * Biyoloji, Coğrafya, Din Kültürü and Felsefe came before Matematik and Türkçe.
 */
export function rankLessons<T extends StudyPlanWizardLesson>(lessons: T[], existing: WeeklySlot[]): T[] {
  const allocatedMinutes = new Map<string, number>();
  for (const slot of existing) {
    const id = slot.lessonId.toLowerCase();
    allocatedMinutes.set(id, (allocatedMinutes.get(id) ?? 0) + slot.durationMinutes);
  }
  const known = (lesson: T) => typeof lesson.examQuestions === 'number';
  const weight = (lesson: T) => known(lesson) ? lesson.examQuestions as number : lesson.topicCount ?? 0;
  const priority = (lesson: T) => Number.isInteger(lesson.priority) ? lesson.priority as number : DEFAULT_PRIORITY;
  return [...lessons].sort((a, b) => priority(a) - priority(b)
    || (allocatedMinutes.get(a.id.toLowerCase()) ?? 0) - (allocatedMinutes.get(b.id.toLowerCase()) ?? 0)
    || Number(known(b)) - Number(known(a)) || weight(b) - weight(a) || nameOrder.compare(a.name, b.name));
}

/**
 * Paket G #41: the chosen courses that have neither a saved card nor a proposed one, in the order the proposal used
 * (the whole ranking, then the leftovers).
 */
export function lessonsWithoutTime<T extends StudyPlanWizardLesson>(lessons: T[], existing: WeeklySlot[], proposal: WeeklySlot[]): T[] {
  const covered = new Set([...existing, ...proposal].map(slot => slot.lessonId.toLowerCase()));
  return rankLessons(lessons, existing).filter(lesson => !covered.has(lesson.id.toLowerCase()));
}

/** Paket G #41: the weekly draft page's courses with the student's priority and exam weight (from the server). */
export function rankedWizardLessons<L extends Pick<WeeklyLesson, 'id' | 'name' | 'examCode' | 'topicCount'>>(lessons: L[], courses: WeeklyCourseRank[] | null | undefined): StudyPlanWizardLesson[] {
  const ranks = new Map((courses ?? []).map(course => [course.lessonId.toLowerCase(), course]));
  return lessons.map(lesson => {
    const rank = ranks.get(lesson.id.toLowerCase());
    // Review of PR 126: a chosen course without a question count ranks after the weighted ones; say so while developing.
    if (rank && rank.examQuestions == null && isDevMode())
      console.warn(`Haftalık öneri: "${lesson.name}" (${lesson.id}) için sınav soru sayısı bilinmiyor; konu sayısıyla sıralanıyor.`);
    return { id: lesson.id, name: lesson.name, examCode: lesson.examCode, priority: rank?.priority ?? DEFAULT_PRIORITY,
      examQuestions: rank?.examQuestions ?? null, topicCount: lesson.topicCount };
  });
}

/** Paket G #41: the notice under the proposal; the courses are named, the student is told what she can do. */
export function noTimeNotice(lessons: StudyPlanWizardLesson[], forStaff = false) {
  if (!lessons.length) return '';
  const names = lessons.map(lesson => lesson.name).join(', ');
  return forStaff ? `Şu dersler için saat kalmadı: ${names}. Öğrencinin müsaitliğine saat eklenmeli ya da ders sayısı azaltılmalı.`
    : `Şu dersler için saat kalmadı: ${names}. Müsaitliğine saat ekle ya da ders sayısını azalt.`;
}
