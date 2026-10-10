import { DailyExercisesComponent } from './daily-exercises.component';
import { ExercisePlayerComponent } from '../exercises/universal-player/exercise-player.component';

describe('Repeated daily task identity', () => {
  it('carries daily order and program day when starting a task', () => {
    const instance = {
      staffTraining: false,
      progress: () => ({ currentWeek: 2, currentDay: 3 }),
      router: { navigate: jasmine.createSpy() }
    };
    const task = { exerciseId: 'exercise', order: 6, difficultyLevel: 1, isCompleted: false };
    DailyExercisesComponent.prototype.startExercise.call(instance as any, task as any);
    const query = instance.router.navigate.calls.mostRecent().args[1].queryParams;
    expect(query.slotOrder).toBe(6);
    expect(query.programDay).toBe(10);
  });

  it('keeps the slot identity in completion and retry requests', () => {
    const instance = {
      exercise: { id: 'exercise' }, sessionId: 'session', dailySlotOrder: 6, dailyProgramDay: 10,
      submitDailyProgress: jasmine.createSpy()
    };
    (ExercisePlayerComponent.prototype as any).completeDailyProgress.call(instance, { totalTime: 1000 }, {}, false);
    const request = instance.submitDailyProgress.calls.mostRecent().args[0];
    expect(request.slotOrder).toBe(6);
    expect(request.programDay).toBe(10);
  });
});
