import { ExercisePlayerComponent } from './exercise-player.component';
import { of } from 'rxjs';

describe('fixation owned session pause', () => {
  it('displays verified round history instead of local estimates', () => {
    const value = Object.create(ExercisePlayerComponent.prototype) as any;
    value.engine = { engineType: 'motion_path', getMode: () => 'fixation' };
    value.sessionId = 'session'; value.exercise = { id: 'exercise' };
    value.engineState = { totalSteps: 1 }; value.questionAnswers = []; value.parsedConfig = {};
    value.isPreviewSession = () => false; value.isTachistoscopeMode = () => false;
    value.showToast = () => undefined; value.completeDailyProgress = () => undefined;
    value.cdr = { detectChanges: () => undefined };
    value.sessionService = { completeSession: () => of({ accuracy: 100, score: 100, rawWPM: null,
      detailedResults: { fixationRoundResults: [{ round: 1, holdMs: 1000, responseTimeMs: 200 }] } }) };
    value.result = { score: 100, accuracy: 100, totalTime: 1550, totalSteps: 1, completedSteps: 1, errors: 0,
      details: { serverValidatedFixation: true, fixationResults: [], averageResponseTimeMs: 999 } };
    value.saveResult(value.result);
    expect(value.result.details.fixationResults.length).toBe(1);
    expect(value.result.details.averageResponseTimeMs).toBe(200);
  });
  it('synchronizes pause and exit cancellation with its owned server session', async () => {
    const value = Object.create(ExercisePlayerComponent.prototype) as any;
    value.sessionId = 'session'; value.actionQueue = Promise.resolve();
    value.engineState = { isPaused: false, isRunning: true, isCompleted: false };
    value.engine = { engineType: 'motion_path', getMode: () => 'fixation',
      pause: () => value.engineState.isPaused = true,
      resume: () => value.engineState.isPaused = false };
    value.isTachistoscopeMode = () => false; value.shouldTrackReading = () => false;
    value.cdr = { detectChanges: () => undefined };
    value.sessionService = { pauseSession: jasmine.createSpy('pause').and.returnValue(of({})),
      resumeSession: jasmine.createSpy('resume').and.returnValue(of({})) };
    await value.goBack();
    expect(value.sessionService.pauseSession).toHaveBeenCalledWith('session');
    await value.cancelExit();
    expect(value.sessionService.resumeSession).toHaveBeenCalledWith('session');
  });
});
