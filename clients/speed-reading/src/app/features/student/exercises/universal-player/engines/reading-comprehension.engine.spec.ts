import { ReadingComprehensionEngine } from './reading-comprehension.engine';
import { fakeAsync, tick } from '@angular/core/testing';
import { EngineCallbacks, EngineResult } from './base-engine.interface';

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

describe('ReadingComprehensionEngine', () => {
  it('uses the reading snapshot when the backend sends content as a string', () => {
    const engine = new ReadingComprehensionEngine();
    engine.initialize({
      content: 'Sunucudan gelen metin burada.',
      readingTextTitle: 'Sunucu metni',
      wordCount: 4
    }, {
      onStart: () => undefined,
      onPause: () => undefined,
      onResume: () => undefined,
      onComplete: () => undefined,
      onError: () => undefined,
      onStateChange: () => undefined,
      onStepComplete: () => undefined,
      onAction: () => undefined
    });

    expect(engine.getText()).toBe('Sunucudan gelen metin burada.');
    expect(engine.getTitle()).toBe('Sunucu metni');
    expect(engine.getWordCount()).toBe(4);

    engine.destroy();
  });

  it('does not complete before the configured minimum reading time', fakeAsync(() => {
    let result: EngineResult | undefined;
    const engine = new ReadingComprehensionEngine();
    engine.initialize({
      content: 'Bir iki üç dört.',
      timing: { minReadingTimeMs: 1000 }
    }, callbacks(value => result = value));

    engine.start();
    tick(500);
    engine.handleInput({ action: 'complete_reading' });
    expect(result).toBeUndefined();
    tick(500);
    engine.handleInput({ action: 'complete_reading' });
    expect(result).toBeDefined();
  }));

  it('makes start and completion idempotent', fakeAsync(() => {
    let starts = 0;
    let completions = 0;
    const engineCallbacks = callbacks(() => completions++);
    engineCallbacks.onStart = () => starts++;
    const engine = new ReadingComprehensionEngine();
    engine.initialize({ content: 'Bir iki.' }, engineCallbacks);

    engine.start();
    engine.start();
    tick(100);
    engine.handleInput({ action: 'complete_reading' });
    engine.handleInput({ action: 'complete_reading' });

    expect(starts).toBe(1);
    expect(completions).toBe(1);
  }));

  it('preserves the initialized total step count after reset', () => {
    const engine = new ReadingComprehensionEngine();
    engine.initialize({ content: 'Bir iki.', wordCount: 4 }, callbacks(() => undefined));

    engine.reset();

    expect(engine.state.totalSteps).toBe(4);
  });

  it('normalizes malformed legacy presentation and timing values', () => {
    const engine = new ReadingComprehensionEngine();
    engine.initialize({
      content: 'Bir iki.',
      timing: { minReadingTimeMs: -10, maxReadingTimeMs: Number.MAX_SAFE_INTEGER },
      display: { fontSize: 'huge', lineHeight: -3 }
    } as any, callbacks(() => undefined));

    expect(engine.getMinReadingTime()).toBe(0);
    expect(engine.getFontSize()).toBe('medium');
    expect(engine.getLineHeight()).toBeGreaterThan(0);
  });
});
