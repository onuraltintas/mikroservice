import { fakeAsync, tick } from '@angular/core/testing';
import { EngineCallbacks, EngineResult } from './base-engine.interface';
import { SubvocalizationReductionEngine } from './subvocalization-reduction.engine';

describe('subvocalization measurement', () => {
  function createEngine(text: string, chunkSize = 1): SubvocalizationReductionEngine {
    const engine = new SubvocalizationReductionEngine();
    engine.initialize({ readingTextContent: text, wpm: 600, chunkSize } as any, {
      onStart: () => undefined, onPause: () => undefined, onResume: () => undefined,
      onComplete: () => undefined, onError: () => undefined,
      onStateChange: () => undefined, onStepComplete: () => undefined, onAction: () => undefined
    });
    return engine;
  }

  it('shows the first group immediately and the partial final group for its actual duration', fakeAsync(() => {
    const engine = createEngine('bir iki uc', 2);
    engine.start();
    expect(engine.getCurrentWordIndex()).toBe(1);
    expect(engine.getCurrentChunk()).toBe('bir iki');
    tick(200);
    expect(engine.getCurrentChunk()).toBe('uc');
    expect(engine.state.isCompleted).toBeFalse();
    tick(100);
    expect(engine.state.isCompleted).toBeTrue();
    expect(engine.state.timeElapsed).toBe(300);
    engine.destroy();
  }));

  it('preserves remaining word exposure while paused', fakeAsync(() => {
    const engine = createEngine('bir iki');
    engine.start();
    tick(60); engine.pause(); tick(1000); engine.resume();
    tick(39);
    expect(engine.getCurrentWordIndex()).toBe(0);
    tick(1);
    expect(engine.getCurrentWordIndex()).toBe(1);
    tick(100);
    expect(engine.state.timeElapsed).toBe(200);
    engine.destroy();
  }));

  it('reports display pace without fabricating reading speed or comprehension', fakeAsync(() => {
    let result: EngineResult | undefined;
    const callbacks: EngineCallbacks = {
      onStart: () => undefined, onPause: () => undefined, onResume: () => undefined,
      onComplete: value => result = value, onError: () => undefined,
      onStateChange: () => undefined, onStepComplete: () => undefined, onAction: () => undefined
    };
    const engine = new SubvocalizationReductionEngine();
    engine.initialize({ readingTextContent: 'bir iki', wpm: 600 } as any, callbacks);
    engine.start();
    tick(1000);
    expect(result).toBeDefined();
    expect(result!.details.wpm).toBeUndefined();
    expect(result!.details.displayPaceWpm).toBe(600);
    expect(result!.details.comprehensionScore).toBeNull();
    expect(result!.details.measurementStatus).toBe('NotMeasured');
    expect(result!.score).toBe(0);
    expect(result!.accuracy).toBe(0);
    engine.destroy();
  }));
});
