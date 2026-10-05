import { TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { CustomPreviewDialogComponent } from './custom-preview-dialog.component';

describe('Single-window custom preview levels', () => {
  it('loads selected-level defaults and returns the matching exercise with temporary values', () => {
    const close = jasmine.createSpy('close');
    const first = { engineType: 'word_highlight', engineConfig: { speedWpm: 200 } };
    const second = { engineType: 'word_highlight', engineConfig: { speedWpm: 400 } };
    TestBed.configureTestingModule({ providers: [
      { provide: MAT_DIALOG_DATA, useValue: { ...first, previewLevels: [
        { id: 'first', label: 'Seviye 1', configuration: first }, { id: 'second', label: 'Seviye 2', configuration: second }
      ] } }, { provide: MatDialogRef, useValue: { close } }
    ] });
    const fixture = TestBed.createComponent(CustomPreviewDialogComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('#preview-level')).not.toBeNull();
    fixture.componentInstance.selectLevel('second');
    expect(fixture.componentInstance.speedWpm).toBe(400);
    fixture.componentInstance.speedWpm = 500;
    fixture.componentInstance.submit();
    expect(close).toHaveBeenCalledWith({ exerciseId: 'second', values: { speedWpm: 500, chunkSize: 1 } });
    expect(second.engineConfig.speedWpm).toBe(400);
  });
});
