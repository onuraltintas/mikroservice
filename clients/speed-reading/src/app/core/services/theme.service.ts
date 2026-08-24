import { Injectable, signal, effect } from '@angular/core';

export type Theme = 'light' | 'dark' | 'sepia';

const STORAGE_KEY = 'sp-theme';

const META_COLORS: Record<Theme, string> = {
  light: '#ffffff',
  dark:  '#0f172a',
  sepia: '#f4ecd8',
};

@Injectable({ providedIn: 'root' })
export class ThemeService {
  readonly theme = signal<Theme>(this.getSaved());

  constructor() {
    // Apply theme to DOM whenever signal changes (including on startup)
    effect(() => this.applyToDom(this.theme()));
  }

  setTheme(theme: Theme): void {
    this.theme.set(theme);
    localStorage.setItem(STORAGE_KEY, theme);
  }

  toggle(): void {
    this.setTheme(this.theme() === 'dark' ? 'light' : 'dark');
  }

  get isDark(): boolean {
    return this.theme() === 'dark';
  }

  // ── private ──────────────────────────────────────────────────────────────

  private getSaved(): Theme {
    return (localStorage.getItem(STORAGE_KEY) as Theme) || 'light';
  }

  private applyToDom(theme: Theme): void {
    const root = document.documentElement;

    // student-panel tokens use data-sp-theme
    if (theme === 'dark') {
      root.setAttribute('data-sp-theme', 'dark');
    } else {
      root.removeAttribute('data-sp-theme');
    }

    // sepia: extra attribute for exercise reading area
    if (theme === 'sepia') {
      root.setAttribute('data-sp-theme', 'sepia');
    }

    // mobile browser chrome color
    const meta = document.querySelector<HTMLMetaElement>('meta[name="theme-color"]');
    if (meta) meta.content = META_COLORS[theme];
  }
}
