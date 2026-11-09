import { of } from 'rxjs';
import { ExercisePlayerComponent } from './exercise-player.component';

describe('Scanning server pause synchronization', () => {
  it('filters preview passages by the scanning word band', () => {
    const player: any = Object.create(ExercisePlayerComponent.prototype);
    player.exercise = { exerciseTypeName: 'Scanning' };
    player.parsedConfig = { engineType: 'scanning', engineConfig: { content: { minWordCount: 100, maxWordCount: 200 } } };
    expect(player.previewTextMatchesLength(99)).toBeFalse();
    expect(player.previewTextMatchesLength(201)).toBeFalse();
    expect(player.previewTextMatchesLength(150)).toBeTrue();
  });
  it('synchronizes pause and resume with the session service', async () => {
    const player: any = Object.create(ExercisePlayerComponent.prototype);
    player.engineState = { isPaused: false };
    player.engine = { engineType: 'scan_find', pause: () => player.engineState.isPaused = true,
      resume: () => player.engineState.isPaused = false };
    player.sessionId = 'scan-session';
    player.actionQueue = Promise.resolve();
    player.cdr = { detectChanges: () => undefined };
    player.isTachistoscopeMode = () => false;
    player.shouldTrackReading = () => false;
    player.sessionService = { pauseSession: jasmine.createSpy().and.returnValue(of({})),
      resumeSession: jasmine.createSpy().and.returnValue(of({})) };
    await player.togglePause();
    expect(player.sessionService.pauseSession).toHaveBeenCalledWith('scan-session');
    await player.togglePause();
    expect(player.sessionService.resumeSession).toHaveBeenCalledWith('scan-session');
  });

  // The search had already ended (here with no answer) but the player's copy still showed it running: the exit and
  // pause buttons must not pause the finished session on the server, which answered HTTP 500.
  it('does not pause or resume the session once the search has ended', async () => {
    const player: any = Object.create(ExercisePlayerComponent.prototype);
    player.engineState = { isRunning: true, isPaused: false, isCompleted: false };
    player.engine = { engineType: 'scan_find', state: { isRunning: false, isPaused: false, isCompleted: true },
      pause: jasmine.createSpy('pause'), resume: jasmine.createSpy('resume') };
    player.exercisePhase = 'completed';
    player.sessionId = 'scan-session';
    player.actionQueue = Promise.resolve();
    player.cdr = { detectChanges: () => undefined };
    player.isTachistoscopeMode = () => false;
    player.shouldTrackReading = () => false;
    player.isFixationMode = () => false;
    player.isSaccadeMode = () => false;
    player.isPreviewSession = () => false;
    player.navigateBack = jasmine.createSpy('navigateBack');
    player.sessionService = { pauseSession: jasmine.createSpy().and.returnValue(of({})),
      resumeSession: jasmine.createSpy().and.returnValue(of({})) };
    await player.togglePause();
    await player.goBack();
    expect(player.sessionService.pauseSession).not.toHaveBeenCalled();
    expect(player.sessionService.resumeSession).not.toHaveBeenCalled();
    expect(player.engine.pause).not.toHaveBeenCalled();
    expect(player.showExitConfirm).toBeFalsy();
    expect(player.navigateBack).toHaveBeenCalled();
  });
});

