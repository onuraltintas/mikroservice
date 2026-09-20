import { fakeAsync, tick } from '@angular/core/testing';
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
  it('reads text stream data from nested engine configuration', () => {
    const engine = new TextStreamEngine();

    engine.initialize({
      engineConfig: {
        mode: 'rsvp',
        timing: { durationMs: 120, intervalMs: 25 },
        content: { count: 2, source: 'custom', items: ['bir', 'iki'] }
      }
    } as any, callbacks);

    expect(engine.getMode()).toBe('rsvp');
    expect(engine.getCurrentDuration()).toBe(120);
    expect(engine.state.totalSteps).toBe(2);
  });

  it('reads text fade data from nested engine configuration', () => {
    const engine = new TextFadeEngine();

    engine.initialize({
      engineConfig: {
        content: { text: 'bir iki üç' },
        fading: { speedWpm: 360, lagMs: 500 }
      }
    } as any, callbacks);

    expect(engine.getWords()).toEqual(['bir', 'iki', 'üç']);
    expect(engine.getWpm()).toBe(360);
  });

  it('reads word highlight data from nested engine configuration', () => {
    const engine = new WordHighlightEngine();

    engine.initialize({
      engineConfig: {
        content: { text: 'bir iki üç dört' },
        pacer: { speedWpm: 480, chunkSize: 2 }
      }
    } as any, callbacks);

    expect(engine.getWords()).toEqual(['bir', 'iki', 'üç', 'dört']);
    expect(engine.getWpm()).toBe(480);
    expect(engine.state.totalSteps).toBe(2);
  });

  it('starts stream and highlight engines only once', fakeAsync(() => {
    let starts = 0;
    const trackedCallbacks = { ...callbacks, onStart: () => starts++ };
    const stream = new TextStreamEngine();
    const highlight = new WordHighlightEngine();
    stream.initialize({ Words: ['bir'] } as any, trackedCallbacks);
    highlight.initialize({ content: { text: 'bir iki' } } as any, trackedCallbacks);

    stream.start();
    stream.start();
    highlight.start();
    highlight.start();

    expect(starts).toBe(2);
    stream.destroy();
    highlight.destroy();
  }));

  it('completes each reading pacer engine only once', () => {
    let completions = 0;
    const trackedCallbacks = { ...callbacks, onComplete: () => completions++ };
    const stream = new TextStreamEngine();
    const fade = new TextFadeEngine();
    const highlight = new WordHighlightEngine();
    stream.initialize({ Words: ['bir'] } as any, trackedCallbacks);
    fade.initialize({ content: { text: 'bir' } } as any, trackedCallbacks);
    highlight.initialize({ content: { text: 'bir' } } as any, trackedCallbacks);

    stream.finish();
    stream.finish();
    (fade as any).complete();
    (fade as any).complete();
    (highlight as any).complete();
    (highlight as any).complete();

    expect(completions).toBe(3);
  });

  it('preserves nested visual options after case-insensitive merging', () => {
    const stream = new TextStreamEngine();
    const fade = new TextFadeEngine();
    const highlight = new WordHighlightEngine();

    stream.initialize({ engineConfig: { visuals: { fontSize: 'xlarge', showFixation: false } } } as any, callbacks);
    fade.initialize({ engineConfig: { visuals: { fontSize: 'large' } } } as any, callbacks);
    highlight.initialize({ engineConfig: { visuals: { fontSize: 'small' } } } as any, callbacks);

    expect(stream.getFontSize()).toBe('xlarge');
    expect((stream as any).config.visuals.showFixation).toBeFalse();
    expect(fade.getFontSize()).toBe('large');
    expect(highlight.getHighlightFontSize()).toBe('small');
  });

  it('pauses the text fade startup lag', fakeAsync(() => {
    const engine = new TextFadeEngine();
    engine.initialize({ content: { text: 'bir iki' }, fading: { speedWpm: 60, lagMs: 1000 } } as any, callbacks);

    engine.start();
    tick(400);
    engine.pause();
    tick(1000);

    expect(engine.state.countdown).toBeGreaterThan(0);
    expect(engine.getFadedIndex()).toBe(-1);
    engine.destroy();
  }));

  it('records partial progress instead of full success when word highlight times out', fakeAsync(() => {
    let result: any;
    const trackedCallbacks = { ...callbacks, onComplete: (value: any) => result = value };
    const engine = new WordHighlightEngine();
    engine.initialize({
      content: { text: 'bir iki üç dört' },
      pacer: { speedWpm: 20, chunkSize: 1 },
      timing: { timeLimitSec: 1 }
    } as any, trackedCallbacks);

    engine.start();
    tick(1100);

    expect(result.completedSteps).toBe(0);
    expect(result.totalSteps).toBe(4);
    expect(result.score).toBe(0);
    engine.destroy();
  }));

  it('excludes paused time from text stream response time', fakeAsync(() => {
    const engine = new TextStreamEngine();
    engine.initialize({
      mode: 'tachistoscope',
      Words: ['bir'],
      DisplayDurationMs: 50,
      visuals: { showFixation: false }
    } as any, callbacks);

    engine.start();
    tick(50);
    engine.pause();
    tick(1000);
    engine.resume();
    engine.handleInput({ answer: 'bir' });

    expect(engine.getLastTrialResult()?.responseTimeMs).toBe(0);
    engine.destroy();
  }));

  it('cancels delayed text stream completion when destroyed', fakeAsync(() => {
    let completions = 0;
    const trackedCallbacks = { ...callbacks, onComplete: () => completions++ };
    const engine = new TextStreamEngine();
    engine.initialize({
      mode: 'tachistoscope',
      Words: ['bir'],
      DisplayDurationMs: 50,
      visuals: { showFixation: false }
    } as any, trackedCallbacks);

    engine.start();
    tick(50);
    engine.handleInput({ answer: 'bir' });
    engine.destroy();
    tick(500);

    expect(completions).toBe(0);
  }));

  it('does not restart text fade after completion without reset', () => {
    let starts = 0;
    const trackedCallbacks = { ...callbacks, onStart: () => starts++ };
    const engine = new TextFadeEngine();
    engine.initialize({ content: { text: 'bir' } } as any, trackedCallbacks);

    (engine as any).complete();
    engine.start();

    expect(starts).toBe(0);
    engine.destroy();
  });

  it('records text stream timeout coverage instead of full success', () => {
    let result: any;
    const trackedCallbacks = { ...callbacks, onComplete: (value: any) => result = value };
    const engine = new TextStreamEngine();
    engine.initialize({ mode: 'rsvp', Words: ['bir', 'iki', 'üç', 'dört'] } as any, trackedCallbacks);

    engine.finish();

    expect(result.completedSteps).toBe(0);
    expect(result.totalSteps).toBe(4);
    expect(result.score).toBe(0);
    expect(result.details.timedOut).toBeTrue();
  });

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

  it('normalizes malformed nested containers', () => {
    const fade = new TextFadeEngine();
    const highlight = new WordHighlightEngine();
    const stream = new TextStreamEngine();

    expect(() => fade.initialize({ fading: 'invalid' } as any, callbacks)).not.toThrow();
    expect(() => highlight.initialize({ pacer: 'invalid', content: 'invalid' } as any, callbacks)).not.toThrow();
    expect(() => stream.initialize({ timing: 'invalid', content: 'invalid' } as any, callbacks)).not.toThrow();
  });

  it('preserves the normalized text fade lag when starting', fakeAsync(() => {
    const engine = new TextFadeEngine();
    engine.initialize({ TargetWpm: 200, LagMs: 1000 } as any, callbacks);

    engine.start();

    expect((engine as any).config.fading.lagMs).toBe(1000);
    engine.destroy();
  }));

  it('clamps the text stream gap timer', () => {
    const engine = new TextStreamEngine();
    engine.initialize({ timing: { durationMs: 100, intervalMs: Number.MAX_VALUE } } as any, callbacks);

    expect((engine as any).config.timing.intervalMs).toBe(10000);
  });

  it('uses camel-case root reading text aliases', () => {
    const fade = new TextFadeEngine();
    const highlight = new WordHighlightEngine();

    fade.initialize({ readingTextContent: 'custom fade text' } as any, callbacks);
    highlight.initialize({ readingTextContent: 'custom highlight text' } as any, callbacks);

    expect(fade.getWords()).toEqual(['custom', 'fade', 'text']);
    expect(highlight.getWords()).toEqual(['custom', 'highlight', 'text']);
  });
});
