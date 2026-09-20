import { ErrorAnalysisEngine } from './error-analysis.engine';

function callbacks(onComplete: (result: any) => void = () => undefined) {
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

describe('ErrorAnalysisEngine', () => {
  it('reads words and errors from nested engine configuration', () => {
    const engine = new ErrorAnalysisEngine();
    engine.initialize({
      engineConfig: {
        textWithErrors: 'yanlız bugün',
        originalText: 'yalnız bugün',
        words: [{ index: 0, text: 'yanlız' }, { index: 1, text: 'bugün' }],
        errors: [{ wordIndex: 0, originalWord: 'yalnız', errorWord: 'yanlız' }]
      }
    } as any, callbacks());

    expect(engine.getTextWithErrors()).toBe('yanlız bugün');
    expect(engine.getWords().length).toBe(2);
    expect(engine.getErrorCount()).toBe(1);
  });

  it('ignores invalid word positions', () => {
    const engine = new ErrorAnalysisEngine();
    engine.initialize({
      words: [{ index: 0, text: 'kelime' }],
      errors: [{ wordIndex: 0, originalWord: 'kelime', errorWord: 'kelimee' }]
    }, callbacks());
    engine.start();

    engine.handleInput({ type: 'select_word', wordIndex: 99 });

    expect(engine.getFalseAlarmCount()).toBe(0);
    expect(engine.state.accuracy).toBe(100);
    engine.destroy();
  });

  it('completes only once and resets hint usage', () => {
    let completions = 0;
    const engine = new ErrorAnalysisEngine();
    engine.initialize({
      words: [{ index: 0, text: 'yanlız' }],
      errors: [{ wordIndex: 0, originalWord: 'yalnız', errorWord: 'yanlız' }]
    }, callbacks(() => completions++));
    engine.start();
    engine.useHint();

    engine.forceComplete();
    engine.forceComplete();
    engine.reset();

    expect(completions).toBe(1);
    expect(engine.getHintUsedCount()).toBe(0);
  });
});
