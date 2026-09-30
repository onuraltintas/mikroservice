import { of, throwError } from 'rxjs';
import { ExercisePlayerComponent } from './exercise-player.component';

describe('program completion flow', () => {
  function player() {
    const instance = {
      exercise: { id: 'exercise' }, sessionId: 'session',
      exerciseProgramService: { completeExercise: jasmine.createSpy().and.returnValue(throwError(() => new Error('offline'))) },
      cdr: { detectChanges: jasmine.createSpy() }, showToast: jasmine.createSpy(),
      dailyProgressSaveStatus: 'idle', pendingDailyProgressRequest: null,
      result: { totalTime: 1000 }, router: { navigate: jasmine.createSpy() }
    };
    (instance as any).submitDailyProgress = (request: any) =>
      (ExercisePlayerComponent.prototype as any).submitDailyProgress.call(instance, request);
    return instance;
  }

  it('keeps a failed daily update retryable without repeating session completion', () => {
    const instance = player();
    (ExercisePlayerComponent.prototype as any).completeDailyProgress.call(instance, instance.result, {}, false);
    expect(instance.dailyProgressSaveStatus).toBe('failed');
    expect(instance.pendingDailyProgressRequest).not.toBeNull();
    expect(instance.showToast).toHaveBeenCalled();
  });

  it('opens the post-training assessment from the completion summary', () => {
    const instance = player();
    (ExercisePlayerComponent.prototype as any).startPostTrainingAssessment.call(instance);
    expect(instance.router.navigate).toHaveBeenCalledWith(['/student/assessment'], { queryParams: { phase: 2 } });
  });

  it('retries the same completed session with the same idempotency key', () => {
    const instance = player();
    (ExercisePlayerComponent.prototype as any).completeDailyProgress.call(instance, instance.result, {}, false);
    instance.exerciseProgramService.completeExercise.and.returnValue(of({}));
    (ExercisePlayerComponent.prototype as any).retryDailyProgress.call(instance);
    expect(instance.exerciseProgramService.completeExercise).toHaveBeenCalledTimes(2);
    expect(instance.exerciseProgramService.completeExercise.calls.argsFor(0)[1]).toBe('session');
    expect(instance.exerciseProgramService.completeExercise.calls.argsFor(1)[1]).toBe('session');
    expect(instance.dailyProgressSaveStatus).toBe('saved');
    expect(instance.pendingDailyProgressRequest).toBeNull();
  });
});
