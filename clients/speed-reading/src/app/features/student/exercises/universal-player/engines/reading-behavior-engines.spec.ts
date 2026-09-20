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

  it('starts each behavior engine only once', () => {
    let regressionStarts = 0;
    let subvocalizationStarts = 0;
    const regression = new RegressionReductionEngine();
    const subvocalization = new SubvocalizationReductionEngine();
    regression.initialize({ readingTextContent: 'bir', wpm: 200 } as any,
      { ...callbacks(), onStart: () => regressionStarts++ });
    subvocalization.initialize({ readingTextContent: 'bir', wpm: 200 } as any,
      { ...callbacks(), onStart: () => subvocalizationStarts++ });

    regression.start();
    regression.start();
    subvocalization.start();
    subvocalization.start();

    expect(regressionStarts).toBe(1);
    expect(subvocalizationStarts).toBe(1);
    regression.destroy();
    subvocalization.destroy();
  });

  it('emits pause and resume callbacks for both behavior engines', () => {
    let pauses = 0;
    let resumes = 0;
    const trackedCallbacks = {
      ...callbacks(),
      onPause: () => pauses++,
      onResume: () => resumes++
    };
    const regression = new RegressionReductionEngine();
    const subvocalization = new SubvocalizationReductionEngine();
    regression.initialize({ readingTextContent: 'bir' } as any, trackedCallbacks);
    subvocalization.initialize({ readingTextContent: 'bir' } as any, trackedCallbacks);

    regression.start();
    subvocalization.start();
    regression.pause();
    subvocalization.pause();
    regression.resume();
    subvocalization.resume();

    expect(pauses).toBe(2);
    expect(resumes).toBe(2);
    regression.destroy();
    subvocalization.destroy();
  });

  it('completes each behavior engine only once', () => {
    let completions = 0;
    const trackedCallbacks = callbacks(() => completions++);
    const regression = new RegressionReductionEngine();
    const subvocalization = new SubvocalizationReductionEngine();
    regression.initialize({ readingTextContent: 'bir' } as any, trackedCallbacks);
    subvocalization.initialize({ readingTextContent: 'bir' } as any, trackedCallbacks);

    (regression as any).complete();
    (regression as any).complete();
    (subvocalization as any).complete();
    (subvocalization as any).complete();

    expect(completions).toBe(2);
  });

  it('uses configured metronome BPM', fakeAsync(() => {
    const engine = new SubvocalizationReductionEngine();
    engine.initialize({
      readingTextContent: 'bir iki üç',
      metronomeEnabled: true,
      metronomeBpm: 120,
      wpm: 20
    } as any, callbacks());

    engine.start();
    expect((engine.state as any).metronomeStep).toBe(1);
    tick(500);
    expect((engine.state as any).metronomeStep).toBe(2);
    engine.destroy();
  }));

  it('fully resets regression state and allows a new run', () => {
    let starts = 0;
    const engine = new RegressionReductionEngine();
    engine.initialize({ readingTextContent: 'bir' } as any,
      { ...callbacks(), onStart: () => starts++ });
    (engine as any).complete();

    engine.reset();
    engine.start();

    expect(engine.state.isCompleted).toBeFalse();
    expect(engine.state.score).toBe(0);
    expect(engine.state.errors).toBe(0);
    expect(starts).toBe(1);
    engine.destroy();
  });

  it('reports one-based subvocalization reading progress', fakeAsync(() => {
    const engine = new SubvocalizationReductionEngine();
    engine.initialize({ readingTextContent: 'bir iki', wpm: 1500, chunkSize: 1 } as any, callbacks());

    engine.start();
    tick(40);

    expect(engine.state.currentStep).toBe(1);
    engine.destroy();
  }));

  it('ignores late regression answers after completion', () => {
    const engine = new RegressionReductionEngine();
    engine.initialize({
      readingTextContent: 'bir',
      questions: [{ questionId: 'q1', correctAnswer: 'a' }]
    } as any, callbacks());
    (engine as any).complete();

    expect(() => engine.handleInput({ type: 'answer', answer: 'a' })).not.toThrow();
  });

  it('ignores invalid regression positions', () => {
    const engine = new RegressionReductionEngine();
    engine.initialize({ readingTextContent: 'bir' } as any, callbacks());

    engine.handleInput({ type: 'regression', wordIndex: 99 });

    expect(engine.state.errors).toBe(0);
    expect(engine.state.accuracy).toBe(100);
  });

  it('ignores malformed subvocalization input events', () => {
    const engine = new SubvocalizationReductionEngine();
    engine.initialize({ readingTextContent: 'bir iki' } as any, callbacks());

    expect(() => engine.handleInput(null)).not.toThrow();
    expect(() => engine.handleInput({ type: 'line_breaks' })).not.toThrow();
  });
});
