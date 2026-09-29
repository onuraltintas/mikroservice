import { ApplicationConfig, inject, provideAppInitializer } from '@angular/core';
import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { StaffAuthService } from './auth/staff-auth.service';
import { staffAuthInterceptor } from './auth/staff-auth.interceptor';
import { staffAuthRecaptchaInterceptor } from './auth/staff-auth-recaptcha.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideRouter([]),
    provideHttpClient(withFetch(), withInterceptors([staffAuthInterceptor, staffAuthRecaptchaInterceptor])),
    provideAppInitializer(() => inject(StaffAuthService).restoreSession())
  ]
};
