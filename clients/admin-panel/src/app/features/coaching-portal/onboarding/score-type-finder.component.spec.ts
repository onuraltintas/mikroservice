import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { vi } from 'vitest';
import { ScoreTypeFinderComponent } from './score-type-finder.component';
import { fieldFoundNote, yksFieldOf } from './onboarding-model';

/** Paket H6 (09.11.2026): "Emin değil misin? Bölümüne göre bulalım": the score type from a programme's name. */
describe('ScoreTypeFinderComponent (Paket H6)', () => {
  const url = '/api/coaching/students/me/onboarding/options/programs/fields';
  const wait = (ms = 400) => new Promise(r => setTimeout(r, ms));

  async function render() {
    TestBed.configureTestingModule({ imports: [ScoreTypeFinderComponent], providers: [provideHttpClient(), provideHttpClientTesting()] });
    const fixture = TestBed.createComponent(ScoreTypeFinderComponent);
    const chosen = vi.fn(); fixture.componentInstance.chosen.subscribe(chosen);
    fixture.detectChanges(); await fixture.whenStable();
    const el = fixture.nativeElement as HTMLElement;
    const http = TestBed.inject(HttpTestingController);
    const settle = async () => { fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges(); };
    const toggle = () => el.querySelector('button[aria-expanded]') as HTMLButtonElement;
    const box = () => el.querySelector('input[type="search"]') as HTMLInputElement;
    const type = async (text: string) => { box().value = text; box().dispatchEvent(new Event('input')); await settle(); };
    const rows = () => Array.from(el.querySelectorAll('ul button')).map(x => x.textContent!.trim());
    return { fixture, el, http, chosen, settle, toggle, box, type, rows };
  }

  it('is a disclosure with a labelled search box', async () => {
    const t = await render();
    expect(t.toggle().textContent!.trim()).toBe('Emin değil misin? Bölümüne göre bulalım');
    expect(t.toggle().getAttribute('aria-expanded')).toBe('false');
    expect(t.box()).toBeNull();
    t.toggle().click(); await t.settle();
    expect(t.toggle().getAttribute('aria-expanded')).toBe('true');
    const panel = t.el.querySelector(`#${t.toggle().getAttribute('aria-controls')}`);
    expect(panel).not.toBeNull();
    const label = t.el.querySelector(`label[for="${t.box().id}"]`)!;
    expect(label.textContent!.trim()).toBe('Bölüm adı');
    expect(t.box().placeholder).toContain('bilgisayar mühendisliği');
    // Önlisans programmes are not listed: the student is told which answer fits them.
    expect(panel!.textContent).toContain('Yalnız TYT (önlisans)');
    // Escape closes it and gives the focus back to the button.
    t.box().dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' })); await t.settle(); await new Promise(r => setTimeout(r)); await t.settle();
    expect(t.toggle().getAttribute('aria-expanded')).toBe('false');
    expect(document.activeElement).toBe(t.toggle());
  });

  it('searches after a pause, groups by name with readable score types and emits the chosen field', async () => {
    const t = await render();
    t.toggle().click(); await t.settle();
    await t.type('b');
    await wait(); await t.settle();
    t.http.expectNone(r => r.url === url);
    expect(t.el.querySelector('[role="status"]')!.textContent).toContain('en az 2 harf');
    await t.type('bil'); await t.type('bilgisayar');
    t.http.expectNone(r => r.url === url);
    await wait(); await t.settle();
    const request = t.http.expectOne(r => r.url === url);
    expect(request.request.params.get('search')).toBe('bilgisayar');
    request.flush({ data: [{ name: 'Bilgisayar Mühendisliği', scoreType: 'SAY' }, { name: 'Hukuk', scoreType: 'EA' }, { name: 'İşletme', scoreType: 'SOZ' },
      { name: 'İngilizce Öğretmenliği', scoreType: 'YDT' }, { name: 'Bilgisayar Programcılığı', scoreType: 'TYT' }] });
    await t.settle();
    expect(t.rows()).toEqual(['Bilgisayar Mühendisliği · Sayısal', 'Hukuk · Eşit Ağırlık', 'İşletme · Sözel', 'İngilizce Öğretmenliği · Dil']);
    expect(t.el.querySelector('[role="status"]')!.textContent).toContain('4 bölüm');
    (Array.from(t.el.querySelectorAll('ul button')).find(x => x.textContent!.includes('Dil')) as HTMLElement).click();
    expect(t.chosen).toHaveBeenCalledWith({ field: { value: 'DIL', label: 'Dil' }, program: 'İngilizce Öğretmenliği' });
    t.http.verify();
  });

  it('ignores a late answer to an older search', async () => {
    const t = await render();
    t.toggle().click(); await t.settle();
    await t.type('hukuk'); await wait(); await t.settle();
    const first = t.http.expectOne(r => r.url === url && r.params.get('search') === 'hukuk');
    await t.type('işletme'); await wait(); await t.settle();
    const second = t.http.expectOne(r => r.url === url && r.params.get('search') === 'işletme');
    expect(first.cancelled).toBe(true);
    second.flush({ data: [{ name: 'İşletme', scoreType: 'EA' }] }); await t.settle();
    expect(t.rows()).toEqual(['İşletme · Eşit Ağırlık']);
  });

  it('says when nothing is found and when the search fails, with "Yeniden dene"', async () => {
    const t = await render();
    t.toggle().click(); await t.settle();
    await t.type('xyzt'); await wait(); await t.settle();
    t.http.expectOne(r => r.url === url).flush({ data: [] }); await t.settle();
    expect(t.rows()).toEqual([]);
    expect(t.el.querySelector('[role="status"]')!.textContent).toContain('bulamadık');
    await t.type('hukuk'); await wait(); await t.settle();
    t.http.expectOne(r => r.url === url).flush({ message: 'x' }, { status: 500, statusText: 'Server Error' }); await t.settle();
    expect(t.el.querySelector('[role="alert"]')!.textContent).toContain('Bölümler yüklenemedi');
    (Array.from(t.el.querySelectorAll('button')).find(x => x.textContent!.trim() === 'Yeniden dene') as HTMLElement).click(); await t.settle();
    t.http.expectOne(r => r.url === url && r.params.get('search') === 'hukuk').flush({ data: [{ name: 'Hukuk', scoreType: 'EA' }] }); await t.settle();
    expect(t.el.querySelector('[role="alert"]')).toBeNull();
    expect(t.rows()).toEqual(['Hukuk · Eşit Ağırlık']);
  });
  // PR 137 review: separators alone or one-letter words are not a search (the server would list nothing either).
  it('sends nothing without a word of two letters and says so', async () => {
    const t = await render();
    t.toggle().click(); await t.settle();
    for (const text of ['..', '()', '--', 'a.', 'a b']) {
      await t.type(text); await wait(); await t.settle();
      t.http.expectNone(r => r.url === url);
      expect(t.el.querySelector('[role="status"]')!.textContent, text).toContain('en az 2 harf');
    }
    await t.type('a. hu'); await wait(); await t.settle();
    t.http.expectOne(r => r.url === url && r.params.get('search') === 'a. hu').flush({ data: [] });
  });

  // PR 137 review: the same term typed again after a short or empty box is searched again.
  it('searches again for the same term typed after clearing the box', async () => {
    const t = await render();
    t.toggle().click(); await t.settle();
    await t.type('hukuk'); await wait(); await t.settle();
    t.http.expectOne(r => r.url === url).flush({ data: [{ name: 'Hukuk', scoreType: 'EA' }] }); await t.settle();
    expect(t.rows()).toEqual(['Hukuk · Eşit Ağırlık']);
    await t.type(''); await t.type('hukuk'); await wait(); await t.settle();
    t.http.expectOne(r => r.url === url && r.params.get('search') === 'hukuk').flush({ data: [{ name: 'Hukuk', scoreType: 'EA' }] }); await t.settle();
    expect(t.box().value).toBe('hukuk');
    expect(t.rows()).toEqual(['Hukuk · Eşit Ağırlık']);
  });

  // PR 137 review: reopening starts over (no rows of the last search under an empty box), even with a search still running.
  it('starts over when it is opened again', async () => {
    const t = await render();
    t.toggle().click(); await t.settle();
    await t.type('hukuk'); await wait(); await t.settle();
    t.http.expectOne(r => r.url === url).flush({ data: [{ name: 'Hukuk', scoreType: 'EA' }] }); await t.settle();
    await t.type('isletme'); await wait(); await t.settle();
    const late = t.http.expectOne(r => r.url === url && r.params.get('search') === 'isletme');
    t.toggle().click(); await t.settle();
    t.toggle().click(); await t.settle();
    expect(late.cancelled).toBe(true);
    expect(t.box().value).toBe('');
    expect(t.rows()).toEqual([]);
    expect(t.el.querySelector('[role="status"]')!.textContent).toContain('Bölümünün adını yaz');
  });

  it('closes with Escape on "Yeniden dene" too', async () => {
    const t = await render();
    t.toggle().click(); await t.settle();
    await t.type('hukuk'); await wait(); await t.settle();
    t.http.expectOne(r => r.url === url).flush({ message: 'x' }, { status: 500, statusText: 'Server Error' }); await t.settle();
    const retry = Array.from(t.el.querySelectorAll('button')).find(x => x.textContent!.trim() === 'Yeniden dene') as HTMLElement;
    retry.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' })); await t.settle(); await new Promise(r => setTimeout(r)); await t.settle();
    expect(t.toggle().getAttribute('aria-expanded')).toBe('false');
    expect(document.activeElement).toBe(t.toggle());
  });
});

describe('score type of a programme (Paket H6)', () => {
  it('maps the catalogue score types to the wizard fields (YDT is Dil; TYT has none)', () => {
    expect(yksFieldOf('SAY')).toEqual({ value: 'SAY', label: 'Sayısal' });
    expect(yksFieldOf('EA')).toEqual({ value: 'EA', label: 'Eşit Ağırlık' });
    expect(yksFieldOf('SOZ')).toEqual({ value: 'SOZ', label: 'Sözel' });
    expect(yksFieldOf('SÖZ')).toEqual({ value: 'SOZ', label: 'Sözel' });
    expect(yksFieldOf('YDT')).toEqual({ value: 'DIL', label: 'Dil' });
    expect(yksFieldOf('DİL')).toEqual({ value: 'DIL', label: 'Dil' });
    expect(yksFieldOf('TYT')).toBeNull();
    expect(yksFieldOf(null)).toBeNull();
    expect(fieldFoundNote({ value: 'SAY', label: 'Sayısal' })).toBe('Bu bölüm Sayısal puanla öğrenci alıyor; puan türün Sayısal olarak seçildi.');
  });
});
