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
});
