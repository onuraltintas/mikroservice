import { TestBed } from '@angular/core/testing';
import { vi } from 'vitest';
import { DarkModeService } from './dark-mode.service';

// T1 (31.10.2026): light / dark / follow the system, kept in the existing `theme` key; the system setting is followed live.
describe('DarkModeService', () => {
  let listeners: ((event: { matches: boolean }) => void)[];
  let systemDark: boolean;
  beforeEach(() => {
    localStorage.clear(); listeners = []; systemDark = false;
    document.documentElement.classList.remove('dark'); document.documentElement.style.colorScheme = '';
    vi.stubGlobal('matchMedia', vi.fn(() => ({
      get matches() { return systemDark; },
      addEventListener: (_: string, listener: (event: { matches: boolean }) => void) => listeners.push(listener),
      removeEventListener: vi.fn()
    })));
    TestBed.resetTestingModule();
  });
  afterEach(() => vi.unstubAllGlobals());
  const create = () => TestBed.inject(DarkModeService);
  const html = () => document.documentElement;

  it('follows the system by default without writing a choice', () => {
    systemDark = true;
    const theme = create();
    expect(theme.mode()).toBe('system');
    expect(theme.isDarkMode()).toBe(true);
    expect(html().classList.contains('dark')).toBe(true);
    expect(html().style.colorScheme).toBe('dark');
    expect(localStorage.getItem('theme')).toBeNull();
  });

  it('tracks a later system change while following the system', () => {
    const theme = create();
    expect(theme.isDarkMode()).toBe(false);
    systemDark = true; listeners.forEach(listener => listener({ matches: true }));
    expect(theme.isDarkMode()).toBe(true);
    expect(html().classList.contains('dark')).toBe(true);
  });

  it('keeps a stored light or dark choice and ignores the system', () => {
    localStorage.setItem('theme', 'light'); systemDark = true;
    const theme = create();
    expect(theme.mode()).toBe('light');
    expect(theme.isDarkMode()).toBe(false);
    listeners.forEach(listener => listener({ matches: true }));
    expect(theme.isDarkMode()).toBe(false);
    expect(html().style.colorScheme).toBe('light');
  });

  it('stores each of the three choices', () => {
    const theme = create();
    theme.setMode('dark');
    expect(localStorage.getItem('theme')).toBe('dark');
    expect(html().classList.contains('dark')).toBe(true);
    theme.setMode('system');
    expect(localStorage.getItem('theme')).toBe('system');
    expect(theme.isDarkMode()).toBe(false);
    theme.setMode('light');
    expect(localStorage.getItem('theme')).toBe('light');
  });

  it('toggle switches between light and dark', () => {
    const theme = create();
    theme.toggle();
    expect(theme.mode()).toBe('dark');
    theme.toggle();
    expect(theme.mode()).toBe('light');
  });
});
