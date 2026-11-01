import { ApplicationConfig, LOCALE_ID, provideBrowserGlobalErrorListeners } from '@angular/core';
import { registerLocaleData } from '@angular/common';
import localeTr from '@angular/common/locales/tr';
import { provideRouter, withNavigationErrorHandler } from '@angular/router';
import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { provideOAuthClient } from 'angular-oauth2-oidc';
import { authInterceptor } from './core/auth/auth.interceptor';
import { authRecaptchaInterceptor } from './core/auth/auth-recaptcha.interceptor';
import { coachingSubscriptionRequiredInterceptor } from './core/auth/coaching-subscription-required.interceptor';

import { routes } from './app.routes';
import { provideClientHydration, withEventReplay } from '@angular/platform-browser';
import { SOCIAL_AUTH_CONFIG, SocialAuthServiceConfig, GoogleLoginProvider } from '@abacritt/angularx-social-login';
import { environment } from '../environments/environment';
import { recoverMissingRouteChunk } from './core/auth/route-chunk-recovery';

// Dates and numbers render in Turkish ("21 Eki 2026", "12,5") across the app (T1, 31.10.2026).
registerLocaleData(localeTr);

export const appConfig: ApplicationConfig = {
  providers: [
    { provide: LOCALE_ID, useValue: 'tr' },
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes, withNavigationErrorHandler(recoverMissingRouteChunk)),
    provideClientHydration(withEventReplay()),
    provideHttpClient(
      withInterceptors([authInterceptor, authRecaptchaInterceptor, coachingSubscriptionRequiredInterceptor]),
      withFetch()
    ),
    provideOAuthClient(),
    {
      provide: SOCIAL_AUTH_CONFIG,
      useValue: {
        autoLogin: false,
        providers: [
          {
            id: GoogleLoginProvider.PROVIDER_ID,
            provider: new GoogleLoginProvider(environment.googleClientId, { oneTapEnabled: false })
          }
        ],
        onError: (err) => {
          console.error(err);
        }
      } as SocialAuthServiceConfig,
    }
  ]
};
