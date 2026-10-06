import { ExercisePlayerComponent } from './exercise-player.component';
import { of } from 'rxjs';

describe('fixation owned session pause', () => {
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
