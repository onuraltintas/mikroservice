import { applyCustomPreviewSettings, getCustomPreviewControls } from './custom-preview-settings';
import { GridInteractionEngine } from './engines/grid-interaction.engine';
import { EngineCallbacks } from './engines/base-engine.interface';

const context = { roles: ['Teacher'], preview: true };
const callbacks: EngineCallbacks = {
  onStart: () => undefined, onPause: () => undefined, onResume: () => undefined,
  onComplete: () => undefined, onError: () => undefined,
  onStateChange: () => undefined, onStepComplete: () => undefined, onAction: () => undefined
};

describe('Remaining custom preview motors', () => {
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
});
