import { Injectable, signal, effect, inject, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';

@Injectable({
    providedIn: 'root'
})
export class DarkModeService {
    isDarkMode = signal<boolean>(false);
    private platformId = inject(PLATFORM_ID);

    constructor() {
        if (isPlatformBrowser(this.platformId)) {
            // Check local storage or system preference
            const isDark = localStorage.getItem('theme') === 'dark' ||
                (!('theme' in localStorage) && window.matchMedia('(prefers-color-scheme: dark)').matches);

            this.isDarkMode.set(isDark);
            this.updateClass(isDark);
        }
    }

    toggle() {
        this.isDarkMode.update(v => !v);
        this.updateClass(this.isDarkMode());
    }

    private updateClass(isDark: boolean) {
        if (isPlatformBrowser(this.platformId)) {
            if (isDark) {
                document.documentElement.classList.add('dark');
                localStorage.setItem('theme', 'dark');
            } else {
                document.documentElement.classList.remove('dark');
                localStorage.setItem('theme', 'light');
            }
        }
    }
}
