import { ExercisePlayerComponent } from './exercise-player.component';
import { of, throwError } from 'rxjs';

function player(preview = false): any {
  const value = Object.create(ExercisePlayerComponent.prototype) as any;
  value.sessionId = preview ? 'preview-mode' : 'session'; value.actionQueue = Promise.resolve();
  value.engineState = { isPaused: false, isRunning: true, isCompleted: false };
  value.engine = { engineType: 'motion_path', getMode: () => 'saccade',
    pause: () => value.engineState.isPaused = true,
    resume: () => value.engineState.isPaused = false,
    stop: jasmine.createSpy('stop') };
  value.isTachistoscopeMode = () => false; value.shouldTrackReading = () => false;
  value.cdr = { detectChanges: () => undefined };
  value.sessionService = { pauseSession: jasmine.createSpy('pause').and.returnValue(of({})),
    resumeSession: jasmine.createSpy('resume').and.returnValue(of({})) };
  return value;
}
describe('saccade owned player pause', () => {
  it('synchronizes explicit pause and resume with the owned session', async () => {
    const value = player();
    await value.togglePause(); expect(value.sessionService.pauseSession).toHaveBeenCalledWith('session');
    await value.togglePause(); expect(value.sessionService.resumeSession).toHaveBeenCalledWith('session');
    expect(value.engineState.isPaused).toBeFalse();
  });
  it('synchronizes exit confirmation and cancellation without counting the wait as active time', async () => {
    const value = player();
    await value.goBack(); expect(value.sessionService.pauseSession).toHaveBeenCalledWith('session');
    await value.cancelExit(); expect(value.sessionService.resumeSession).toHaveBeenCalledWith('session');
  });
  it('keeps preview pause and exit cancellation local only', async () => {
    const value = player(true);
    await value.goBack(); expect(value.engineState.isPaused).toBeTrue();
    await value.cancelExit(); expect(value.engineState.isPaused).toBeFalse();
    expect(value.sessionService.pauseSession).not.toHaveBeenCalled();
    expect(value.sessionService.resumeSession).not.toHaveBeenCalled();
  });
  it('stops and surfaces a server pause failure instead of silently continuing', async () => {
    const value = player(); value.getActionValidationErrorMessage = () => 'Duraklatma başarısız';
    value.sessionService.pauseSession.and.returnValue(throwError(() => new Error('offline')));
    await value.togglePause();
    expect(value.engine.stop).toHaveBeenCalled(); expect(value.error).toBe('Duraklatma başarısız');
  });
});
