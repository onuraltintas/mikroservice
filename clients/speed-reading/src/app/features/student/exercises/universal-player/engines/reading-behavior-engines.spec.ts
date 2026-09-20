import { fakeAsync, tick } from '@angular/core/testing';
import { EngineCallbacks } from './base-engine.interface';
import { RegressionReductionEngine } from './regression-reduction.engine';
import { SubvocalizationReductionEngine } from './subvocalization-reduction.engine';

function callbacks(onComplete: (result: any) => void = () => undefined): EngineCallbacks {
  return {
    onStart: () => undefined,
    onPause: () => undefined,
    onResume: () => undefined,
    onComplete,
    onError: () => undefined,
    onStateChange: () => undefined,
    onStepComplete: () => undefined,
    onAction: () => undefined
  };
}

describe('reading behavior engines', () => {
  it('reads regression content and pacing from nested engine configuration', () => {
    const engine = new RegressionReductionEngine();

    engine.initialize({
      engineConfig: {
        readingTextContent: 'bir iki üç',
        wpm: 480,
        chunkSize: 2,
        maskingType: 'trailing'
      }
    } as any, callbacks());

    expect(engine.getWords()).toEqual(['bir', 'iki', 'üç']);
    expect(engine.getMaskingType()).toBe('trailing');
    expect(engine.state.totalSteps).toBe(3);
  });

  it('completes regression reading when there are no questions', fakeAsync(() => {
    let result: any;
    const engine = new RegressionReductionEngine();
    engine.initialize({ readingTextContent: 'bir', wpm: 1500 } as any, callbacks(value => result = value));

    engine.start();
    tick(100);

    expect(engine.state.isCompleted).toBeTrue();
    expect(result.completedSteps).toBe(1);
    engine.destroy();
  }));

  it('reads subvocalization content and settings from nested engine configuration', () => {
    const engine = new SubvocalizationReductionEngine();

    engine.initialize({
      engineConfig: {
        readingTextContent: 'bir iki üç dört',
        targetWpm: 400,
        chunkSize: 2,
        displayMode: 'chunk',
        metronomeEnabled: true,
        metronomeBpm: 120
      }
    } as any, callbacks());

    expect(engine.getWords()).toEqual(['bir', 'iki', 'üç', 'dört']);
    expect(engine.getTargetWpm()).toBe(400);
    expect(engine.getDisplayMode()).toBe('chunk');
  });

  it('keeps subvocalization total steps consistent when questions exist', () => {
    const engine = new SubvocalizationReductionEngine();
    engine.initialize({
      readingTextContent: 'bir iki',
      questions: [{ questionId: 'q1', correctAnswer: 'a' }]
    } as any, callbacks());

    expect(engine.state.totalSteps).toBe(3);
    engine.reset();
    expect(engine.state.totalSteps).toBe(3);
  });
});
