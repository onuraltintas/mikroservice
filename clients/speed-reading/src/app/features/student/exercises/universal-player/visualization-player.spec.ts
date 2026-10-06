import { ExercisePlayerComponent } from './exercise-player.component';
import { createActionFailureState } from './exercise-action-queue';

describe('visualization result normalization', () => {
  it('keeps preview and withheld assessment answers unmeasured', () => {
    const player = Object.create(ExercisePlayerComponent.prototype) as any;
    player.engine = { engineType: 'visualization' }; player.questionAnswers = [];
    expect(player.isMeasuredClientResult({ score: 0, accuracy: 0,
      details: { measurementStatus: 'NotMeasured', answers: [{ isCorrect: null }] } })).toBeFalse();
  });
  it('allows a successful answer retry without clearing an earlier critical failure', async () => {
    const player = Object.create(ExercisePlayerComponent.prototype) as any;
    player.engine = { engineType: 'visualization' }; player.sessionId = 'owned';
    player.actionQueue = Promise.resolve(); player.actionFailureState = createActionFailureState();
    player.validateActionWithTransientRetry = async () => { throw new Error('Network timeout'); };
    await player.enqueueAction({ action: 'answer_question' }).catch(() => undefined);
    player.validateActionWithTransientRetry = async () => ({ isValid: true, isCorrect: true });
    await player.enqueueAction({ action: 'answer_question' });
    expect(player.actionFailureState.hasFailure).toBeFalse();
    player.actionFailureState.hasFailure = true;
    await player.enqueueAction({ action: 'answer_question' });
    expect(player.actionFailureState.hasFailure).toBeTrue();
  });
});
