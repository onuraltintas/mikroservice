import { Injectable } from '@angular/core';
import { environment } from '../../../environments/environment';

export interface GoogleIdentityResponse {
  credential: string;
}

export type GoogleIdentityCallback = (response: GoogleIdentityResponse) => void;

interface GoogleIdentityApi {
  accounts?: {
    id?: {
      initialize: (options: {
        client_id: string;
        callback: GoogleIdentityCallback;
        auto_select: boolean;
        cancel_on_tap_outside: boolean;
      }) => void;
      renderButton: (element: HTMLElement, options: {
        theme: string;
        size: string;
        text: 'signin_with' | 'signup_with';
        shape: string;
        width: number;
      }) => void;
    };
  };
}

declare global {
  interface Window {
    google?: GoogleIdentityApi;
  }
}

@Injectable({ providedIn: 'root' })
export class GoogleIdentityService {
  private static readonly scriptSource = 'https://accounts.google.com/gsi/client';

  private scriptPromise: Promise<void> | null = null;
  private initialized = false;
  private activeCallback: GoogleIdentityCallback | null = null;

  renderButton(
    element: HTMLElement,
    text: 'signin_with' | 'signup_with',
    callback: GoogleIdentityCallback
  ): Promise<void> {
    this.activeCallback = callback;

    return this.ensureScript().then(() => {
      const googleIdentity = window.google?.accounts?.id;
      if (!googleIdentity) {
        throw new Error('Google Identity Services yüklenemedi.');
      }

      if (!this.initialized) {
        googleIdentity.initialize({
          client_id: environment.googleClientId,
          callback: response => this.activeCallback?.(response),
          auto_select: false,
          cancel_on_tap_outside: true
        });
        this.initialized = true;
      }

      if (!element.querySelector('iframe')) {
        googleIdentity.renderButton(element, {
          theme: 'outline',
          size: 'large',
          text,
          shape: 'rectangular',
          width: element.offsetWidth || 350
        });
      }
    });
  }

  clearCallback(callback: GoogleIdentityCallback): void {
    if (this.activeCallback === callback) {
      this.activeCallback = null;
    }
  }

  private ensureScript(): Promise<void> {
    if (window.google?.accounts?.id) {
      return Promise.resolve();
    }

    if (this.scriptPromise) {
      return this.scriptPromise;
    }

    this.scriptPromise = new Promise<void>((resolve, reject) => {
      const existingScript = document.querySelector<HTMLScriptElement>(
        `script[src="${GoogleIdentityService.scriptSource}"]`
      );

      if (existingScript) {
        existingScript.addEventListener('load', () => resolve(), { once: true });
        existingScript.addEventListener(
          'error',
          () => reject(new Error('Google Identity Services betiği yüklenemedi.')),
          { once: true }
        );
        return;
      }

      const script = document.createElement('script');
      script.src = GoogleIdentityService.scriptSource;
      script.async = true;
      script.defer = true;
      script.addEventListener('load', () => resolve(), { once: true });
      script.addEventListener(
        'error',
        () => reject(new Error('Google Identity Services betiği yüklenemedi.')),
        { once: true }
      );
      document.head.appendChild(script);
    }).catch(error => {
      this.scriptPromise = null;
      throw error;
    });

    return this.scriptPromise;
  }
}
