import { applyCustomPreviewSettings, getCustomPreviewControls } from './custom-preview-settings';

describe('Tracking custom settings', () => {
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
