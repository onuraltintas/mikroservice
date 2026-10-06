import { fakeAsync, tick } from '@angular/core/testing';
import { EngineCallbacks, EngineResult } from './base-engine.interface';
import { SubvocalizationReductionEngine } from './subvocalization-reduction.engine';

describe('subvocalization measurement', () => {
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
