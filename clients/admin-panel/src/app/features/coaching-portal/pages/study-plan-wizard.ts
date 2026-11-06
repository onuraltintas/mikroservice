import { WeeklySlot, Weekday, weekDays } from './weekly-calendar';

export interface StudyPlanWizardLesson { id: string; name: string; examCode: string | null }
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
 * the lessons with the least weekly time first, and saved cards are never moved.
 */
export function createProposalFromWindows(windows: StudyPlanWindow[], lessons: StudyPlanWizardLesson[], existing: WeeklySlot[]): WeeklySlot[] {
  if (!windows.length || !lessons.length) return [];
  const order = weekDays.map(day => day.value as string);
  const ordered = windows.filter(window => order.includes(window.day))
    .map(window => ({ day: window.day as Weekday, startMinute: Math.max(0, Math.ceil(window.startMinute / 30) * 30), endMinute: Math.min(1440, window.endMinute) }))
    .sort((a, b) => order.indexOf(a.day) - order.indexOf(b.day) || a.startMinute - b.startMinute);
  const proposal: WeeklySlot[] = [];
  const allocatedMinutes = new Map<string, number>();
  for (const slot of existing) {
    const id = slot.lessonId.toLowerCase();
    allocatedMinutes.set(id, (allocatedMinutes.get(id) ?? 0) + slot.durationMinutes);
  }
  const orderedLessons = [...lessons].sort((a, b) =>
    (allocatedMinutes.get(a.id.toLowerCase()) ?? 0) - (allocatedMinutes.get(b.id.toLowerCase()) ?? 0));
  let lessonIndex = 0;
  for (const { day, startMinute, endMinute } of ordered) {
    let minute = startMinute;
    while (minute + 30 <= endMinute) {
      if (overlaps(day, minute, 30, existing) || overlaps(day, minute, 30, proposal)) { minute += 30; continue; }
      const durationMinutes = minute + 60 <= endMinute && !overlaps(day, minute, 60, existing) && !overlaps(day, minute, 60, proposal) ? 60 : 30;
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
