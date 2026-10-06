import { fakeAsync, tick } from '@angular/core/testing';
import { EngineCallbacks, EngineResult } from './base-engine.interface';
import { SubvocalizationReductionEngine } from './subvocalization-reduction.engine';

describe('subvocalization measurement', () => {
  const noOp = () => undefined;
  function withQuestion(onComplete: (result: EngineResult) => void = noOp): SubvocalizationReductionEngine {
    const engine = new SubvocalizationReductionEngine();
    engine.initialize({ readingTextContent: 'bir', wpm: 600,
      questions: [{ id: 'q1', correctAnswer: 'B' }] } as any, {
      onStart: noOp, onPause: noOp, onResume: noOp, onComplete, onError: noOp,
      onStateChange: noOp, onStepComplete: noOp, onAction: noOp
    });
    return engine;
  }

  it('ignores client answer keys and incomplete validation responses', fakeAsync(() => {
    const engine = withQuestion();
    engine.start(); tick(100);
    engine.handleInput({ type: 'answer', answer: 'B' });
    expect(engine.showingFeedback).toBeFalse();
    engine.handleInput({ type: 'answer', answer: 'A', serverValidated: true });
    expect(engine.showingFeedback).toBeFalse();
    engine.destroy();
  }));

  it('scores only server-confirmed answers without emitting a second answer action', fakeAsync(() => {
    let result: EngineResult | undefined;
    const engine = withQuestion(value => result = value);
    engine.start(); tick(100);
    engine.handleInput({ type: 'answer', answer: 'A', serverValidated: true, isCorrect: true, correctAnswer: 'A' });
    expect(engine.showingFeedback).toBeTrue();
    engine.nextQuestion();
    expect(result!.score).toBe(100);
    expect(result!.details.answers[0].questionId).toBe('q1');
    expect(result!.details.measurementStatus).toBe('Measured');
    expect(result!.details.wpm).toBeUndefined();
    engine.destroy();
  }));

  it('allows preview answers without claiming measured comprehension', fakeAsync(() => {
    let result: EngineResult | undefined;
    const engine = withQuestion(value => result = value);
    engine.start(); tick(100);
    engine.handleInput({ type: 'answer', answer: 'A', previewOnly: true });
    engine.nextQuestion();
    expect(result!.details.comprehensionScore).toBeNull();
    expect(result!.details.measurementStatus).toBe('NotMeasured');
    engine.destroy();
  }));

  function createEngine(text: string, chunkSize = 1): SubvocalizationReductionEngine {
    const engine = new SubvocalizationReductionEngine();
    engine.initialize({ readingTextContent: text, wpm: 600, chunkSize } as any, {
      onStart: () => undefined, onPause: () => undefined, onResume: () => undefined,
      onComplete: () => undefined, onError: () => undefined,
      onStateChange: () => undefined, onStepComplete: () => undefined, onAction: () => undefined
    });
    return engine;
  }

  it('shows the first group immediately and the partial final group for its actual duration', fakeAsync(() => {
    const engine = createEngine('bir iki uc', 2);
    engine.start();
    expect(engine.getCurrentWordIndex()).toBe(1);
    expect(engine.getCurrentChunk()).toBe('bir iki');
    tick(200);
    expect(engine.getCurrentChunk()).toBe('uc');
    expect(engine.state.isCompleted).toBeFalse();
    tick(100);
    expect(engine.state.isCompleted).toBeTrue();
    expect(engine.state.timeElapsed).toBe(300);
    engine.destroy();
  }));

  it('preserves remaining word exposure while paused', fakeAsync(() => {
    const engine = createEngine('bir iki');
    engine.start();
    tick(60); engine.pause(); tick(1000); engine.resume();
    tick(39);
    expect(engine.getCurrentWordIndex()).toBe(0);
    tick(1);
    expect(engine.getCurrentWordIndex()).toBe(1);
    tick(100);
    expect(engine.state.timeElapsed).toBe(200);
    engine.destroy();
  }));

  it('reports display pace without fabricating reading speed or comprehension', fakeAsync(() => {
    let result: EngineResult | undefined;
    const callbacks: EngineCallbacks = {
      onStart: () => undefined, onPause: () => undefined, onResume: () => undefined,
      onComplete: value => result = value, onError: () => undefined,
      onStateChange: () => undefined, onStepComplete: () => undefined, onAction: () => undefined
    };
    const engine = new SubvocalizationReductionEngine();
    engine.initialize({ readingTextContent: 'bir iki', wpm: 600 } as any, callbacks);
    engine.start();
    tick(1000);
    expect(result).toBeDefined();
    expect(result!.details.wpm).toBeUndefined();
    expect(result!.details.displayPaceWpm).toBe(600);
    expect(result!.details.comprehensionScore).toBeNull();
    expect(result!.details.measurementStatus).toBe('NotMeasured');
    expect(result!.score).toBe(0);
    expect(result!.accuracy).toBe(0);
    engine.destroy();
  }));
});
