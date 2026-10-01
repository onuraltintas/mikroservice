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

  async createToken(action: string, configurationUrl = `${environment.apiUrl}/auth/captcha-config`): Promise<string | null> {
    if (!isPlatformBrowser(this.platformId)) return null;

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

    await this.loadScript(configuration.siteKey);
    const token = await (window as GoogleRecaptchaWindow).grecaptcha?.execute(
      configuration.siteKey,
      { action }
    );
    if (!token) throw new Error('Güvenlik doğrulaması tamamlanamadı.');
    return token;
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
