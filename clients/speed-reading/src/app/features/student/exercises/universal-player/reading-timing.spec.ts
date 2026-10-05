import { fakeAsync, flushMicrotasks, tick } from '@angular/core/testing';
import { of } from 'rxjs';
import { ExercisePlayerComponent } from './exercise-player.component';
import { RegressionReductionEngine } from './engines/regression-reduction.engine';
import { EngineCallbacks } from './engines/base-engine.interface';

describe('reading phase timing', () => {
  it('synchronizes normal reading pauses with the server', fakeAsync(() => {
    const player = Object.create(ExercisePlayerComponent.prototype) as any;
    player.sessionId = 'owned-reading';
    player.engineState = { isPaused: false };
    player.isTachistoscopeMode = () => false;
    player.shouldTrackReading = () => true;
    player.actionQueue = Promise.resolve();
    player.engine = { pause: jasmine.createSpy('pause'), resume: jasmine.createSpy('resume') };
    player.cdr = { detectChanges: () => undefined };
    player.sessionService = {
      pauseSession: jasmine.createSpy('pauseSession').and.returnValue(of({})),
      resumeSession: jasmine.createSpy('resumeSession').and.returnValue(of({}))
    };
    player.togglePause();
    flushMicrotasks();
    expect(player.sessionService.pauseSession).toHaveBeenCalledWith('owned-reading');
    player.engineState.isPaused = true;
    player.togglePause();
    flushMicrotasks();
    expect(player.sessionService.resumeSession).toHaveBeenCalledWith('owned-reading');
  }));

  it('does not include regression question time in local reading WPM', fakeAsync(() => {
    let result: any;
    const callbacks: EngineCallbacks = {
      onStart: () => undefined, onPause: () => undefined, onResume: () => undefined,
      onComplete: value => result = value, onError: () => undefined,
      onStateChange: () => undefined, onStepComplete: () => undefined, onAction: () => undefined
    };
    const engine = new RegressionReductionEngine();
    engine.initialize({ readingTextContent: 'bir iki uc', wpm: 60,
      questions: [{ questionId: 'q1', correctAnswer: 'A' }] } as any, callbacks);
    engine.start();
    tick(4000);
    expect(engine.getPhase()).toBe('answering');
    tick(60000);
    (engine as any).complete();
    expect(result.details.wpm).toBe(45);
    engine.destroy();
  }));
});
