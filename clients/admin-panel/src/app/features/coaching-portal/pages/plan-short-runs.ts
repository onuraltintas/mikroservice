import { StudyTask } from '../../../core/services/coaching-study-planning.service';

/**
 * Paket J (user decision 14.11.2026, "yalnız görünümde grupla"): a lesson's short topics follow each other as rows of 6-17 minutes.
 * Each stays its own task (the reports, the practice advice and the outcome credits read a task's one topic), but the plan shows
 * such a run as one row that opens to its tasks. A run: the same lesson and day, each task at most 20 minutes, no gap between them,
 * at least two tasks and at most an hour together. The order of the day is kept.
 */
export const SHORT_TASK_MINUTES = 20;
export const SHORT_RUN_MINUTES = 60;
export type PlanDayItem = { run: null; task: StudyTask } | { run: { lessonName: string; minutes: number; done: number; startMinute: number }; tasks: StudyTask[] };

export function planDayItems(tasks: StudyTask[]): PlanDayItem[] {
  const items: PlanDayItem[] = []; let open: StudyTask[] = [];
  const close = () => {
    if (open.length > 1) items.push({ run: { lessonName: open[0].lessonName ?? '', minutes: open.reduce((sum, x) => sum + x.plannedMinutes, 0),
      done: open.filter(x => x.isCompleted).length, startMinute: open[0].plannedStartMinute! }, tasks: open });
    else if (open.length) items.push({ run: null, task: open[0] });
    open = [];
  };
  for (const task of tasks) {
    const short = task.plannedMinutes <= SHORT_TASK_MINUTES && task.plannedStartMinute != null && !!task.weeklyLessonId;
    const last = open[open.length - 1];
    const follows = !!last && short && last.plannedDate === task.plannedDate && last.weeklyLessonId === task.weeklyLessonId
      && last.plannedStartMinute! + last.plannedMinutes === task.plannedStartMinute
      && open.reduce((sum, x) => sum + x.plannedMinutes, 0) + task.plannedMinutes <= SHORT_RUN_MINUTES;
    if (!follows) close();
    if (short) open.push(task); else items.push({ run: null, task });
  }
  close();
  return items;
}
