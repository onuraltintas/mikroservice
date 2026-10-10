import { fakeAsync, tick } from '@angular/core/testing';
import { WordHighlightEngine } from './word-highlight.engine';
import { EngineCallbacks } from './base-engine.interface';

describe('Fixed-pace Speed Reading practice', () => {
  const callbacks: EngineCallbacks = {
    onStart: () => undefined, onPause: () => undefined, onResume: () => undefined,
    onComplete: () => undefined, onError: () => undefined, onStateChange: () => undefined,
    onStepComplete: () => undefined, onAction: () => undefined
  };
  it('requires real content rather than a static fallback', () => {
    expect(() => new WordHighlightEngine().initialize({ engineConfig: { mode: 'speed_reading' } } as any, callbacks))
      .toThrowError(/reading text/i);
  });
  it('highlights one word at fixed pace until the text ends, excluding pauses', fakeAsync(() => {
    let result: any;
    const engine = new WordHighlightEngine();
    engine.initialize({ engineConfig: { mode: 'speed_reading', content: { text: 'bir iki üç dört' },
      pacer: { speedWpm: 100, chunkSize: 3 }, timing: { timeLimitSec: 1 }, adaptive: { enabled: true } } } as any,
      { ...callbacks, onComplete: value => result = value });
    expect(engine.getChunks().length).toBe(4);
    engine.start();
    tick(600);
    expect(engine.getCurrentChunkIndex()).toBe(1);
    engine.pause();
    tick(3000);
    engine.resume();
    tick(1770);
    expect(result).toBeUndefined();
    tick(60);
    expect(result.details.displayPaceWpm).toBe(100);
    expect(result.details.wordCount).toBe(4);
    expect(result.details.completionPercent).toBe(100);
    expect(result.details.wpm).toBeNull();
    expect(result.totalTime).toBeLessThan(2500);
    engine.destroy();
  }));
});
