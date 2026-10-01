import { DOCUMENT } from '@angular/common';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { PublicCmsService } from './public-cms.service';

@Injectable({ providedIn: 'root' })
export class NewsletterCaptchaService {
  private readonly document = inject(DOCUMENT);
  private readonly cms = inject(PublicCmsService);
  private scriptPromise?: Promise<void>;

  async createToken(): Promise<string | undefined> {
    const configuration = await firstValueFrom(this.cms.getGoogleRecaptchaConfiguration());
    if (!configuration.enabled) return undefined;
    if (!configuration.siteKey) throw new Error('reCAPTCHA site key is missing.');

    await this.loadScript(configuration.siteKey);
    return window.grecaptcha.execute(configuration.siteKey, { action: 'newsletter_signup' });
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
        script.onerror = () => reject(new Error('reCAPTCHA could not be loaded.'));
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
