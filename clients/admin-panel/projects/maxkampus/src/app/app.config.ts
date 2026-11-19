import { registerLocaleData } from '@angular/common';
import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import localeTr from '@angular/common/locales/tr';
import { ApplicationConfig, LOCALE_ID, provideZonelessChangeDetection } from '@angular/core';
import { provideRouter, withComponentInputBinding, withInMemoryScrolling } from '@angular/router';
import { GoogleLoginProvider, SOCIAL_AUTH_CONFIG, SocialAuthServiceConfig } from '@abacritt/angularx-social-login';
import { StaffAuthService } from '../../../staff-portal/src/app/auth/staff-auth.service';
import { environment } from '../environments/environment';
import { routes } from './app.routes';
import { maxkampusAuthInterceptor, maxkampusRecaptchaInterceptor } from './core/interceptors';
import { MaxkampusStaffAuthAdapter } from './core/staff-auth-adapter';

registerLocaleData(localeTr);

export const appConfig: ApplicationConfig = {
  providers: [
    provideZonelessChangeDetection(),
    { provide: LOCALE_ID, useValue: 'tr' },
    provideRouter(routes, withComponentInputBinding(), withInMemoryScrolling({ scrollPositionRestoration: 'enabled', anchorScrolling: 'enabled' })),
    provideHttpClient(withFetch(), withInterceptors([maxkampusRecaptchaInterceptor, maxkampusAuthInterceptor])),
    // The institution panel (shared with the staff portal) asks the staff auth service who is signed in.
    MaxkampusStaffAuthAdapter,
    { provide: StaffAuthService, useExisting: MaxkampusStaffAuthAdapter },
    {
      provide: SOCIAL_AUTH_CONFIG,
      useValue: {
        autoLogin: false,
        lang: 'tr',
        providers: [{ id: GoogleLoginProvider.PROVIDER_ID, provider: new GoogleLoginProvider(environment.googleClientId, { oneTapEnabled: false }) }],
        onError: error => console.warn('Google sign-in could not start.', error)
      } as SocialAuthServiceConfig
    }
  ]
};
