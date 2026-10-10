import { DailyExercisesComponent } from './daily-exercises.component';
import { ExercisePlayerComponent } from '../exercises/universal-player/exercise-player.component';
import { fakeAsync, tick } from '@angular/core/testing';
import { of } from 'rxjs';

describe('Repeated daily task identity', () => {
  it('restores the same slot context from URL after a page reload', fakeAsync(() => {
    const instance = {
      route: {
        queryParams: of({ slotOrder: '6', programDay: '10', programProgressId: 'progress' }),
        snapshot: { paramMap: { get: () => 'exercise' } }
      },
      loadExercise: jasmine.createSpy()
    } as any;
    ExercisePlayerComponent.prototype.ngOnInit.call(instance);
    tick(100);
    expect(instance.dailySlotOrder).toBe(6);
    expect(instance.dailyProgramDay).toBe(10);
    expect(instance.dailyProgramProgressId).toBe('progress');
    expect(instance.loadExercise).toHaveBeenCalledWith('exercise');
  }));
  it('carries daily order and program day when starting a task', () => {
    const instance = {
      staffTraining: false,
      progress: () => ({ currentWeek: 2, currentDay: 3, programProgressId: 'progress' }),
      router: { navigate: jasmine.createSpy() }
    };
    const task = { exerciseId: 'exercise', order: 6, difficultyLevel: 1, isCompleted: false };
    DailyExercisesComponent.prototype.startExercise.call(instance as any, task as any);
    const query = instance.router.navigate.calls.mostRecent().args[1].queryParams;
    expect(query.slotOrder).toBe(6);
    expect(query.programDay).toBe(10);
    expect(query.programProgressId).toBe('progress');
  });

  it('keeps the slot identity in completion and retry requests', () => {
    const instance = {
      exercise: { id: 'exercise' }, sessionId: 'session', dailySlotOrder: 6, dailyProgramDay: 10,
      dailyProgramProgressId: 'progress',
      submitDailyProgress: jasmine.createSpy()
    };
    (ExercisePlayerComponent.prototype as any).completeDailyProgress.call(instance, { totalTime: 1000 }, {}, false);
    const request = instance.submitDailyProgress.calls.mostRecent().args[0];
    expect(request.slotOrder).toBe(6);
    expect(request.programDay).toBe(10);
    expect(request.programProgressId).toBe('progress');
  });
});
