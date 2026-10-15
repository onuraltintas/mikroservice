import { ExercisePlayerComponent } from './exercise-player.component';
import { of } from 'rxjs';
import { fakeAsync, tick } from '@angular/core/testing';
import { VisualExpansionEngine } from './engines/visual-expansion.engine';

describe('visual expansion answer UI', () => {
  for (const serverAuthoritative of [false, true]) {
    it(`counts training answers from the first round despite catalog assessment metadata (server=${serverAuthoritative})`, fakeAsync(() => {
      const engine = new VisualExpansionEngine();
      const actions: any[] = [];
      const value = Object.create(ExercisePlayerComponent.prototype) as any;
      value.engine = engine;
      engine.initialize({ serverAuthoritative, isAssessmentMode: false, rounds: 4,
        startDegrees: 8, targetDegrees: 16, timing: { durationMs: 400, intervalMs: 1 },
        assessmentProtocol: { version: 2, practiceRounds: 3, roundsPerBlock: 6, blockValues: [8, 12, 16] }
      } as any, { onStart() {}, onPause() {}, onResume() {}, onError() {}, onComplete() {},
        onStateChange() {}, onStepComplete() {}, onAction: action => actions.push(action) });
      engine.start();
      expect(engine.state.totalSteps).toBe(4);
      for (let round = 0; round < 4; round++) {
        tick(1);
        if (serverAuthoritative) engine.reconcileServerResponse(actions.at(-1), {
          isValid: true, feedbackData: { stimuli: ['A', 'B'], degrees: 8, displayDurationMs: 400 }
        });
        const answers = engine.getCurrentStimuli().map(item => item.content);
        tick(engine.getCurrentDisplayDurationMs());
        engine.handleInput({ answers: round % 2 === 0 ? answers : ['?', '?'] });
        if (serverAuthoritative) engine.reconcileServerResponse(actions.at(-1), {
          isValid: true, isCorrect: round % 2 === 0, nextStep: round + 1
        });
        expect(value.getExpansionCorrectCount()).toBe(Math.ceil((round + 1) / 2));
        expect(value.getExpansionWrongCount()).toBe(Math.floor((round + 1) / 2));
      }
      engine.destroy();
    }));
  }
  function player(stimuli: string[]) {
    const value = Object.create(ExercisePlayerComponent.prototype) as any;
    value.authService = { canPreviewExercises: () => false, hasRole: (role: string) => role === 'Student' };
    value.expansionAnswers = [];
    value.engine = { engineType: 'visual_expansion', getLastShownStimuli: () => stimuli,
      getExpectedAnswerCount: () => stimuli.length, getPattern: () => 'radial',
      isWaitingForInput: true, state: { isPaused: false }, handleInput: jasmine.createSpy('input') };
    value.cdr = { detectChanges: () => undefined };
    return value;
  }
  it('accepts four complete words in the displayed radial order', () => {
    const value = player(['GÜN', 'SU', 'YOL', 'EV']);
    expect(value.getExpansionAnswerSlots()).toEqual([0, 1, 2, 3]);
    expect(value.getExpansionAnswerMaxLength(0)).toBe(3);
    expect(value.getExpansionAnswerLabel(2)).toBe('Alt sol');
    value.expansionAnswers = ['GÜN', 'SU', 'YOL', 'EV'];
    value.submitExpansionAnswer();
    expect(value.engine.handleInput).toHaveBeenCalledWith({ answers: ['GÜN', 'SU', 'YOL', 'EV'] });
  });
  it('does not submit missing answers or accept input while paused', () => {
    const value = player(['A', 'B']);
    value.expansionAnswers = ['A'];
    value.submitExpansionAnswer();
    expect(value.engine.handleInput).not.toHaveBeenCalled();
    value.expansionAnswers = ['A', 'B'];
    value.engine.state.isPaused = true;
    value.submitExpansionAnswer();
    expect(value.engine.handleInput).not.toHaveBeenCalled();
  });
  it('synchronizes owned pause/resume and exit cancellation with the server', async () => {
    const value = player(['A','B']);
    value.sessionId = 'session'; value.actionQueue = Promise.resolve();
    value.engineState = { isPaused: false, isRunning: true, isCompleted: false };
    value.isTachistoscopeMode = () => false; value.shouldTrackReading = () => false;
    value.engine.pause = () => value.engineState.isPaused = true;
    value.engine.resume = () => value.engineState.isPaused = false;
    value.sessionService = { pauseSession: jasmine.createSpy('pause').and.returnValue(of({})),
      resumeSession: jasmine.createSpy('resume').and.returnValue(of({})) };
    await value.goBack();
    expect(value.sessionService.pauseSession).toHaveBeenCalledWith('session');
    expect(value.engineState.isPaused).toBeTrue();
    expect(value.showExitConfirm).toBeTrue();
    await value.cancelExit();
    expect(value.sessionService.resumeSession).toHaveBeenCalledWith('session');
    expect(value.engineState.isPaused).toBeFalse();
    expect(value.showExitConfirm).toBeFalse();
  });
  it('uses verified server round summaries instead of local preview estimates', () => {
    const value = player(['A','B']);
    value.sessionId = 'session'; value.exercise = { id: 'exercise' };
    value.route = { snapshot: { queryParamMap: { get: () => null } } };
    value.engineState = { totalSteps: 2 }; value.questionAnswers = []; value.parsedConfig = {};
    value.isPreviewSession = () => false; value.isTachistoscopeMode = () => false;
    value.showToast = () => undefined; value.completeDailyProgress = () => undefined;
    value.sessionService = { completeSession: () => of({ accuracy: 50, score: 50, rawWPM: null,
      detailedResults: { visualExpansionMaxPresentedDistance: 12, visualExpansionAverageResponseTimeMs: 500,
        visualExpansionTargetSummary: { targetDistance: 16, targetReached: false, targetRounds: 0,
          targetCorrectRounds: 0, targetAccuracy: null, shortestCorrectDisplayDurationMs: null },
        visualExpansionRoundResults: [{ round: 1, distance: 10, responseTimeMs: 400 }, { round: 2, distance: 12, responseTimeMs: 600 }] } }) };
    value.result = { score: 100, accuracy: 100, totalTime: 1000, totalSteps: 2, completedSteps: 2, errors: 0,
      details: { maxDegreesReached: 999, averageResponseTimeMs: 999, roundResults: [] } };
    value.saveResult(value.result);
    expect(value.result.details.maxDegreesReached).toBe(12);
    expect(value.result.details.averageResponseTimeMs).toBe(500);
    expect(value.result.details.roundResults.length).toBe(2);
    expect(value.result.details.targetSummary.targetReached).toBeFalse();
    expect(value.result.details.targetSummary.targetAccuracy).toBeNull();
  });
});
