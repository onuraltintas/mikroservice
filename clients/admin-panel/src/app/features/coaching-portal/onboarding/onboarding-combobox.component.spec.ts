import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { ComboPage, OnboardingComboboxComponent, fold } from './onboarding-combobox.component';

describe('OnboardingComboboxComponent (Paket C #3)', () => {
  const wait = (ms = 250) => new Promise(r => setTimeout(r, ms));

  it('searches a long list on the server page by page; the browser never gets the whole catalog', async () => {
    const load = vi.fn((search: string, page: number) => of<ComboPage>({ total: 45,
      items: Array.from({ length: page < 3 ? 20 : 5 }, (_, i) => ({ value: `${search}-${page}-${i}`, label: `Okul ${page}-${i}` })) }));
    TestBed.configureTestingModule({ imports: [OnboardingComboboxComponent] });
    const fixture = TestBed.createComponent(OnboardingComboboxComponent);
    fixture.componentRef.setInput('label', 'Hangi okul?'); fixture.componentRef.setInput('load', load);
    fixture.detectChanges(); await wait(); fixture.detectChanges();
    expect(load).toHaveBeenCalledWith('', 1);
    const el = fixture.nativeElement as HTMLElement;
    const input = el.querySelector('input')!;
    expect(input.getAttribute('role')).toBe('combobox'); expect(input.getAttribute('aria-label')).toBe('Hangi okul?');
    input.dispatchEvent(new Event('focus')); fixture.detectChanges();
    expect(el.querySelectorAll('[role="option"]').length).toBe(20);
    // The listbox holds only options; "more" and the empty state sit outside it.
    expect(Array.from(el.querySelector('[role="listbox"]')!.children).every(x => x.getAttribute('role') === 'option')).toBe(true);
    expect(el.querySelector('[role="listbox"] button')).toBeNull();
    (el.querySelector('.ob-combo-more button') as HTMLElement).click(); await wait(); fixture.detectChanges();
    expect(el.querySelectorAll('[role="option"]').length).toBe(40);
    input.value = 'fen'; input.dispatchEvent(new Event('input')); await wait(); fixture.detectChanges();
    expect(load).toHaveBeenLastCalledWith('fen', 1);
    expect(el.querySelectorAll('[role="option"]').length).toBe(20);
    const picked = vi.fn(); fixture.componentInstance.picked.subscribe(picked);
    input.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowDown' })); input.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowDown' }));
    fixture.detectChanges();
    expect(input.getAttribute('aria-activedescendant')).toMatch(/-opt-1$/);
    input.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter' }));
    expect(picked).toHaveBeenCalledWith(expect.objectContaining({ value: 'fen-1-1' }));
  });

  // G4 (#43): a short list matches every typed word, in any order.
  it('filters a short list by every word, in any order', () => {
    TestBed.configureTestingModule({ imports: [OnboardingComboboxComponent] });
    const fixture = TestBed.createComponent(OnboardingComboboxComponent);
    fixture.componentRef.setInput('label', 'Şehir');
    fixture.componentRef.setInput('options', [{ value: '1', label: 'Karşıyaka Anadolu' }, { value: '2', label: 'Karşıyaka Fen' }, { value: '3', label: 'Bornova Anadolu' }]);
    fixture.detectChanges();
    const shown = (query: string) => { fixture.componentInstance.query.set(query); return fixture.componentInstance.shown().map(x => x.value); };
    expect(shown('anadolu  KARŞIYAKA')).toEqual(['1']);
    expect(shown('anadolu')).toEqual(['1', '3']);
    expect(shown('fen bornova')).toEqual([]);
    expect(shown('')).toEqual(['1', '2', '3']);
  });

  // F2 review: a refused field marks its list, so the focus-first-invalid rule reaches it.
  it('can be marked invalid and described by an error', () => {
    TestBed.configureTestingModule({ imports: [OnboardingComboboxComponent] });
    const fixture = TestBed.createComponent(OnboardingComboboxComponent);
    fixture.componentRef.setInput('label', 'İlçe'); fixture.componentRef.setInput('options', []);
    fixture.detectChanges();
    const input = (fixture.nativeElement as HTMLElement).querySelector('input')!;
    expect(input.hasAttribute('aria-invalid')).toBe(false);
    fixture.componentRef.setInput('invalid', true); fixture.componentRef.setInput('describedBy', 'homeDistrict-error'); fixture.detectChanges();
    expect(input.getAttribute('aria-invalid')).toBe('true'); expect(input.getAttribute('aria-describedby')).toBe('homeDistrict-error');
  });

  it('filters a short list in the browser, ignoring case, Turkish i and accents', () => {
    expect(fold('İSTANBUL')).toBe(fold('istanbul'));
    expect(fold('Çankaya')).toBe('cankaya');
    expect(fold('Işıklar')).toBe('isiklar');
  });
});
