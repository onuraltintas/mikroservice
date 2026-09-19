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

  it('counts mixed-case duplicate targets once in case-insensitive mode', () => {
    let result: EngineResult | undefined;
    const engine = new ScanFindEngine();
    engine.initialize({
      content: { text: 'Target', wordCount: 1 },
      targets: { words: ['Target', 'target'], caseSensitive: false }
    }, callbacks(value => result = value));

    engine.start();
    engine.handleWordClick(0);

    expect(result).toEqual(jasmine.objectContaining({ accuracy: 100, totalSteps: 1, completedSteps: 1 }));
  });

  it('keeps cumulative progress while advancing across rounds', () => {
    let result: EngineResult | undefined;
    const engine = new ScanFindEngine();
    engine.initialize({
      scanningRounds: [
        { textContent: 'one', targets: ['one'] },
        { textContent: 'two', targets: ['two'] }
      ]
    } as any, callbacks(value => result = value));

    engine.start();
    engine.handleWordClick(0);
    expect(engine.state.currentStep).toBe(1);
    engine.handleWordClick(0);

    expect(result).toEqual(jasmine.objectContaining({ accuracy: 100, totalSteps: 2, completedSteps: 2 }));
  });

  it('skips empty rounds and continues with the next playable round', () => {
    let result: EngineResult | undefined;
    const engine = new ScanFindEngine();
    engine.initialize({
      scanningRounds: [
        { textContent: '', targets: [] },
        { textContent: 'target', targets: ['target'] }
      ]
    } as any, callbacks(value => result = value));

    engine.start();

    expect(result).toBeUndefined();
    expect(engine.getTargetWords()).toEqual(['target']);
    engine.handleWordClick(0);
    expect(result?.accuracy).toBe(100);
  });

  it('completes find-any mode after the first valid target', () => {
    let result: EngineResult | undefined;
    const engine = new ScanFindEngine();
    engine.initialize({
      content: { text: 'one two', wordCount: 2 },
      targets: { words: ['one', 'two'], mode: 'find_any' }
    }, callbacks(value => result = value));

    engine.start();
    engine.handleWordClick(0);

    expect(result).toEqual(jasmine.objectContaining({ accuracy: 100, totalSteps: 1, completedSteps: 1 }));
  });

  it('uses server reading text aliases for text-id content', () => {
    const engine = new ScanFindEngine();
    engine.initialize({
      ReadingTextContent: 'server target text',
      content: { source: 'text_id' },
      targets: { words: ['target'] }
    } as any, callbacks(() => undefined));

    expect(engine.getWords().map(word => word.text)).toEqual(['server', 'target', 'text']);
  });

  it('restores completed prior rounds when resuming', () => {
    let result: EngineResult | undefined;
    const engine = new ScanFindEngine();
    engine.initialize({
      currentRound: 1,
      scanningRounds: [
        { textContent: 'one', targets: ['one'], foundTargets: ['one'] },
        { textContent: 'two', targets: ['two'], foundTargets: [] }
      ]
    } as any, callbacks(value => result = value));

    engine.start();
    expect(engine.state.currentStep).toBe(1);
    engine.handleWordClick(0);

    expect(result).toEqual(jasmine.objectContaining({ accuracy: 100, totalSteps: 2, completedSteps: 2 }));
  });

  it('restores found targets in the current resumed round', () => {
    const engine = new ScanFindEngine();
    engine.initialize({
      currentRound: 0,
      scanningRounds: [
        { textContent: 'one two', targets: ['one', 'two'], foundTargets: ['one'] }
      ]
    } as any, callbacks(() => undefined));

    expect(engine.state.currentStep).toBe(1);
    expect(engine.getWords()[0].found).toBeTrue();
  });
});
