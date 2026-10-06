import { ExercisePlayerComponent } from './exercise-player.component';
import { of } from 'rxjs';

describe('visual expansion answer UI', () => {
  function player(stimuli: string[]) {
    const value = Object.create(ExercisePlayerComponent.prototype) as any;
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
  it('uses verified server round summaries instead of local preview estimates', () => {
    const value = player(['A','B']);
    value.sessionId = 'session'; value.exercise = { id: 'exercise' };
    value.engineState = { totalSteps: 2 }; value.questionAnswers = []; value.parsedConfig = {};
    value.isPreviewSession = () => false; value.isTachistoscopeMode = () => false;
    value.showToast = () => undefined; value.completeDailyProgress = () => undefined;
    value.sessionService = { completeSession: () => of({ accuracy: 50, score: 50, rawWPM: null,
      detailedResults: { visualExpansionMaxPresentedDistance: 12, visualExpansionAverageResponseTimeMs: 500,
        visualExpansionRoundResults: [{ round: 1, distance: 10, responseTimeMs: 400 }, { round: 2, distance: 12, responseTimeMs: 600 }] } }) };
    value.result = { score: 100, accuracy: 100, totalTime: 1000, totalSteps: 2, completedSteps: 2, errors: 0,
      details: { maxDegreesReached: 999, averageResponseTimeMs: 999, roundResults: [] } };
    value.saveResult(value.result);
    expect(value.result.details.maxDegreesReached).toBe(12);
    expect(value.result.details.averageResponseTimeMs).toBe(500);
    expect(value.result.details.roundResults.length).toBe(2);
  });
});
