import { fakeAsync, tick } from '@angular/core/testing';
import { EngineCallbacks, EngineResult } from './base-engine.interface';
import { ScanFindEngine } from './scan-find.engine';

function callbacks(onComplete: (result: EngineResult) => void): EngineCallbacks {
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

describe('ScanFindEngine', () => {
  it('deduplicates targets and completes after the unique target is found', () => {
    let result: EngineResult | undefined;
    const engine = new ScanFindEngine();
    engine.initialize({
      content: { text: 'target', wordCount: 1 },
      targets: { words: ['target', 'target'], caseSensitive: false }
    }, callbacks(value => result = value));

    engine.start();
    engine.handleWordClick(0);

    expect(result).toEqual(jasmine.objectContaining({
      score: 100,
      accuracy: 100,
      totalSteps: 1,
      completedSteps: 1
    }));
  });

  it('reports normalized partial accuracy when the time limit expires', fakeAsync(() => {
    let result: EngineResult | undefined;
    const engine = new ScanFindEngine();
    engine.initialize({
      content: { text: 'one two', wordCount: 2 },
      targets: { words: ['one', 'two'], caseSensitive: false },
      timeLimitSeconds: 1
    }, callbacks(value => result = value));

    engine.start();
    engine.handleWordClick(0);
    tick(1100);

    expect(result).toEqual(jasmine.objectContaining({
      score: 50,
      accuracy: 50,
      totalSteps: 2,
      completedSteps: 1
    }));
  }));

  it('normalizes malformed legacy containers and bounds generated content', () => {
    const engine = new ScanFindEngine();

    expect(() => engine.initialize({
      content: 'invalid',
      targets: 'invalid',
      timing: 'invalid',
      scanningRounds: 'invalid',
      timeLimitSeconds: Number.MAX_VALUE
    } as any, callbacks(() => undefined))).not.toThrow();

    expect(engine.getWords().length).toBeLessThanOrEqual(10000);
    expect(engine.getTargetWords()).toEqual([]);
  });
});
