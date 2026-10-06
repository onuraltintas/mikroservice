import { ExercisePlayerComponent } from './exercise-player.component';
import { createActionFailureState } from './exercise-action-queue';

describe('subvocalization answer validation', () => {
  function player(): any {
    const value = Object.create(ExercisePlayerComponent.prototype);
    value.engineState = { isRunning: true, isPaused: false };
    value.engine = { engineType: 'subvocalization_reduction', getCurrentQuestion: () => ({ id: 'q1' }),
      getPhase: () => 'answering', handleInput: jasmine.createSpy('input') };
    value.sessionId = 'session'; value.questionAnswers = [];
    value.questionFeedback = null; value.questionSubmissionPending = false;
    value.cdr = { detectChanges: () => undefined }; value.showToast = jasmine.createSpy('toast');
    return value;
  }

  it('waits for validation and blocks repeated clicks', async () => {
    const value = player();
    let apply: any, release: any;
    value.enqueueAction = jasmine.createSpy('queue').and.callFake((_action: any, callback: any) => {
      apply = callback; return new Promise(resolve => release = resolve);
    });
    value.submitSubvocAnswer('A'); value.submitSubvocAnswer('A');
    expect(value.engine.handleInput).not.toHaveBeenCalled();
    expect(value.enqueueAction).toHaveBeenCalledTimes(1);
    apply({ isValid: true, isCorrect: true, correctAnswer: 'A' }); release();
    await Promise.resolve();
    expect(value.engine.handleInput).toHaveBeenCalledWith(jasmine.objectContaining({ serverValidated: true, isCorrect: true }));
    expect(value.questionAnswers[0].questionId).toBe('q1');
  });

  it('keeps failures retryable and assessment answers hidden', async () => {
    const value = player(); value.isAssessmentMode = true;
    value.enqueueAction = (_action: any, apply: any) => Promise.resolve().then(() => apply({ isValid: true }));
    value.submitSubvocAnswer('A');
    await Promise.resolve(); await Promise.resolve(); await Promise.resolve();
    expect(value.engine.handleInput).not.toHaveBeenCalled();
    expect(value.questionSubmissionPending).toBeFalse();
    value.enqueueAction = (_action: any, apply: any) => Promise.resolve().then(() => apply({ isValid: true, isCorrect: true, correctAnswer: 'A' }));
    value.submitSubvocAnswer('A'); await Promise.resolve(); await Promise.resolve();
    expect(value.questionFeedback.isCorrect).toBeNull();
    expect(value.questionFeedback.correctAnswer).toBeUndefined();
  });

  it('does not promote preview answers into measured results', () => {
    const value = player(); value.sessionId = 'preview-mode';
    value.submitSubvocAnswer('A');
    expect(value.engine.handleInput).toHaveBeenCalledWith(jasmine.objectContaining({ previewOnly: true }));
    expect(value.isMeasuredClientResult({ details: { measurementStatus: 'NotMeasured', comprehensionScore: null, totalQuestions: 1 } })).toBeFalse();
  });

  it('allows completion after a rejected answer is successfully retried', async () => {
    const value = player();
    value.actionQueue = Promise.resolve(); value.actionFailureState = createActionFailureState();
    let attempts = 0;
    value.validateActionWithTransientRetry = async () => ++attempts === 1
      ? { isValid: false, message: 'Retry' } : { isValid: true, isCorrect: true };
    value.submitSubvocAnswer('A');
    await value.actionQueue.catch(() => undefined);
    await Promise.resolve();
    value.submitSubvocAnswer('A');
    await value.actionQueue;
    expect(value.actionFailureState.hasFailure).toBeFalse();
  });
});
