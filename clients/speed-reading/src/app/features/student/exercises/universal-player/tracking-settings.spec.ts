import { applyCustomPreviewSettings, getCustomPreviewControls } from './custom-preview-settings';

describe('Tracking custom settings', () => {
  it('applies a path-only choice', () => {
    const changed: any = applyCustomPreviewSettings({ engineType: 'motion_path', mode: 'tracking' }, { pathType: 'circle' }, { roles: ['Admin'], preview: true });
    expect(changed.engineConfig?.path?.type).toBe('circle');
  });
  it('overrides root legacy aliases rather than letting them override custom choices', () => {
    const config = { engineType: 'motion_path', mode: 'tracking', timing: { speedMs: 1250, durationMs: 30000 } };
    const changed: any = applyCustomPreviewSettings(config, { speedLevel: 4, durationSeconds: 20 }, { roles: ['Teacher'], preview: true });
    expect(changed.engineConfig.timing.speedMs).toBe(0);
    expect(changed.engineConfig.timing.durationMs).toBe(20000);
  });
  it('offers duration, path and target size as well as speed', () => {
    const config = { engineType: 'motion_path', mode: 'tracking', timing: { durationSeconds: 30 }, path: { type: 'circle' } };
    const controls = getCustomPreviewControls(config);
    expect(controls.map(item => item.key)).toEqual(['speedLevel', 'durationSeconds', 'pathType', 'pointSize']);
    const changed: any = applyCustomPreviewSettings(config, { speedLevel: 4, durationSeconds: 20, pathType: 'vertical', pointSize: 48 }, { roles: ['Admin'], preview: true });
    expect(changed.engineConfig['timing']).toEqual({ durationSeconds: 20 });
    expect(changed.engineConfig['path']).toEqual({ type: 'vertical' });
    expect(changed.engineConfig['content']).toEqual({ pointSize: 48 });
  });
});
