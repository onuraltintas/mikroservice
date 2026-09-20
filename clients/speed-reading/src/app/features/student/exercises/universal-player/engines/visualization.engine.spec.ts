import { VisualizationEngine } from './visualization.engine';

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
});
