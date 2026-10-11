import { ExercisePlayerComponent } from './exercise-player.component';
import { createActionFailureState } from './exercise-action-queue';
import { of, Subject } from 'rxjs';

describe('visualization result normalization', () => {
  for (const scenes of [undefined, []]) {
    it(`applies custom duration after loading database scenes (inline: ${JSON.stringify(scenes)})`, () => {
      const player = Object.create(ExercisePlayerComponent.prototype) as any;
      const previousState = history.state;
      history.replaceState({ customPreview: { exerciseId: 'exercise', values: { sceneDurationSec: 17 } } }, '');
      try {
        player.exercise = { id: 'exercise' };
        player.parsedConfig = { engineType: 'visualization', engineConfig: { mode: 'static', scenes } };
        player.destroy$ = new Subject<void>();
        player.isPreviewSession = () => true;
        player.authService = { currentUserValue: { roles: ['Teacher'] } };
        player.exerciseService = { getPreviewVisualizationScenes: () => of([
          { id: 'scene', description: 'Yeni sahne', duration: 40, displayOrder: 0,
            questions: [{ id: 'question', questionText: 'Soru', options: ['Bir', 'İki'] }] }
        ]) };
        player.initializePreviewEngine = jasmine.createSpy('initializePreviewEngine');
        player.startPreviewSession();
        const loaded = player.parsedConfig.engineConfig.scenes[0];
        expect(loaded.sceneId).toBe('scene');
        expect(loaded.questions[0].questionId).toBe('question');
        expect(loaded.duration).toBe(17);
        expect(player.initializePreviewEngine).toHaveBeenCalled();
      } finally { history.replaceState(previousState, ''); }
    });
  }
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
