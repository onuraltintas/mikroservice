import { ErrorAnalysisEngine } from './error-analysis.engine';
import { fakeAsync, tick } from '@angular/core/testing';

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
  it('rejects empty or inconsistent error targets before starting', () => {
    const errors: string[] = []; const cb: any = callbacks(); cb.onError = (error: string) => { errors.push(error); };
    const engine = new ErrorAnalysisEngine();
    engine.initialize({ words: [{ index: 0, text: 'metin' }], errors: [{ wordIndex: 9 }] } as any, cb);
    engine.start(); expect(engine.state.isRunning).toBeFalse(); expect(errors.length).toBe(1); engine.destroy();
  });
  it('does not permit hints or completion while paused', () => {
    let completions = 0; const engine = new ErrorAnalysisEngine();
    engine.initialize({ words: [{ index: 0, text: 'yanlış' }], errors: [{ wordIndex: 0, originalWord: 'doğru', errorWord: 'yanlış' }] }, callbacks(() => completions++));
    engine.start(); engine.pause(); expect(engine.useHint()).toBeNull(); engine.forceComplete();
    expect(completions).toBe(0); engine.destroy(); expect(engine.state.isRunning).toBeFalse();
  });
  it('clears old timers on initialization and cannot restart completed work', fakeAsync(() => {
    const engine = new ErrorAnalysisEngine(); const config = { words: [{ index: 0, text: 'yanlış' }], errors: [{ wordIndex: 0, originalWord: 'doğru', errorWord: 'yanlış' }] };
    engine.initialize(config, callbacks()); engine.start(); tick(500); engine.initialize(config, callbacks()); tick(500);
    expect(engine.state.timeElapsed).toBe(0); engine.start(); engine.forceComplete(); engine.start(); expect(engine.state.isRunning).toBeFalse(); engine.destroy();
  }));
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

  it('reports zero accuracy when completed without any selections', () => {
    let result: any;
    const engine = new ErrorAnalysisEngine();
    engine.initialize({
      words: [{ index: 0, text: 'yanlız' }],
      errors: [{ wordIndex: 0, originalWord: 'yalnız', errorWord: 'yanlız' }]
    }, callbacks(value => result = value));
    engine.start();

    engine.forceComplete();

    expect(result.score).toBe(0);
    expect(result.accuracy).toBe(0);
    expect(result.details.missedErrors).toBe(1);
  });
});
