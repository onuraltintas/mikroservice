import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { vi } from 'vitest';
import { DarkModeService } from '../../core/services/dark-mode.service';
import { RevealDirective } from './reveal.directive';
import { ThemeToggleComponent } from './theme-toggle.component';

describe('public site parts (27.11.2026)', () => {
  beforeEach(() => { localStorage.clear(); document.documentElement.classList.remove('dark'); });

  it('the theme toggle starts light, switches to dark and says what the next press does', () => {
    const fixture = TestBed.createComponent(ThemeToggleComponent);
    fixture.detectChanges();
    const button = fixture.nativeElement.querySelector('button') as HTMLButtonElement;
    expect(button.getAttribute('aria-label')).toBe('Koyu temaya geç');
    button.click();
    fixture.detectChanges();
    expect(TestBed.inject(DarkModeService).isDarkMode()).toBe(true);
    expect(document.documentElement.classList.contains('dark')).toBe(true);
    expect(localStorage.getItem('theme')).toBe('dark');
    expect(button.getAttribute('aria-label')).toBe('Açık temaya geç');
  });

  @Component({ standalone: true, imports: [RevealDirective], template: `<div publicReveal="2" id="block">İçerik</div>` })
  class HostComponent {}

  it('leaves a block visible when the visitor asked for reduced motion', () => {
    vi.stubGlobal('matchMedia', vi.fn(() => ({ matches: true, addEventListener: vi.fn(), removeEventListener: vi.fn() })));
    const fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('#block').classList.contains('reveal-pending')).toBe(false);
    vi.unstubAllGlobals();
  });

  it('hides a block below the fold until it scrolls into view, with a short stagger', () => {
    let notify: (entries: { isIntersecting: boolean }[]) => void = () => undefined;
    vi.stubGlobal('matchMedia', vi.fn(() => ({ matches: false, addEventListener: vi.fn(), removeEventListener: vi.fn() })));
    vi.stubGlobal('IntersectionObserver', class { constructor(callback: typeof notify) { notify = callback; } observe() {} disconnect() {} });
    vi.spyOn(HTMLElement.prototype, 'getBoundingClientRect').mockReturnValue({ top: 5000 } as DOMRect);
    const fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
    const block = fixture.nativeElement.querySelector('#block') as HTMLElement;
    expect(block.classList.contains('reveal-pending')).toBe(true);
    expect(block.style.getPropertyValue('--reveal-delay')).toBe('140ms');
    notify([{ isIntersecting: true }]);
    expect(block.classList.contains('reveal-run')).toBe(true);
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });
});
