import { TestBed } from '@angular/core/testing';
import { PanelConfirm, PanelConfirmDialogComponent } from './panel-confirm';
import { hashUrl, problemText } from './panel-state';
import { readCsvText } from './sections/students.component';

// 20.11.2026 maxkampüs live test.
describe('course panel live-test fixes', () => {
  it('keeps the page path when it writes a section into the address (<base href="/">)', () => {
    const path = globalThis.location.pathname + globalThis.location.search;
    expect(hashUrl('#takvim?hafta=2026-11-16')).toBe(`${path}#takvim?hafta=2026-11-16`);
  });

  it('names the reason for codes that used to show "İşlem tamamlanamadı"', () => {
    expect(problemText({ error: { code: 'Lesson.NotEnded' } })).toContain('henüz bitmedi');
    expect(problemText({ error: { code: 'Lesson.InvalidTime' } })).toContain('geçmiş bir saate');
    expect(problemText({ error: { code: 'StudentImport.InvalidHeader' } })).toContain('ad, e-posta, telefon');
  });

  it('asks in the panel\'s own dialog and answers with the button pressed', async () => {
    const fixture = TestBed.configureTestingModule({ imports: [PanelConfirmDialogComponent] }).createComponent(PanelConfirmDialogComponent);
    const confirm = TestBed.inject(PanelConfirm);
    const answer = confirm.ask('Ders iptal edilsin mi?', { confirmText: 'Dersi iptal et', danger: true });
    await fixture.whenStable();
    const element: HTMLElement = fixture.nativeElement;
    expect(element.querySelector('[role="alertdialog"]')?.textContent).toContain('Ders iptal edilsin mi?');
    expect(document.activeElement?.textContent?.trim()).toBe('Vazgeç');
    (element.querySelectorAll('.dialog-actions button')[1] as HTMLButtonElement).click();
    expect(await answer).toBe(true);
    await fixture.whenStable();
    expect(element.querySelector('[role="alertdialog"]')).toBeNull();

    const second = confirm.ask('Öğretmen çıkarılsın mı?');
    await fixture.whenStable();
    (element.querySelector('.dialog-actions button') as HTMLButtonElement).click();
    expect(await second).toBe(false);
  });

  it('reads a CSV saved by Excel in Turkish (Windows-1254) as well as UTF-8', () => {
    const utf8 = new TextEncoder().encode('ad\nÖğrenci Şeker').buffer;
    expect(readCsvText(utf8)).toBe('ad\nÖğrenci Şeker');
    // "Öğrenci Şeker" in Windows-1254: Ö=D6 ğ=F0 Ş=DE
    const cp1254 = new Uint8Array([0x61, 0x64, 0x0a, 0xd6, 0xf0, 0x72, 0x65, 0x6e, 0x63, 0x69, 0x20, 0xde, 0x65, 0x6b, 0x65, 0x72]).buffer;
    expect(readCsvText(cp1254)).toBe('ad\nÖğrenci Şeker');
  });
});
