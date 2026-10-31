import { Injectable, signal, inject, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';

export type ThemeMode = 'light' | 'dark' | 'system';

/**
 * Colour theme: light, dark or the system setting (T1, 31.10.2026). The choice lives in the `theme` localStorage key,
 * which public/theme-boot.js also reads to apply the theme before Angular starts. Without a saved choice the system
 * setting is followed, including later changes, and nothing is written.
 */
@Injectable({
    providedIn: 'root'
})
export class DarkModeService {
    readonly mode = signal<ThemeMode>('system');
    readonly isDarkMode = signal<boolean>(false);
    private readonly platformId = inject(PLATFORM_ID);
    private systemDark = false;

    constructor() {
        if (!isPlatformBrowser(this.platformId)) return;
        const query = typeof window.matchMedia === 'function' ? window.matchMedia('(prefers-color-scheme: dark)') : null;
        this.systemDark = !!query?.matches;
        query?.addEventListener?.('change', event => {
            this.systemDark = event.matches;
            if (this.mode() === 'system') this.apply();
        });
        const saved = this.read();
        this.mode.set(saved === 'light' || saved === 'dark' ? saved : 'system');
        this.apply();
    }

    setMode(mode: ThemeMode) {
        this.mode.set(mode);
        this.write(mode);
        this.apply();
    }

    toggle() {
        this.setMode(this.isDarkMode() ? 'light' : 'dark');
    }

    private apply() {
        const dark = this.mode() === 'dark' || (this.mode() === 'system' && this.systemDark);
        this.isDarkMode.set(dark);
        if (!isPlatformBrowser(this.platformId)) return;
        const root = document.documentElement;
        root.classList.toggle('dark', dark);
        root.style.colorScheme = dark ? 'dark' : 'light';
    }

    private read(): string | null {
        try { return localStorage.getItem('theme'); } catch { return null; }
    }

    private write(mode: ThemeMode) {
        try { localStorage.setItem('theme', mode); } catch { /* storage blocked: the choice lasts for this visit */ }
    }
}
