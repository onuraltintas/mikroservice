import { fakeAsync, tick } from '@angular/core/testing';
import { EngineCallbacks, EngineResult } from './base-engine.interface';
import { ExamSimulationEngine } from './exam-simulation.engine';

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

describe('ExamSimulationEngine', () => {
  it('rejects an assessment session without server-provided text', () => {
    const engine = new ExamSimulationEngine();

    expect(() => engine.initialize({ isAssessmentMode: true } as any, callbacks(() => undefined)))
      .toThrowError(/text/i);
  });

  it('enforces minimum reading time and completes once', fakeAsync(() => {
    let completions = 0;
    const engine = new ExamSimulationEngine();
    engine.initialize({
      content: { text: 'Sınav metni burada.' },
      timing: { minReadingTimeMs: 1000 }
    }, callbacks(() => completions++));

    engine.start();
    tick(500);
    engine.handleInput({ action: 'complete_reading' });
    expect(completions).toBe(0);
    tick(500);
    engine.handleInput({ action: 'complete_reading' });
    engine.handleInput({ action: 'complete_reading' });
    expect(completions).toBe(1);
  }));

  it('does not use a question prompt as the assessment reading body', () => {
    const engine = new ExamSimulationEngine();

    expect(() => engine.initialize({
      isAssessmentMode: true,
      questions: [{ questionText: 'Soru metni okuma parçası değildir.' }]
    } as any, callbacks(() => undefined))).toThrowError(/text/i);
  });

  it('accepts string content and nested timing from the server contract', () => {
    const engine = new ExamSimulationEngine();
    engine.initialize({
      content: 'Sunucunun sınav okuma metni.',
      engineConfig: { timing: { minReadingTimeMs: 700 } }
    } as any, callbacks(() => undefined));

    expect(engine.getText()).toBe('Sunucunun sınav okuma metni.');
  });

  it('normalizes malformed text and reports reading as unscored', fakeAsync(() => {
    let result: EngineResult | undefined;
    const engine = new ExamSimulationEngine();
    expect(() => engine.initialize({
      readingTextContent: {} as any,
      wordCount: -3
    }, callbacks(value => result = value))).not.toThrow();

    engine.start();
    tick(100);
    engine.handleInput({ action: 'complete_reading' });
    expect(engine.state.totalSteps).toBeGreaterThan(0);
    expect(result).toEqual(jasmine.objectContaining({ score: 0, accuracy: 0 }));
  }));

  it('merges root text with nested content metadata', () => {
    const engine = new ExamSimulationEngine();
    engine.initialize({
      content: 'Bir iki üç.',
      engineConfig: { content: { wordCount: 3 } }
    } as any, callbacks(() => undefined));

    expect(engine.getText()).toBe('Bir iki üç.');
    expect(engine.state.totalSteps).toBe(3);
  });

  it('normalizes a case-insensitive font size accepted by the server', () => {
    const engine = new ExamSimulationEngine();
    engine.initialize({ display: { fontSize: 'LARGE' }, content: 'Metin.' } as any, callbacks(() => undefined));

    expect(engine.getFontSize()).toBe('large');
  });

  it('restores the server reading clock and caps completion at its deadline', fakeAsync(() => {
    let result: EngineResult | undefined;
    const engine = new ExamSimulationEngine();
    engine.initialize({ content: 'Bir iki üç.', readingTextTitle: 'Test paragrafı',
      readingStartTime: new Date(Date.now() - 1500).toISOString(), readingMinimumMs: 300,
      readingMaximumMs: 2000, timing: { minReadingTimeMs: 10000, maxReadingTimeMs: 30000 },
      display: { fontSize: 'small', lineHeight: 2.2 } } as any, callbacks(value => result = value));
    expect(engine.getTitle()).toBe('Test paragrafı');
    expect(engine.getWords()).toEqual(['Bir', 'iki', 'üç.']);
    expect(engine.getWordCount()).toBe(3);
    expect(engine.getLineHeight()).toBe(2.2);
    expect(engine.getMaximumMs()).toBe(2000);
    engine.start(); engine.start(); tick(100);
    expect(engine.canComplete()).toBeTrue();
    tick(500);
    expect(result?.totalTime).toBe(2000);
    expect(engine.getReadingPhase()).toBe('completed');
    engine.destroy();
  }));

  it('cleans up its clock on stop, reset and destroy', fakeAsync(() => {
    const engine = new ExamSimulationEngine();
    engine.initialize({ content: 'Bir iki üç.', display: { lineHeight: 10 } } as any, callbacks(() => undefined));
    engine.start(); tick(100);
    engine.pause(); engine.pause(); tick(200);
    expect(engine.state.timeElapsed).toBe(100);
    engine.resume(); engine.resume(); tick(100);
    expect(engine.state.timeElapsed).toBe(200);
    engine.handleInput({ scrollProgress: 100 });
    expect(engine.getLineHeight()).toBe(3);
    engine.stop(); tick(100);
    expect(engine.state.isRunning).toBeFalse();
    engine.reset();
    expect(engine.state.timeElapsed).toBe(0);
    expect(engine.getReadingPhase()).toBe('reading');
    engine.start(); engine.destroy(); tick(100);
    expect(engine.state.timeElapsed).toBe(0);
  }));
});
