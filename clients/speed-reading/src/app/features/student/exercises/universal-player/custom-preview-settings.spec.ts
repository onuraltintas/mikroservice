import { applyCustomPreviewSettings } from './custom-preview-settings';
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
});
