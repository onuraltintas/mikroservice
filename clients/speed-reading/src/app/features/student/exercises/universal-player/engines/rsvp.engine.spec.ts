import { fakeAsync, tick } from '@angular/core/testing';
import { TextStreamEngine } from './text-stream.engine';
import { EngineCallbacks } from './base-engine.interface';
import { ExercisePlayerComponent } from '../exercise-player.component';

describe('RSVP presentation', () => {
  let result: any;
  let errors: string[];
  let engine: TextStreamEngine;
  const callbacks: EngineCallbacks = {
    onStart: () => undefined, onPause: () => undefined, onResume: () => undefined,
    onStateChange: () => undefined, onStepComplete: () => undefined, onAction: () => undefined,
    onComplete: value => result = value, onError: value => errors.push(value)
  };
  beforeEach(() => { result = undefined; errors = []; engine = new TextStreamEngine(); });
  afterEach(() => engine.destroy());

  it('preserves the remaining word exposure across pause', fakeAsync(() => {
    engine.initialize({ mode: 'rsvp', words: ['bir', 'iki'], timing: { durationMs: 200 }, visuals: { showFixation: false } } as any, callbacks);
    engine.start(); tick(150); engine.pause(); tick(5000); engine.resume(); tick(50);
    expect(engine.state.currentStep).toBe(1);
    tick(200);
    expect(result.totalTime).toBe(400);
    expect(result.details.wpm).toBeNull();
  }));

  it('preserves the remaining fixation across pause', fakeAsync(() => {
    engine.initialize({ mode: 'rsvp', words: ['bir'], timing: { durationMs: 200 } } as any, callbacks);
    engine.start(); tick(100); engine.pause(); tick(1000); engine.resume(); tick(399);
    expect(result).toBeUndefined(); tick(1);
    expect(result.totalTime).toBe(500);
  }));

  it('does not silently replace missing RSVP text with random words', () => {
    engine.initialize({ mode: 'rsvp' } as any, callbacks);
    engine.start();
    expect(errors.length).toBe(1);
    expect(engine.state.isRunning).toBeFalse();
  });

  it('does not let catalog words replace the owned server text', fakeAsync(() => {
    engine.initialize({ serverAuthoritative: true, words: ['sunucu'], engineConfig: { mode: 'rsvp', words: ['eski', 'katalog'] }, visuals: { showFixation: false } } as any, callbacks);
    engine.start(); tick(500);
    expect(result.totalSteps).toBe(1);
  }));

  it('normalizes RSVP mode names', fakeAsync(() => {
    engine.initialize({ mode: 'RSVP', words: ['bir'], visuals: { showFixation: false } } as any, callbacks);
    engine.start(); tick(500);
    expect(result).toBeDefined();
  }));

  it('does not impose a second player deadline on RSVP', fakeAsync(() => {
    engine.initialize({ mode: 'rsvp', words: ['bir'] } as any, callbacks);
    const player = Object.create(ExercisePlayerComponent.prototype) as any;
    player.engine = { engineType: 'text_stream', getMode: () => 'rsvp', finish: jasmine.createSpy('finish') };
    player.exercise = { exerciseTypeName: 'RSVP' };
    player.engineState = { remainingSeconds: 1 };
    player.cdr = { detectChanges: () => undefined };
    player.startTimer(); tick(2100); player.stopTimer();
    expect(player.engine.finish).not.toHaveBeenCalled();
  }));

  it('uses the owned timing snapshot after a catalog edit', fakeAsync(() => {
    engine.initialize({ serverAuthoritative: true, exerciseTypeName: 'RSVP', words: ['bir', 'iki'],
      rsvpProtocolVersion: 1, rsvpDisplayDurationMs: 200, rsvpGapMs: 100, rsvpFixationMs: 300,
      engineConfig: { mode: 'rsvp', displayDurationMs: 50, timing: { intervalMs: 0 }, visuals: { showFixation: false } }
    } as any, callbacks);
    engine.start(); tick(1099); expect(result).toBeUndefined(); tick(1);
    expect(result.totalTime).toBe(1100);
  }));

  for (const words of [undefined, null, []]) {
    it(`rejects absent owned text (${String(words)}) even when catalog content exists`, () => {
      engine.initialize({ serverAuthoritative: true, exerciseTypeName: 'RSVP', words,
        engineConfig: { mode: 'rsvp', words: ['katalog'], content: { source: 'custom', items: ['yerel'] } }
      } as any, callbacks);
      engine.start();
      expect(errors.length).toBe(1);
      expect(engine.state.isRunning).toBeFalse();
    });
  }

  it('reports RSVP mode inferred from type to the player', () => {
    engine.initialize({ exerciseTypeName: 'RSVP', words: ['bir'] } as any, callbacks);
    expect(engine.getMode()).toBe('rsvp');
  });

  it('normalizes the player Tachistoscope type check', () => {
    engine.initialize({ mode: 'rsvp', words: ['bir'] } as any, callbacks);
    const player = Object.create(ExercisePlayerComponent.prototype) as any;
    player.engine = engine;
    player.exercise = { exerciseTypeName: 'tachistoscope' };
    expect(player.isTachistoscopeMode()).toBeTrue();
  });

  it('keeps the remaining gap and excludes pauses from presentation time', fakeAsync(() => {
    engine.initialize({ mode: 'rsvp', words: ['bir', 'iki'], timing: { durationMs: 200, intervalMs: 100 }, visuals: { showFixation: false } } as any, callbacks);
    engine.start(); tick(250); engine.pause(); tick(1000); engine.resume(); tick(249);
    expect(result).toBeUndefined(); tick(1);
    expect(result.totalTime).toBe(500);
    expect(result.details.displayPaceWpm).toBe(240);
  }));
});
