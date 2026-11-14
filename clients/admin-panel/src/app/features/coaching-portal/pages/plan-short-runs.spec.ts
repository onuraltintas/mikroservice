import { StudyTask } from '../../../core/services/coaching-study-planning.service';
import { planDayItems } from './plan-short-runs';

// Paket J (user decision 14.11.2026): a lesson's short topics are shown as one row; each stays its own task.
describe('planDayItems', () => {
  const task = (id: string, start: number | null, minutes: number, lesson: string | null = 'math', done = false, date = '2026-11-16'): StudyTask =>
    ({ id, plannedDate: date, title: id, plannedMinutes: minutes, plannedStartMinute: start, weeklyLessonId: lesson, lessonName: lesson === 'math' ? 'Matematik' : 'Fizik', isCompleted: done } as unknown as StudyTask);
  const shape = (tasks: StudyTask[]) => planDayItems(tasks).map(x => x.run ? `${x.run.lessonName}:${x.tasks.map(t => t.id).join('+')}=${x.run.minutes}/${x.run.done}` : x.task.id);

  it('groups a lesson\'s back-to-back short tasks up to an hour and keeps the order', () => {
    // 6+17+17+17 = 57; the next 9 would pass the hour and starts a new run with the following 9.
    expect(shape([task('a', 1044, 6), task('b', 1050, 17, 'math', true), task('c', 1067, 17), task('d', 1084, 17), task('e', 1101, 9), task('f', 1110, 9), task('long', 1119, 60)]))
      .toEqual(['Matematik:a+b+c+d=57/1', 'Matematik:e+f=18/0', 'long']);
  });
  it('leaves alone a single short task, a gap, another lesson, a long task and a task without a time', () => {
    expect(shape([task('a', 600, 10), task('gap', 620, 10), task('other', 630, 10, 'phys'), task('long', 640, 30), task('untimed', null, 10), task('free', 700, 10, null)]))
      .toEqual(['a', 'gap', 'other', 'long', 'untimed', 'free']);
  });
  it('never joins two days', () => {
    expect(shape([task('a', 600, 10), task('b', 610, 10, 'math', false, '2026-11-17')])).toEqual(['a', 'b']);
  });
});
