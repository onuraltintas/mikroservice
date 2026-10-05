import { resolveSchulteSettings } from './schulte-settings';

describe('Schulte settings', () => {
  it('reads nested dimensions, timing and disabled visual options', () => {
    const result = resolveSchulteSettings({ engineConfig: {
      gridSize: 7, rules: { timeLimit: 90 }, showHints: false,
      showFixationPoint: false, visuals: { highlightCorrect: false }
    } }, {});
    expect(result.gridSize).toBe(7);
    expect(result.timeLimit).toBe(90);
    expect(result.showHints).toBeFalse();
    expect(result.showFixationPoint).toBeFalse();
    expect(result.highlightOnClick).toBeFalse();
  });

  it('uses server values without losing explicit false options', () => {
    const result = resolveSchulteSettings({ gridSize: 3, timeLimit: 90 }, {
      gridSize: 6, timeLimitSeconds: 120, showHints: false
    });
    expect(result.gridSize).toBe(6);
    expect(result.timeLimit).toBe(120);
    expect(result.showHints).toBeFalse();
    expect(result.sequenceType).toBe('numeric');
  });

  it('keeps defaults and reads unified grid dimensions', () => {
    expect(resolveSchulteSettings({}, {}).gridSize).toBe(5);
    expect(resolveSchulteSettings({ engineConfig: { grid: { rows: 4, cols: 4 } } }, {}).gridSize).toBe(4);
  });
});
