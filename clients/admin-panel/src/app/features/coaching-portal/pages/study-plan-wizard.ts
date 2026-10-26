import { WeeklySlot, Weekday, weekDays } from './weekly-calendar';

export interface StudyPlanWizardLesson { id: string; name: string; examCode: string | null }
export interface StudyPlanWizardInput { days: Weekday[]; startMinute: number; minutesPerDay: 30 | 60 | 90 | 120 }

export function createStudyPlanProposal(input: StudyPlanWizardInput, lessons: StudyPlanWizardLesson[], existing: WeeklySlot[]): WeeklySlot[] {
  if (!input.days.length || !lessons.length) return [];
  if (!Number.isInteger(input.startMinute) || input.startMinute < 0 || input.startMinute % 30 !== 0
    || input.startMinute + input.minutesPerDay > 1440) return [];

  const proposal: WeeklySlot[] = [];
  const orderedDays = weekDays.map(day => day.value).filter(day => input.days.includes(day));
  const allocatedMinutes = new Map<string, number>();
  for (const slot of existing) {
    const id = slot.lessonId.toLowerCase();
    allocatedMinutes.set(id, (allocatedMinutes.get(id) ?? 0) + slot.durationMinutes);
  }
  const orderedLessons = [...lessons].sort((a, b) =>
    (allocatedMinutes.get(a.id.toLowerCase()) ?? 0) - (allocatedMinutes.get(b.id.toLowerCase()) ?? 0));
  let lessonIndex = 0;
  for (const day of orderedDays) {
    const endMinute = input.startMinute + input.minutesPerDay;
    let minute = input.startMinute;
    while (minute + 30 <= endMinute) {
      if (overlaps(day, minute, 30, existing) || overlaps(day, minute, 30, proposal)) { minute += 30; continue; }
      const durationMinutes = minute + 60 <= endMinute && !overlaps(day, minute, 60, existing) ? 60 : 30;
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
