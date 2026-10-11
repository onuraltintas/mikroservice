import { buildGroupingBoundaries } from './grouping-text';
import { WordHighlightEngine } from './word-highlight.engine';
import { fakeAsync, tick } from '@angular/core/testing';

describe('Grouping text boundaries', () => {
  for (const [text, size, ends, breaks] of [
    ['Bir iki. Üç dört beş! Altı yedi?', 3, [2, 5, 7], [false, false, false]],
    ['Bir iki üç\n\ndört beş altı yedi', 2, [2, 3, 5, 7], [false, true, false, false]],
    ['Dr. Deniz 12.04.2026 tarihinde 3.5 litre aldı.', 3, [3, 6, 7], [false, false, false]],
    ['“Bir iki!” Üç dört.', 3, [2, 4], [false, false]],
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
});
