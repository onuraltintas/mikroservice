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
  value.isPreviewSession = () => preview;
  value.cdr = { detectChanges: () => undefined };
  value.sessionService = { pauseSession: jasmine.createSpy('pause').and.returnValue(of({})),
    resumeSession: jasmine.createSpy('resume').and.returnValue(of({})),
    discardSession: jasmine.createSpy('discard').and.returnValue(of({})) };
  value.navigateBack = jasmine.createSpy('navigateBack');
  value.showToast = jasmine.createSpy('showToast');
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
  it('deletes the incomplete owned session before navigating away', async () => {
    const value = player();
    value.showExitConfirm = true;
    await value.confirmExit();
    expect(value.sessionService.discardSession).toHaveBeenCalledOnceWith('session');
    expect(value.engine.stop).toHaveBeenCalled();
    expect(value.navigateBack).toHaveBeenCalled();
  });
  it('keeps the exit confirmation open when the incomplete session could not be deleted', async () => {
    const value = player();
    value.showExitConfirm = true;
    value.sessionService.discardSession.and.returnValue(throwError(() => new Error('offline')));
    await value.confirmExit();
    expect(value.navigateBack).not.toHaveBeenCalled();
    expect(value.showExitConfirm).toBeTrue();
    expect(value.showToast).toHaveBeenCalled();
  });
  it('asks before discarding a paused incomplete session even when the engine is no longer running', async () => {
    const value = player();
    value.engineState = { isPaused: true, isRunning: false, isCompleted: false };
    await value.goBack();
    expect(value.showExitConfirm).toBeTrue();
    expect(value.navigateBack).not.toHaveBeenCalled();
  });
  it('stops and surfaces a server pause failure instead of silently continuing', async () => {
    const value = player(); value.getActionValidationErrorMessage = () => 'Duraklatma başarısız';
    value.sessionService.pauseSession.and.returnValue(throwError(() => new Error('offline')));
    await value.togglePause();
    expect(value.engine.stop).toHaveBeenCalled(); expect(value.error).toBe('Duraklatma başarısız');
  });
});
