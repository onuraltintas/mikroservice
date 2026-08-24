import { Injectable, signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class LanguageService {
    private language = signal<string>(
        (typeof localStorage !== 'undefined' ? localStorage.getItem('lang') : null) ?? 'tr'
    );
    currentLanguage = this.language.asReadonly();

    setLanguage(lang: string): void {
        if (typeof localStorage !== 'undefined') {
            localStorage.setItem('lang', lang);
        }
        this.language.set(lang);
    }
}
