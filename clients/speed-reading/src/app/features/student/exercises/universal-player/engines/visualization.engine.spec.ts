import { VisualizationEngine } from './visualization.engine';
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

describe('VisualizationEngine', () => {
  const scene = {
    sceneId: 'scene-1',
    description: 'Bir sahne',
    duration: 5,
    displayOrder: 1,
    questions: [{
      questionId: 'q1', questionText: 'Ne gördün?', options: ['A', 'B'], correctAnswer: 'A', questionType: 'choice'
    }]
  };

  it('reads scenes and mode from nested engine configuration', () => {
    const engine = new VisualizationEngine();
    engine.initialize({ engineConfig: { mode: 'guided', scenes: [scene] } } as any, callbacks());

    expect(engine.mode).toBe('guided');
    expect(engine.getCurrentScene()?.sceneId).toBe('scene-1');
    expect(engine.state.totalSteps).toBe(1);
  });

  it('starts and completes only once', () => {
    let starts = 0;
    let completions = 0;
    const engine = new VisualizationEngine();
    engine.initialize({ scenes: [scene] } as any, {
      ...callbacks(() => completions++), onStart: () => starts++
    });

    engine.start();
    engine.start();
    (engine as any).complete();
    (engine as any).complete();

    expect(starts).toBe(1);
    expect(completions).toBe(1);
    engine.destroy();
  });

  it('clears answer feedback on reset and ignores malformed input', () => {
    const engine = new VisualizationEngine();
    engine.initialize({ scenes: [scene] } as any, callbacks());
    engine.start();
    (engine as any).endSceneDisplay();
    engine.handleInput({ type: 'answer', answer: 'A' });

    engine.reset();

    expect(engine.showingFeedback).toBeFalse();
    expect(engine.lastAnswer).toBe('');
    expect(() => engine.handleInput(null)).not.toThrow();
  });

  it('advances past scenes without questions instead of getting stuck', () => {
    let completions = 0;
    const engine = new VisualizationEngine();
    engine.initialize({ scenes: [{ ...scene, questions: [] }] } as any, callbacks(() => completions++));
    engine.start();

    (engine as any).endSceneDisplay();

    expect(engine.state.isCompleted).toBeTrue();
    expect(completions).toBe(1);
    engine.destroy();
  });

  it('keeps preview answers unmeasured without emitting persistence actions', () => {
    let result: any; const actions: any[] = [];
    const engine = new VisualizationEngine();
    engine.initialize({ previewOnly: true, scenes: [scene] } as any,
      { ...callbacks(value => result = value), onAction: action => actions.push(action) });
    engine.start(); engine.handleInput({ action: 'skip_scene' });
    engine.handleInput({ type: 'answer', answer: 'A' }); engine.nextQuestion();
    expect(engine.isAnswerEvaluated()).toBeFalse();
    expect(result.details.measurementStatus).toBe('NotMeasured');
    expect(actions).toEqual([]);
  });

  it('resumes a guided step with its remaining time and excludes pauses', fakeAsync(() => {
    const engine = new VisualizationEngine();
    engine.initialize({ mode: 'guided', scenes: [{ ...scene, steps: ['ilk', 'son'], stepDurationMs: 1000 }] } as any, callbacks());
    engine.start(); tick(400); engine.pause(); tick(3000); engine.resume();
    tick(599); expect(engine.getGuidedStepText()).toBe('ilk');
    tick(1); expect(engine.getGuidedStepText()).toBe('son');
    tick(1000); expect(engine.getPhase()).toBe('questions');
    expect(engine.state.timeElapsed).toBe(2000);
    engine.destroy();
  }));

  it('uses the description when a guided scene has no steps', () => {
    const engine = new VisualizationEngine();
    engine.initialize({ mode: 'guided', scenes: [scene] } as any, callbacks());
    engine.start(); expect(engine.getGuidedStepText()).toBe(scene.description); engine.destroy();
  });

  it('rejects incomplete server responses and permits a retry', () => {
    const engine = new VisualizationEngine();
    engine.initialize({ serverAuthoritative: true, scenes: [{ ...scene, questions: [{ ...scene.questions[0], correctAnswer: '' }] }] } as any, callbacks());
    engine.start(); engine.handleInput({ action: 'skip_scene' });
    engine.handleInput({ type: 'answer', answer: 'A' }); engine.applyServerResponse({});
    expect(engine.state.currentStep).toBe(0); expect(engine.isAnswerPending()).toBeFalse();
    engine.handleInput({ type: 'answer', answer: 'A' });
    engine.applyServerResponse({ isValid: true, isCorrect: true });
    expect(engine.state.currentStep).toBe(1); engine.destroy();
  });

  it('ignores answer and skip inputs while paused', () => {
    const engine = new VisualizationEngine(); engine.initialize({ scenes: [scene] } as any, callbacks());
    engine.start(); engine.pause(); engine.handleInput({ action: 'skip_scene' });
    expect(engine.getPhase()).toBe('scene'); engine.destroy();
  });
});
