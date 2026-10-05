import { TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { CustomPreviewDialogComponent } from './custom-preview-dialog.component';

describe('Custom preview without level selection', () => {
  it('shows only custom controls and resets to the default content settings', () => {
    const close = jasmine.createSpy('close');
    const configuration = { engineType: 'word_highlight', engineConfig: { targetWpm: 200, content: 'Default content' } };
    TestBed.configureTestingModule({ providers: [
      { provide: MAT_DIALOG_DATA, useValue: configuration }, { provide: MatDialogRef, useValue: { close } }
    ] });
    const fixture = TestBed.createComponent(CustomPreviewDialogComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('#preview-level')).toBeNull();
    fixture.componentInstance.speedWpm = 19;
    fixture.componentInstance.submit();
    expect(close).not.toHaveBeenCalled();
    fixture.componentInstance.reset();
    expect(fixture.componentInstance.speedWpm).toBe(200);
    fixture.componentInstance.speedWpm = 500;
    fixture.componentInstance.submit();
    expect(close).toHaveBeenCalledWith({ speedWpm: 500, chunkSize: 1 });
    expect(configuration.engineConfig.content).toBe('Default content');
    expect(configuration.engineConfig.targetWpm).toBe(200);
  });
});
