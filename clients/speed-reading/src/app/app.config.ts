import { ApplicationConfig, ErrorHandler, provideBrowserGlobalErrorListeners, provideZoneChangeDetection, isDevMode, LOCALE_ID } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptors, withFetch } from '@angular/common/http';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { provideCharts, withDefaultRegisterables } from 'ng2-charts';
import { provideToastr } from 'ngx-toastr';
import { MatPaginatorIntl } from '@angular/material/paginator';
import { MAT_DATE_LOCALE } from '@angular/material/core';
import { registerLocaleData } from '@angular/common';
import localeTr from '@angular/common/locales/tr';

import { routes } from './app.routes';
import { authInterceptor } from './core/interceptors/auth.interceptor';
import { errorInterceptor } from './core/interceptors/error.interceptor';
import { apiResponseInterceptor } from './core/interceptors/api-response.interceptor';
import { GlobalErrorHandler } from './core/handlers/global-error.handler';
import { TurkishPaginatorIntl } from './core/services/turkish-paginator-intl.service';
import { provideServiceWorker } from '@angular/service-worker';

// Register Turkish locale globally
registerLocaleData(localeTr);

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes),
    provideHttpClient(
      withInterceptors([apiResponseInterceptor, authInterceptor, errorInterceptor]),
      withFetch()
    ),
    provideAnimationsAsync(),
    provideCharts(withDefaultRegisterables()),
    provideToastr({
      timeOut: 4000,
      positionClass: 'toast-top-right',
      preventDuplicates: true,
      progressBar: true,
      closeButton: true,
      enableHtml: false,
      tapToDismiss: true,
      maxOpened: 5,
      autoDismiss: true
    }),
    { provide: MatPaginatorIntl, useClass: TurkishPaginatorIntl },
    { provide: MAT_DATE_LOCALE, useValue: 'tr-TR' },
    { provide: LOCALE_ID, useValue: 'tr-TR' },
    { provide: ErrorHandler, useClass: GlobalErrorHandler },
    provideServiceWorker('ngsw-worker.js', {
      enabled: !isDevMode(),
      registrationStrategy: 'registerWhenStable:30000'
    })
  ]
};
