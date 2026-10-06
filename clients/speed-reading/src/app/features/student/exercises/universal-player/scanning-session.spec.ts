import { of } from 'rxjs';
import { ExercisePlayerComponent } from './exercise-player.component';

describe('Scanning server pause synchronization', () => {
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
});
