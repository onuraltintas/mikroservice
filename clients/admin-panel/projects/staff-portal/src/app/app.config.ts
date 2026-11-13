import { ApplicationConfig, LOCALE_ID, inject, provideAppInitializer } from '@angular/core';
import { registerLocaleData } from '@angular/common';
import localeTr from '@angular/common/locales/tr';
import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { StaffAuthService } from './auth/staff-auth.service';
import { staffAuthInterceptor } from './auth/staff-auth.interceptor';
import { staffAuthRecaptchaInterceptor } from './auth/staff-auth-recaptcha.interceptor';
import { StaffLocationService } from './navigation/staff-location.service';
import { coachingInstitutionInterceptor } from './features/coaching/coaching-institution-selection';

// Live test 13.11.2026: the staff portal showed "13 Nov 2026" and "43.75 net". Dates and numbers render in Turkish,
// as in the student portal.
registerLocaleData(localeTr);

export const appConfig: ApplicationConfig = {
  providers: [
    { provide: LOCALE_ID, useValue: 'tr' },
    provideRouter([]),
    // Roadmap O2: product, workspace and section are kept in the address.
    StaffLocationService,
    provideHttpClient(withFetch(), withInterceptors([staffAuthInterceptor, staffAuthRecaptchaInterceptor, coachingInstitutionInterceptor])),
    provideAppInitializer(() => inject(StaffAuthService).restoreSession())
  ]
};
