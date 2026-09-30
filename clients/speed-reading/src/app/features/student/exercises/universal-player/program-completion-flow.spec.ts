import { throwError } from 'rxjs';
import { ExercisePlayerComponent } from './exercise-player.component';

describe('program completion flow', () => {
  function player() {
    return {
      exercise: { id: 'exercise' }, sessionId: 'session',
      exerciseProgramService: { completeExercise: jasmine.createSpy().and.returnValue(throwError(() => new Error('offline'))) },
      cdr: { detectChanges: jasmine.createSpy() }, showToast: jasmine.createSpy(),
      dailyProgressSaveStatus: 'idle', pendingDailyProgressRequest: null,
      result: { totalTime: 1000 }, router: { navigate: jasmine.createSpy() }
    };
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
});
