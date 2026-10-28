import { ApplicationConfig, inject, provideAppInitializer } from '@angular/core';
import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { StaffAuthService } from './auth/staff-auth.service';
import { staffAuthInterceptor } from './auth/staff-auth.interceptor';
import { staffAuthRecaptchaInterceptor } from './auth/staff-auth-recaptcha.interceptor';
import { StaffLocationService } from './navigation/staff-location.service';
import { coachingInstitutionInterceptor } from './features/coaching/coaching-institution-selection';

export const appConfig: ApplicationConfig = {
  providers: [
    provideRouter([]),
    // Roadmap O2: product, workspace and section are kept in the address.
    StaffLocationService,
    provideHttpClient(withFetch(), withInterceptors([staffAuthInterceptor, staffAuthRecaptchaInterceptor, coachingInstitutionInterceptor])),
    provideAppInitializer(() => inject(StaffAuthService).restoreSession())
  ]
};
