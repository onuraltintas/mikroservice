import { applyCustomPreviewSettings, getCustomPreviewControls } from './custom-preview-settings';
import { SubvocalizationReductionEngine } from './engines/subvocalization-reduction.engine';
import { CustomPreviewDialogComponent } from './custom-preview-dialog.component';
import { TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';

describe('Custom preview settings safety', () => {
  const context = { roles: ['Admin'], preview: true };
  it('changes only supported settings without mutating saved exercise configuration', () => {
    const original = { engineType: 'word_highlight', engineConfig: { pacer: { speedWpm: 200, chunkSize: 1 }, content: { text: 'Original' } } };
    const result = applyCustomPreviewSettings(original, { speedWpm: 300, chunkSize: 2, text: 'Injected' }, context);
    expect(result.engineConfig.pacer).toEqual({ speedWpm: 300, chunkSize: 2 });
    expect(result.engineConfig.content.text).toBe('Original');
    expect(original.engineConfig.pacer.speedWpm).toBe(200);
  });
  it('rejects custom settings for students and persistent training sessions', () => {
    const config = { engineType: 'word_highlight', engineConfig: {} };
    expect(applyCustomPreviewSettings(config, { speedWpm: 300 }, { roles: ['Student'], preview: true })).toEqual(config);
    expect(applyCustomPreviewSettings(config, { speedWpm: 300 }, { roles: ['Teacher'], preview: false })).toEqual(config);
  });
  it('rejects non-finite, out-of-range and unsupported engine settings', () => {
    const config = { engineType: 'word_highlight', engineConfig: {} };
    expect(() => applyCustomPreviewSettings(config, { speedWpm: Infinity }, context)).toThrow();
    expect(() => applyCustomPreviewSettings(config, { chunkSize: 0 }, context)).toThrow();
    expect(applyCustomPreviewSettings({ engineType: 'focus' }, { speedWpm: 300 }, context)).toEqual({ engineType: 'focus' });
  });
  it('maps subvocalization speed to consistent milliseconds and supports inclusive bounds', () => {
    const result = applyCustomPreviewSettings({ engineType: 'subvocalization_reduction', engineConfig: {} as Record<string, unknown> },
      { speedWpm: 1500, chunkSize: 10 }, { roles: ['Teacher'], preview: true });
    expect(result.engineConfig['msPerWord']).toBe(40);
    expect(result.engineConfig['targetWpm']).toBe(1500);
  });
  it('supports regression speed and clears its overriding delay', () => {
    const result = applyCustomPreviewSettings({ engineType: 'regression_reduction', engineConfig: { wordDelayMs: 700, wpm: 100 } },
      { speedWpm: 300, chunkSize: 3 }, context);
    expect(result.engineConfig.wpm).toBe(300);
    expect(result.engineConfig.wordDelayMs).toBe(0);
  });
  it('supports fading speed without adding unsupported chunk controls', () => {
    const result = applyCustomPreviewSettings({ engineType: 'text_fade', engineConfig: {} as Record<string, unknown> },
      { speedWpm: 400, chunkSize: 3 }, context);
    expect(result.engineConfig['targetWpm']).toBe(400);
    expect(result.engineConfig['chunkSize']).toBeUndefined();
  });
  it('maps text stream timing without modifying stimuli or adaptive rules', () => {
    const config = { engineType: 'text_stream', engineConfig: { stimuli: ['A'], adaptive: { enabled: true } } as Record<string, unknown> };
    const result = applyCustomPreviewSettings(config, { displayDurationMs: 600, intervalMs: 150, speedWpm: 400 }, context);
    expect(result.engineConfig['displayDurationMs']).toBe(600);
    expect(result.engineConfig['timing']).toEqual({ intervalMs: 150 });
    expect(result.engineConfig['stimuli']).toEqual(['A']);
    expect(result.engineConfig['adaptive']).toEqual({ enabled: true });
    expect(result.engineConfig['targetWpm']).toBeUndefined();
    expect(() => applyCustomPreviewSettings(config, { displayDurationMs: 49 }, context)).toThrow();
  });
  it('uses only controls relevant to the motion mode', () => {
    const config = { engineType: 'motion_path', engineConfig: { mode: 'tracking', movement: { speedLevel: 1 } } };
    const result = applyCustomPreviewSettings(config, { speedLevel: 4, holdMs: 700 }, context);
    expect(result.engineConfig.movement.speedLevel).toBe(4);
    expect((result.engineConfig as Record<string, unknown>)['timing']).toBeUndefined();
    expect(() => applyCustomPreviewSettings(config, { speedLevel: 6 }, context)).toThrow();
    const fixation = applyCustomPreviewSettings({ engineType: 'motion_path', engineConfig: { mode: 'fixation' } as Record<string, unknown> }, { holdMs: 700 }, context);
    expect(fixation.engineConfig['timing']).toEqual({ holdMs: 700 });
  });
  it('sets scan time limit without changing targets', () => {
    const config = { engineType: 'scan_find', engineConfig: { timeLimitSeconds: 120, targets: { words: ['A'] } } };
    const result = applyCustomPreviewSettings(config, { timeLimitSec: 60 }, context);
    expect(result.engineConfig.timeLimitSeconds).toBe(60);
    expect(result.engineConfig.targets).toEqual(config.engineConfig.targets);
    expect(() => applyCustomPreviewSettings(config, { timeLimitSec: 0 }, context)).toThrow();
  });
  it('shows recorded defaults and resets custom inputs without saving them', () => {
    const close = jasmine.createSpy('close');
    TestBed.configureTestingModule({ providers: [
      { provide: MAT_DIALOG_DATA, useValue: { engineType: 'word_highlight', engineConfig: { pacer: { speedWpm: 320, chunkSize: 3 } } } },
      { provide: MatDialogRef, useValue: { close } }
    ] });
    const fixture = TestBed.createComponent(CustomPreviewDialogComponent);
    fixture.detectChanges();
    expect(fixture.componentInstance.speedWpm).toBe(320);
    fixture.componentInstance.speedWpm = 600;
    fixture.componentInstance.reset();
    expect(fixture.componentInstance.speedWpm).toBe(320);
    fixture.componentInstance.submit();
    expect(close).toHaveBeenCalledWith({ speedWpm: 320, chunkSize: 3 });
  });
  it('resolves legacy root and priority difficulty defaults', () => {
    expect(getCustomPreviewControls({ engineType: 'text_fade', fading: { speedWpm: 450 } })[0].value).toBe(450);
    expect(getCustomPreviewControls({ engineType: 'word_highlight', pacer: { speedWpm: 350 } })[0].value).toBe(350);
    expect(getCustomPreviewControls({ engineType: 'subvocalization_reduction', engineConfig: { targetWpm: 200, difficultySettings: { targetWpm: 500 } } })[0].value).toBe(500);
  });
  it('applies custom values to the actual motor despite priority difficulty settings', () => {
    const config = { engineType: 'subvocalization_reduction', engineConfig: { difficultySettings: { targetWpm: 100, chunkSize: 1, msPerWord: 600 } } };
    const result = applyCustomPreviewSettings(config, { speedWpm: 600, chunkSize: 4 }, context);
    const engine = new SubvocalizationReductionEngine();
    engine.initialize(result as any, {} as any);
    expect((engine as any).config.wpm).toBe(600);
    expect((engine as any).config.chunkSize).toBe(4);
    expect((engine as any).config.msPerWord).toBe(100);
    expect(config.engineConfig.difficultySettings.targetWpm).toBe(100);
  });
});
