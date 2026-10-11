import { resolveVisualExpansionSettings } from './visual-expansion-settings';

describe('visual expansion catalog settings', () => {
  it('preserves nested age/level values in preview instead of overwriting them with undefined', () => {
    expect(resolveVisualExpansionSettings({ engineConfig: {
      rounds: 30, startDegrees: 8, targetDegrees: 16, displayDurationMs: 1200
    } }, {})).toEqual({ rounds: 30, startDegrees: 8, targetDegrees: 16, displayDurationMs: 1200 });
  });
  it('uses session snapshot ahead of catalog and respects nested timing overrides', () => {
    expect(resolveVisualExpansionSettings({ rounds: 45, engineConfig: {
      displayDurationMs: 1200, timing: { durationMs: 900 }, startDegrees: 8, targetDegrees: 26
    } }, { Rounds: 6, visualExpansionStartDegrees: 10,
      visualExpansionTargetDegrees: 18, visualExpansionDisplayDurationMs: 1100 }))
      .toEqual({ rounds: 6, startDegrees: 10, targetDegrees: 18, displayDurationMs: 1100 });
    expect(resolveVisualExpansionSettings({ engineConfig: {
      rounds: 40, displayDurationMs: 1200, timing: { durationMs: 900 },
      expansion: { startDegrees: 10, targetDegrees: 20 }
    } }, {}).displayDurationMs).toBe(900);
  });
  it('retains legacy root configuration without inventing missing values', () => {
    expect(resolveVisualExpansionSettings({ rounds: 6, startDegrees: 8, targetDegrees: 16, displayDurationMs: 1100 }))
      .toEqual({ rounds: 6, startDegrees: 8, targetDegrees: 16, displayDurationMs: 1100 });
    expect(resolveVisualExpansionSettings({})).toEqual({});
  });
});
