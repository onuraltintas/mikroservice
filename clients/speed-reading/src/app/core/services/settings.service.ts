import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Observable, tap, catchError, of } from 'rxjs';
import { environment } from '../../../environments/environment';
import { UserSettings, DEFAULT_SETTINGS } from '../models/settings.model';

@Injectable({
    providedIn: 'root'
})
export class SettingsService {
    private http = inject(HttpClient);
    private readonly STORAGE_KEY = 'user-settings';

    private settingsSubject = new BehaviorSubject<UserSettings>(this.loadFromCache());
    public settings$ = this.settingsSubject.asObservable();

    constructor() {
        // Settings will be loaded after authentication
        // Don't load on service initialization to avoid 401 errors on login page
    }

    /**
     * Get current settings value (synchronous)
     */
    getCurrentSettings(): UserSettings {
        return this.settingsSubject.value;
    }

    /**
     * Load settings from backend
     */
    loadSettings(): Observable<UserSettings> {
        return this.http.get<UserSettings>(`${environment.apiUrl}/user-settings`).pipe(
            tap(settings => {
                this.settingsSubject.next(settings);
                this.saveToCache(settings);
                this.applySettings(settings);
            }),
            catchError(error => {
                console.error('Failed to load settings from backend:', error);
                // Use cached or default settings
                const cachedSettings = this.loadFromCache();
                this.applySettings(cachedSettings);
                return of(cachedSettings);
            })
        );
    }

    /**
     * Update settings on backend and apply locally
     */
    updateSettings(settings: Partial<UserSettings>): Observable<void> {
        const updatedSettings = { ...this.settingsSubject.value, ...settings };

        return this.http.put<void>(`${environment.apiUrl}/user-settings`, updatedSettings).pipe(
            tap(() => {
                this.settingsSubject.next(updatedSettings);
                this.saveToCache(updatedSettings);
                this.applySettings(updatedSettings);
            }),
            catchError(error => {
                console.error('Failed to update settings:', error);
                throw error;
            })
        );
    }

    /**
     * Apply settings to CSS custom properties
     */
    applySettings(settings: UserSettings): void {
        const root = document.documentElement;

        // Font settings
        root.style.setProperty('--reading-font-size', `${settings.fontSize}px`);
        root.style.setProperty('--reading-font-family', settings.fontFamily);
        root.style.setProperty('--reading-line-height', (settings.lineHeight / 100).toString());
        root.style.setProperty('--reading-letter-spacing', `${settings.letterSpacing}px`);

        // Theme
        root.setAttribute('data-theme', settings.theme);
    }

    /**
     * Reset settings to defaults
     */
    resetToDefaults(): Observable<void> {
        return this.http.post<void>(`${environment.apiUrl}/user-settings/reset`, {}).pipe(
            tap(() => {
                this.settingsSubject.next(DEFAULT_SETTINGS);
                this.saveToCache(DEFAULT_SETTINGS);
                this.applySettings(DEFAULT_SETTINGS);
            }),
            catchError(error => {
                console.error('Failed to reset settings:', error);
                throw error;
            })
        );
    }

    /**
     * Save settings to localStorage for offline access
     */
    private saveToCache(settings: UserSettings): void {
        try {
            localStorage.setItem(this.STORAGE_KEY, JSON.stringify(settings));
        } catch (error) {
            console.error('Failed to save settings to cache:', error);
        }
    }

    /**
     * Load settings from localStorage
     */
    private loadFromCache(): UserSettings {
        try {
            const cached = localStorage.getItem(this.STORAGE_KEY);
            if (cached) {
                return JSON.parse(cached);
            }
        } catch (error) {
            console.error('Failed to load settings from cache:', error);
        }
        return DEFAULT_SETTINGS;
    }
}
