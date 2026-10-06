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
  it('clears timed-out result metadata on reset', fakeAsync(() => {
    const results: EngineResult[] = [];
    const engine = new ScanFindEngine();
    engine.initialize({ content: { text: 'one' }, targets: { words: ['one'] }, timeLimitSeconds: 1 },
      callbacks(result => results.push(result)));
    engine.start();
    tick(1100);
    expect(results[0].details.incomplete).toBeTrue();
    engine.reset();
    engine.start();
    engine.handleWordClick(0);
    expect(results[1].details.incomplete).toBeFalse();
    engine.destroy();
  }));
  it('waits for server acknowledgement before counting or completing a target', () => {
    let result: EngineResult | undefined;
    const actions: any[] = [];
    const engine = new ScanFindEngine();
    engine.initialize({
      serverAuthoritative: true,
      scanningRounds: [{ textContent: 'ışık', targets: ['ışık'], foundTargets: [] }]
    }, { ...callbacks(value => result = value), onAction: action => actions.push(action) });
    engine.start();
    expect(actions[0].action).toBe('scan_start');
    engine.handleWordClick(0);
    expect(engine.state.currentStep).toBe(0);
    engine.reconcileServerResponse(actions[0], {
      isValid: true, isCompleted: false, feedbackData: {
        scanningRounds: [{ textContent: 'ışık', targets: ['ışık'], foundTargets: [] }], currentRound: 0,
        totalSteps: 1, correctCount: 0, incorrectCount: 0, searchTimeMs: 0
      }
    });
    engine.handleWordClick(0);
    expect(actions[1]).toEqual(jasmine.objectContaining({ action: 'scan_click', index: 0, number: 0 }));
    expect(result).toBeUndefined();
    engine.reconcileServerResponse(actions[1], {
      isValid: true, isCompleted: true, feedbackData: {
        scanningRounds: [{ textContent: 'ışık', targets: ['ışık'], foundTargets: ['ışık'] }], currentRound: 1,
        totalSteps: 1, correctCount: 1, incorrectCount: 0, searchTimeMs: 250
      }
    });
    expect(result?.completedSteps).toBe(1);
    expect(result?.totalTime).toBe(250);
    engine.destroy();
  });

  it('matches Turkish casing and surrounding punctuation', () => {
    let result: EngineResult | undefined;
    const engine = new ScanFindEngine();
    engine.initialize({
      content: { text: '“IŞIK” [İNCİ]', wordCount: 2 },
      targets: { words: ['ışık', 'inci'], caseSensitive: false }
    }, callbacks(value => result = value));
    engine.start();
    engine.handleWordClick(0);
    expect(engine.isTargetFound('ışık')).toBeTrue();
    engine.handleWordClick(1);
    expect(result?.accuracy).toBe(100);
    engine.destroy();
  });

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
