import { EngineCallbacks } from './base-engine.interface';
import { TextFadeEngine } from './text-fade.engine';
import { TextStreamEngine } from './text-stream.engine';
import { WordHighlightEngine } from './word-highlight.engine';

const callbacks: EngineCallbacks = {
  onStart: () => undefined,
  onPause: () => undefined,
  onResume: () => undefined,
  onComplete: () => undefined,
  onError: () => undefined,
  onStateChange: () => undefined,
  onStepComplete: () => undefined,
  onAction: () => undefined
};

describe('reading pacer runtime safety', () => {
  it('clamps text stream duration and content count from legacy configuration', () => {
    const engine = new TextStreamEngine();

    engine.initialize({
      DisplayDurationMs: 1,
      TotalStimuli: 10_000,
      Words: Array.from({ length: 600 }, (_, index) => `word-${index}`)
    } as any, callbacks);

    expect(engine.getCurrentDuration()).toBe(50);
    expect(engine.state.totalSteps).toBe(500);
  });

  it('clamps text fade WPM even for legacy active configuration', () => {
    const engine = new TextFadeEngine();

    engine.initialize({ TargetWpm: 100_000, LagMs: -1 } as any, callbacks);

    expect(engine.getWpm()).toBe(1500);
  });

  it('clamps word highlight WPM and chunk size before building chunks', () => {
    const engine = new WordHighlightEngine();

    engine.initialize({
      TargetWpm: 100_000,
      ChunkSize: 999,
      content: { text: Array.from({ length: 30 }, (_, index) => `word-${index}`).join(' ') }
    } as any, callbacks);

    expect(engine.getWpm()).toBe(1500);
    expect(engine.getChunkSize()).toBe(10);
  });

  it('ignores malformed legacy chunk entries instead of crashing', () => {
    const engine = new WordHighlightEngine();

    expect(() => engine.initialize({ Chunks: [1, null, 'valid words'] } as any, callbacks))
      .not.toThrow();
    expect(engine.state.totalSteps).toBe(1);
  });
});
