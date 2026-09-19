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
});
