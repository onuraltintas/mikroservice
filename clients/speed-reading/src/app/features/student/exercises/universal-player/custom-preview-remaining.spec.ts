import { applyCustomPreviewSettings, getCustomPreviewControls } from './custom-preview-settings';
import { GridInteractionEngine } from './engines/grid-interaction.engine';
import { VisualizationEngine } from './engines/visualization.engine';
import { AdaptiveFluencyEngine } from './engines/adaptive-fluency.engine';
import { ErrorAnalysisEngine } from './engines/error-analysis.engine';
import { EngineCallbacks } from './engines/base-engine.interface';

const context = { roles: ['Teacher'], preview: true };
const callbacks: EngineCallbacks = {
  onStart: () => undefined, onPause: () => undefined, onResume: () => undefined,
  onComplete: () => undefined, onError: () => undefined,
  onStateChange: () => undefined, onStepComplete: () => undefined, onAction: () => undefined
};

describe('Remaining custom preview motors', () => {
  it('shows guided preview time using the selected step interval', () => {
    const engine = new VisualizationEngine();
    engine.initialize({ previewOnly: true, mode: 'guided', scenes: [
      { duration: 5, steps: ['Bir', 'Iki'], stepDurationMs: 1500 }
    ] } as any, callbacks);
    engine.start();
    expect(engine.getSceneDisplayRemaining()).toBe(3);
    engine.destroy();
  });
  it('keeps guided and non-guided scene timing separate in mixed previews', () => {
    const configuration = { engineType: 'visualization', engineConfig: { mode: 'guided', scenes: [
      { duration: 5, steps: ['Bir'], stepDurationMs: 3000 }, { duration: 8 }
    ] } };
    const result = applyCustomPreviewSettings(configuration, { sceneDurationSec: 12, stepDurationMs: 1500 }, context);
    expect(result.engineConfig.scenes[0].duration).toBe(5);
    expect(result.engineConfig.scenes[0].stepDurationMs).toBe(1500);
    expect(result.engineConfig.scenes[1].duration).toBe(12);
    expect(result.engineConfig.scenes[1].stepDurationMs).toBeUndefined();
  });

  it('does not let nested server flags turn a local visualization preview into a server session', () => {
    const engine = new VisualizationEngine();
    engine.initialize({ previewOnly: true, sessionData: { serverAuthoritative: true, scenes: [] } } as any, callbacks);
    expect((engine as any).serverAuthoritative).toBeFalse();
  });
  it('changes error-analysis text size without rewriting errors or words', () => {
    const configuration = { engineType: 'error_analysis', engineConfig: {
      words: [{ index: 0, text: 'kelime' }], errors: [{ wordIndex: 0, originalWord: 'kelime' }]
    } };
    expect(getCustomPreviewControls(configuration)[0]?.value).toBe('medium');
    const result = applyCustomPreviewSettings(configuration, { fontSize: 'large', errors: [] }, context);
    const engine = new ErrorAnalysisEngine();
    engine.initialize({ ...result, ...result.engineConfig } as any, callbacks);
    expect(engine.getFontSize()).toBe('large');
    expect(result.engineConfig.words).toEqual(configuration.engineConfig.words);
    expect(result.engineConfig.errors).toEqual(configuration.engineConfig.errors);
    expect(() => applyCustomPreviewSettings(configuration, { fontSize: 'huge' }, context)).toThrow();
  });
  it('uses the selected Schulte size in the real motor without changing the catalogue board', () => {
    const configuration = { engineType: 'grid_interaction', gridSize: 5,
      engineConfig: { grid: { rows: 5, cols: 5 }, content: { title: 'Tablo' } } };
    expect(getCustomPreviewControls(configuration)[0]?.value).toBe(5);
    const result = applyCustomPreviewSettings(configuration, { gridSize: 3, grid: [[999]] }, context);
    const engine = new GridInteractionEngine();
    engine.initialize({ ...result, ...result.engineConfig } as any, callbacks);
    expect(engine.getGridSize()).toBe(3);
    expect(engine.getGrid().length).toBe(9);
    expect([...engine.getGrid()].sort((a, b) => Number(a) - Number(b))).toEqual([1, 2, 3, 4, 5, 6, 7, 8, 9]);
    expect(engine.state.totalSteps).toBe(9);
    expect(result.engineConfig.content).toEqual(configuration.engineConfig.content);
    expect(configuration.gridSize).toBe(5);
    expect(configuration.engineConfig.grid).toEqual({ rows: 5, cols: 5 });
  });

  it('resolves the nested Schulte default and validates inclusive size boundaries', () => {
    const configuration = { engineType: 'grid_interaction', engineConfig: { grid: { rows: 4 } } };
    expect(getCustomPreviewControls(configuration)[0]?.value).toBe(4);
    for (const gridSize of [3, 7]) {
      expect((applyCustomPreviewSettings(configuration, { gridSize }, context) as any).gridSize).toBe(gridSize);
    }
    for (const gridSize of [2, 8, 3.5, NaN]) {
      expect(() => applyCustomPreviewSettings(configuration, { gridSize }, context)).toThrow();
    }
    expect(applyCustomPreviewSettings(configuration, { gridSize: 3 }, { roles: ['Teacher'], preview: false })).toBe(configuration);
  });

  it('changes only visualization scene timing and preserves descriptions, IDs and questions', () => {
    const scenes = [{ sceneId: 'scene', description: 'Özgün sahne', duration: 5,
      questions: [{ questionId: 'question', questionText: 'Soru', options: ['A', 'B'] }] }];
    const configuration = { engineType: 'visualization', engineConfig: { mode: 'static', scenes } };
    expect(getCustomPreviewControls(configuration)[0]?.value).toBe(5);
    const result = applyCustomPreviewSettings(configuration, { sceneDurationSec: 12, scenes: [] }, context);
    const engine = new VisualizationEngine();
    engine.initialize({ ...result, ...result.engineConfig } as any, callbacks);
    expect(engine.getCurrentScene()?.duration).toBe(12);
    expect(engine.getCurrentScene()?.sceneId).toBe('scene');
    expect(engine.getCurrentScene()?.description).toBe('Özgün sahne');
    expect(result.engineConfig.scenes[0].questions).toEqual(scenes[0].questions);
    expect(scenes[0].duration).toBe(5);
    expect(() => applyCustomPreviewSettings(configuration, { sceneDurationSec: 0 }, context)).toThrow();
  });

  it('resolves guided visualization SessionData and changes only the effective step interval', () => {
    const configuration = { engineType: 'visualization', engineConfig: {
      SessionData: { mode: 'guided', Scenes: [{ SceneId: 'scene', Duration: 5, Steps: ['Bir', 'İki'], StepDurationMs: 3000 }] }
    } };
    expect(getCustomPreviewControls(configuration).map(control => control.key)).toEqual(['stepDurationMs']);
    const result = applyCustomPreviewSettings(configuration, { stepDurationMs: 1500, sceneDurationSec: 30 }, context);
    const engine = new VisualizationEngine();
    engine.initialize({ ...result, ...result.engineConfig } as any, callbacks);
    expect(engine.getCurrentScene()?.stepDurationMs).toBe(1500);
    expect(engine.getCurrentScene()?.duration).toBe(5);
    expect(engine.getCurrentScene()?.steps).toEqual(['Bir', 'İki']);
    expect(() => applyCustomPreviewSettings(configuration, { stepDurationMs: 99 }, context)).toThrow();
  });

  it('keeps an adaptive preview target through local stages without altering stage or content', () => {
    const configuration = { engineType: 'adaptive_fluency', engineConfig: {
      sessionData: { adaptiveStage: 0, adaptiveTargetWpm: 200, content: 'Özgün metin', questions: [{ id: 'question' }] }
    } };
    expect(getCustomPreviewControls(configuration)[0]?.value).toBe(200);
    const result = applyCustomPreviewSettings(configuration, { adaptiveTargetWpm: 400, adaptiveStage: 3, content: 'Değiştir' }, context);
    const engine = new AdaptiveFluencyEngine();
    engine.initialize({ ...result, ...result.engineConfig, previewOnly: true } as any, callbacks);
    expect(engine.getTargetWpm()).toBe(400);
    expect(engine.getStage()).toBe(0);
    expect(engine.getText()).toBe('Özgün metin');
    expect(engine.getQuestions()).toEqual([{ id: 'question' }]);
    engine.applyStage({ stage: 1 });
    expect(engine.getTargetWpm()).toBe(400);
    engine.applyStage({ stage: 2 });
    expect(engine.getTargetWpm()).toBe(400);
    expect(configuration.engineConfig.sessionData.adaptiveTargetWpm).toBe(200);
  });

  it('does not retain a manual adaptive target in server-owned sessions and rejects invalid bounds', () => {
    const configuration = { engineType: 'adaptive_fluency', engineConfig: { adaptiveTargetWpm: 200 } };
    const engine = new AdaptiveFluencyEngine();
    engine.initialize({ ...configuration, previewOnly: false } as any, callbacks);
    engine.applyStage({ stage: 1, targetWpm: 300 });
    expect(engine.getTargetWpm()).toBe(300);
    engine.applyStage({ stage: 2 });
    expect(engine.getTargetWpm()).toBeUndefined();
    for (const adaptiveTargetWpm of [19, 1501, Infinity]) {
      expect(() => applyCustomPreviewSettings(configuration, { adaptiveTargetWpm }, context)).toThrow();
    }
  });
});
