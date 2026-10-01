import { DOCUMENT } from '@angular/common';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import {
  CoachingManagementService,
  CoachingNewsletterRecaptchaConfiguration
} from './coaching-management.service';

@Injectable({ providedIn: 'root' })
export class CoachingNewsletterCaptchaService {
  private readonly document = inject(DOCUMENT);
  private readonly coaching = inject(CoachingManagementService);
  private configurationPromise?: Promise<CoachingNewsletterRecaptchaConfiguration>;
  private scriptPromise?: Promise<void>;

  async createToken(): Promise<string | undefined> {
    let configuration;
    try {
      configuration = await (this.configurationPromise ??= this.loadConfiguration());
    } catch (error) {
      this.configurationPromise = undefined;
      throw error;
    }

    if (!configuration.enabled) return undefined;
    if (!configuration.siteKey) throw new Error('Koçluk bülteni reCAPTCHA site key is missing.');

    await this.loadScript(configuration.siteKey);
    return window.grecaptcha.execute(configuration.siteKey, { action: 'newsletter_signup' });
  }

  private loadConfiguration(): Promise<CoachingNewsletterRecaptchaConfiguration> {
    return firstValueFrom(this.coaching.getCoachingNewsletterRecaptchaConfiguration());
  }

  private async loadScript(siteKey: string): Promise<void> {
    if (!this.scriptPromise) {
      this.scriptPromise = new Promise((resolve, reject) => {
        const ready = () => window.grecaptcha.ready(resolve);
        if (window.grecaptcha) {
          ready();
          return;
        }

        const script = this.document.createElement('script');
        script.src = `https://www.google.com/recaptcha/api.js?render=${encodeURIComponent(siteKey)}`;
        script.async = true;
        script.defer = true;
        script.onload = ready;
        script.onerror = () => reject(new Error('Koçluk bülteni reCAPTCHA yüklenemedi.'));
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

declare global {
  interface Window {
    grecaptcha: {
      ready(callback: () => void): void;
      execute(siteKey: string, options: { action: string }): Promise<string>;
    };
  }
}
