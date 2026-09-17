import { ReadingComprehensionEngine } from './reading-comprehension.engine';

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
});
