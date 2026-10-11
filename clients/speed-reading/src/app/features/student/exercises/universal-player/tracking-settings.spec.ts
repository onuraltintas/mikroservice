import { applyCustomPreviewSettings, getCustomPreviewControls } from './custom-preview-settings';

describe('Tracking custom settings', () => {
  it('applies a path-only choice', () => {
    const changed: any = applyCustomPreviewSettings({ engineType: 'motion_path', mode: 'tracking' }, { pathType: 'circle' }, { roles: ['Admin'], preview: true });
    expect(changed.engineConfig?.path?.type).toBe('circle');
  });
  it('overrides root legacy aliases rather than letting them override custom choices', () => {
    const config = { engineType: 'motion_path', mode: 'tracking', timing: { speedMs: 1250, durationMs: 30000 } };
    const changed: any = applyCustomPreviewSettings(config, { cycleDurationMs: 2000, durationSeconds: 20 }, { roles: ['Teacher'], preview: true });
    expect(changed.engineConfig.timing.speedMs).toBe(2000);
    expect(changed.engineConfig.timing.durationMs).toBe(20000);
  });
  it('offers duration, path and target size as well as speed', () => {
    const config = { engineType: 'motion_path', mode: 'tracking', timing: { durationSeconds: 30 }, path: { type: 'circle' } };
    const controls = getCustomPreviewControls(config);
    expect(controls.map(item => item.key)).toEqual(['cycleDurationMs', 'durationSeconds', 'pathType', 'pointSize']);
    const changed: any = applyCustomPreviewSettings(config, { cycleDurationMs: 2000, durationSeconds: 20, pathType: 'vertical', pointSize: 48 }, { roles: ['Admin'], preview: true });
    expect(changed.engineConfig['timing']).toEqual({ speedMs: 2000, durationSeconds: 20 });
    expect(changed.engineConfig['path']).toEqual({ type: 'vertical' });
    expect(changed.engineConfig['content']).toEqual({ pointSize: 48 });
  });
  it('keeps the exact recorded cycle in an unchanged custom preview', () => {
    const config = { engineType: 'motion_path', engineConfig: { mode: 'tracking', path: { type: 'circle' }, timing: { speedMs: 2800, durationMs: 60000 } } };
    const controls = getCustomPreviewControls(config);
    expect(controls[0].value).toBe(2800);
    const changed: any = applyCustomPreviewSettings(config, Object.fromEntries(controls.map(item => [item.key, item.value])), { roles: ['Teacher'], preview: true });
    expect(changed.engineConfig.timing.speedMs).toBe(2800);
    expect(config.engineConfig.timing.speedMs).toBe(2800);
  });
  it('rejects out-of-range cycle times', () => {
    const config = { engineType: 'motion_path', mode: 'tracking' };
    for (const value of [49, 60001, NaN, 1.5]) expect(() => applyCustomPreviewSettings(config, { cycleDurationMs: value }, { roles: ['Admin'], preview: true })).toThrow();
  });
  it('normalizes legacy millisecond durations and path names just like the player', () => {
    const controls = getCustomPreviewControls({ engineType: 'motion_path', engineConfig: {
      mode: 'tracking', timing: { durationMs: 5100 }, path: { type: 'Circle' }
    } });
    expect(controls.find(item => item.key === 'durationSeconds')?.value).toBe(6);
    expect(controls.find(item => item.key === 'pathType')?.value).toBe('circle');
  });
});
