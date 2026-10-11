import { buildGroupingBoundaries } from './grouping-text';
import { WordHighlightEngine } from './word-highlight.engine';
import { fakeAsync, tick } from '@angular/core/testing';

describe('Grouping text boundaries', () => {
  for (const [text, size, ends, breaks] of [
    ['Bir iki. Üç dört beş! Altı yedi?', 3, [2, 5, 7], [false, false, false]],
    ['Bir iki üç\n\ndört beş altı yedi', 2, [2, 3, 5, 7], [false, true, false, false]],
    ['Dr. Deniz 12.04.2026 tarihinde 3.5 litre aldı.', 3, [3, 6, 7], [false, false, false]],
    ['“Bir iki!” Üç dört.', 3, [2, 4], [false, false]],
    ['Bir ٣. Üç dört.', 3, [2, 4], [false, false]],
  ] as const) {
    it(`preserves boundaries: ${text}`, () => {
      const boundaries = buildGroupingBoundaries(text, size);
      expect(boundaries.map(group => group.endIndex)).toEqual([...ends]);
      expect(boundaries.map(group => group.hasNewline)).toEqual([...breaks]);
    });
  }

  it('rejects missing real text in chunking preview', () => {
    expect(() => new WordHighlightEngine().initialize({ mode: 'chunking' } as any, {} as any)).toThrow();
  });

  it('uses backend whitespace semantics in canonical grouping snapshots', () => {
    const engine = new WordHighlightEngine();
    engine.initialize({ mode: 'chunking', serverAuthoritative: true,
      content: 'bir\u0085iki\uFEFFüç dört', groupingVersion: 1, groupingChunkSize: 2,
      groupingChunks: [{ endIndex: 2, hasNewline: false }, { endIndex: 3, hasNewline: false }] } as any, {} as any);
    expect(engine.getChunks().map(group => group.words)).toEqual([['bir', 'iki\uFEFFüç'], ['dört']]);
  });

  it('uses actual group size for timing and excludes pauses', fakeAsync(() => {
    const engine = new WordHighlightEngine();
    const complete = jasmine.createSpy('complete');
    engine.initialize({ mode: 'chunking', content: { text: 'Bir. İki üç dört.' },
      pacer: { chunkSize: 3, speedWpm: 120 } } as any,
      { onStart() {}, onStateChange() {}, onStepComplete() {}, onPause() {}, onResume() {}, onComplete: complete } as any);
    expect(engine.getChunks().map(group => group.words.length)).toEqual([1, 3]);
    engine.start();
    tick(510);
    expect(engine.getCurrentChunkIndex()).toBe(1);
    engine.pause(); tick(3000); engine.resume();
    tick(1510);
    expect(complete).toHaveBeenCalledTimes(1);
    expect(complete.calls.mostRecent().args[0].totalTime).toBeLessThan(2100);
    engine.destroy();
  }));

  it('preserves valid legacy server snapshots with fixed group boundaries', () => {
    const engine = new WordHighlightEngine();
    engine.initialize({ mode: 'chunking', serverAuthoritative: true,
      content: 'Bir. İki üç dört.', pacer: { chunkSize: 3, speedWpm: 120 } } as any, {} as any);
    expect(engine.getChunks().map(group => group.words.length)).toEqual([3, 1]);
  });

  it('preserves fractional legacy server pace rather than rounding session timing', () => {
    const engine = new WordHighlightEngine();
    engine.initialize({ mode: 'chunking', serverAuthoritative: true, content: 'bir iki üç dört',
      groupingChunkSize: 2, groupingDisplayPaceWpm: 60000 * 2 / 1300,
      pacer: { chunkSize: 2, speedWpm: 900 } } as any, {} as any);
    expect(engine.getWpm()).toBe(60000 * 2 / 1300);
  });

  it('rejects malformed canonical snapshots instead of rebuilding from current catalog', () => {
    const config = { mode: 'chunking', serverAuthoritative: true, content: 'bir iki üç dört',
      groupingVersion: 1, groupingChunkSize: 2, groupingDisplayPaceWpm: 200,
      groupingChunks: [{ endIndex: 3, hasNewline: false }, { endIndex: 4, hasNewline: false }] };
    expect(() => new WordHighlightEngine().initialize(config as any, {} as any)).toThrow();
  });
});
