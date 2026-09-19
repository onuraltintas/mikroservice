import { ReadingComprehensionEngine } from './reading-comprehension.engine';
import { fakeAsync, tick } from '@angular/core/testing';
import { EngineCallbacks, EngineResult } from './base-engine.interface';
import { EngineFactory } from './engine-factory';

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

  it('uses nested and case-insensitive timing and display configuration', () => {
    const engine = new ReadingComprehensionEngine();
    engine.initialize({
      content: 'Bir iki.',
      engineConfig: {
        TIMING: { MINREADINGTIMEMS: 750 },
        DISPLAY: { FONTSIZE: 'large', LINEHEIGHT: 2.2 }
      }
    } as any, callbacks(() => undefined));

    expect(engine.getMinReadingTime()).toBe(750);
    expect(engine.getFontSize()).toBe('large');
    expect(engine.getLineHeight()).toBe(2.2);
  });

  it('uses one consistent bounded step count in state and result', fakeAsync(() => {
    let result: EngineResult | undefined;
    const engine = new ReadingComprehensionEngine();
    engine.initialize({ content: 'Bir iki.', wordCount: 4 }, callbacks(value => result = value));

    engine.start();
    tick(100);
    engine.handleInput({ action: 'complete_reading' });

    expect(engine.state.currentStep).toBe(4);
    expect(result).toEqual(jasmine.objectContaining({ totalSteps: 4, completedSteps: 4 }));
  }));

  it('treats zero minimum reading time consistently', () => {
    const engine = new ReadingComprehensionEngine();
    engine.initialize({ content: 'Bir iki.', timing: { minReadingTimeMs: 0 } }, callbacks(() => undefined));

    expect(engine.canComplete()).toBeTrue();
  });

  it('normalizes malformed text and word count without throwing', () => {
    const engine = new ReadingComprehensionEngine();

    expect(() => engine.initialize({
      readingTextContent: {} as any,
      content: { text: {} as any },
      wordCount: Number.MAX_SAFE_INTEGER
    }, callbacks(() => undefined))).not.toThrow();
    expect(engine.state.totalSteps).toBeLessThanOrEqual(100000);
  });

  it('keeps the free reading runtime identity', () => {
    expect(EngineFactory.create('free_reading')?.engineType).toBe('free_reading');
  });

  it('reports the reading phase as unscored', fakeAsync(() => {
    let result: EngineResult | undefined;
    const engine = new ReadingComprehensionEngine();
    engine.initialize({ content: 'Bir iki.' }, callbacks(value => result = value));
    engine.start();
    tick(100);
    engine.handleInput({ action: 'complete_reading' });

    expect(result).toEqual(jasmine.objectContaining({ score: 0, accuracy: 0 }));
  }));

  it('merges root text with nested content metadata', () => {
    const engine = new ReadingComprehensionEngine();
    engine.initialize({
      content: 'Bir iki üç.',
      engineConfig: { content: { wordCount: 3 } }
    } as any, callbacks(() => undefined));

    expect(engine.getText()).toBe('Bir iki üç.');
    expect(engine.state.totalSteps).toBe(3);
  });
});
