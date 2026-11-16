import { DOCUMENT, isPlatformBrowser } from '@angular/common';
import { HttpBackend, HttpClient } from '@angular/common/http';
import { Injectable, PLATFORM_ID, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';

interface AuthRecaptchaConfiguration {
  enabled: boolean;
  siteKey: string | null;
}

interface GoogleRecaptchaWindow {
  grecaptcha?: {
    ready(callback: () => void): void;
    execute(siteKey: string, options: { action: string }): Promise<string>;
  };
}

@Injectable({ providedIn: 'root' })
export class AuthRecaptchaService {
  private readonly document = inject(DOCUMENT);
  private readonly platformId = inject(PLATFORM_ID);
  private readonly http = new HttpClient(inject(HttpBackend));
  private readonly configurationPromises = new Map<string, Promise<AuthRecaptchaConfiguration>>();
  private scriptPromise?: Promise<void>;

  /** A token made ahead of the request it is for (single use, valid for two minutes at Google; kept for 90 seconds). */
  private prepared?: { action: string; configurationUrl: string; token: Promise<string | null>; at: number };

  /**
   * Live test 16.11.2026 (#2): the sign-in button waited about 1.5 seconds for the configuration, Google's script and the token.
   * The sign-in page loads the first two when it opens. A failure here is silent; the request itself reports it.
   */
  warmUp(configurationUrl = `${environment.apiUrl}/auth/captcha-config`): void {
    if (!isPlatformBrowser(this.platformId)) return;
    void this.siteKey(configurationUrl).then(siteKey => siteKey ? this.loadScript(siteKey) : undefined).catch(() => undefined);
  }

  /** Makes the token while the person is still typing, so the request leaves as soon as the button is pressed. */
  prepareToken(action: string, configurationUrl = `${environment.apiUrl}/auth/captcha-config`): void {
    if (!isPlatformBrowser(this.platformId)) return;
    if (this.prepared && this.prepared.action === action && this.prepared.configurationUrl === configurationUrl
      && Date.now() - this.prepared.at < AuthRecaptchaService.preparedTokenLifetimeMs) return;
    const token = this.newToken(action, configurationUrl);
    token.catch(() => { if (this.prepared?.token === token) this.prepared = undefined; });
    this.prepared = { action, configurationUrl, token, at: Date.now() };
  }

  private static readonly preparedTokenLifetimeMs = 90_000;

  async createToken(action: string, configurationUrl = `${environment.apiUrl}/auth/captcha-config`): Promise<string | null> {
    if (!isPlatformBrowser(this.platformId)) return null;
    const prepared = this.prepared;
    if (prepared && prepared.action === action && prepared.configurationUrl === configurationUrl) {
      // A token is used once: whatever happens, the next request makes its own.
      this.prepared = undefined;
      if (Date.now() - prepared.at < AuthRecaptchaService.preparedTokenLifetimeMs) {
        try { return await prepared.token; } catch { /* the token is made again below */ }
      }
    }
    return this.newToken(action, configurationUrl);
  }

  private async newToken(action: string, configurationUrl: string): Promise<string | null> {
    const siteKey = await this.siteKey(configurationUrl);
    if (!siteKey) return null;
    await this.loadScript(siteKey);
    const token = await (window as GoogleRecaptchaWindow).grecaptcha?.execute(siteKey, { action });
    if (!token) throw new Error('Güvenlik doğrulaması tamamlanamadı.');
    return token;
  }

  /** The site key, or null when the check is turned off. */
  private async siteKey(configurationUrl: string): Promise<string | null> {
    let configuration: AuthRecaptchaConfiguration;
    try {
      let pending = this.configurationPromises.get(configurationUrl);
      if (!pending) {
        pending = this.loadConfiguration(configurationUrl);
        this.configurationPromises.set(configurationUrl, pending);
      }
      configuration = await pending;
    } catch (error) {
      this.configurationPromises.delete(configurationUrl);
      throw error;
    }

    if (!configuration.enabled) return null;
    if (!configuration.siteKey) {
      throw new Error('Güvenlik doğrulaması yapılandırması eksik.');
    }
    return configuration.siteKey;
  }

  private loadConfiguration(configurationUrl: string): Promise<AuthRecaptchaConfiguration> {
    return firstValueFrom(this.http.get<AuthRecaptchaConfiguration>(
      configurationUrl
    ));
  }

  private async loadScript(siteKey: string): Promise<void> {
    const browserWindow = window as GoogleRecaptchaWindow;
    if (!this.scriptPromise) {
      this.scriptPromise = new Promise((resolve, reject) => {
        const ready = () => {
          if (browserWindow.grecaptcha) {
            browserWindow.grecaptcha.ready(resolve);
          } else {
            reject(new Error('Güvenlik doğrulama hizmeti başlatılamadı.'));
          }
        };
        if (browserWindow.grecaptcha) {
          ready();
          return;
        }

        const script = this.document.createElement('script');
        script.src = `https://www.google.com/recaptcha/api.js?render=${encodeURIComponent(siteKey)}`;
        script.async = true;
        script.defer = true;
        script.onload = ready;
        script.onerror = () => reject(new Error('Güvenlik doğrulama hizmetine ulaşılamadı.'));
        this.document.head.appendChild(script);
      });
    }

    try {
      await this.scriptPromise;
    } catch (error) {
      this.scriptPromise = undefined;
      throw error;
    }
  }
}
