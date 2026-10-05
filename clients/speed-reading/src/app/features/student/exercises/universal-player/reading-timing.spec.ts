import { fakeAsync, flushMicrotasks, tick } from '@angular/core/testing';
import { of, Subject, throwError } from 'rxjs';
import { ExercisePlayerComponent } from './exercise-player.component';
import { RegressionReductionEngine } from './engines/regression-reduction.engine';
import { EngineCallbacks } from './engines/base-engine.interface';

describe('reading phase timing', () => {
  it('waits for validated server start before starting the engine', () => {
    const player = Object.create(ExercisePlayerComponent.prototype) as any;
    const response = new Subject<any>();
    const started = jasmine.createSpy('started');
    player.sessionId = 'reading';
    player.destroy$ = new Subject<void>();
    player.shouldTrackReading = () => true;
    player.sessionService = { validateAction: () => response };
    player.startReadingTracking(started);
    expect(started).not.toHaveBeenCalled();
    response.next({ isValid: true });
    expect(started).toHaveBeenCalledTimes(1);
    response.complete();
  });

  it('does not finalize reading as successful after a tracking network error', () => {
    const player = Object.create(ExercisePlayerComponent.prototype) as any;
    const finalized = jasmine.createSpy('finalized');
    player.sessionId = 'reading';
    player.destroy$ = new Subject<void>();
    player.shouldTrackReading = () => true;
    player.readingTrackingStarted = true;
    player.readingTrackingStartCompleted = true;
    player.sessionService = { validateAction: () => throwError(() => new Error('offline')) };
    player.engine = { stop: () => undefined };
    player.stopTimer = () => undefined;
    player.cdr = { detectChanges: () => undefined };
    player.finishReadingTracking(finalized);
    expect(finalized).not.toHaveBeenCalled();
    expect(player.readingTrackingFinished).toBeFalse();
  });
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
