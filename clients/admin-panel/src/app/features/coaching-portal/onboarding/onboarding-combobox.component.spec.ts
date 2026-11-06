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

  it('filters a short list in the browser, ignoring case, Turkish i and accents', () => {
    expect(fold('İSTANBUL')).toBe(fold('istanbul'));
    expect(fold('Çankaya')).toBe('cankaya');
    expect(fold('Işıklar')).toBe('isiklar');
  });
});
