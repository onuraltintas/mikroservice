import { HttpErrorResponse, HttpRequest } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { lastValueFrom, throwError } from 'rxjs';
import { AuthService } from '../services/auth.service';
import { ToasterService } from '../services/toaster.service';
import { errorInterceptor } from './error.interceptor';

describe('errorInterceptor', () => {
  it('shows the auth CAPTCHA message without treating it as an authorization failure', async () => {
    spyOn(console, 'error');
    const navigate = jasmine.createSpy('navigate');
    const showError = jasmine.createSpy('showError');
    TestBed.configureTestingModule({
      providers: [
        { provide: Router, useValue: { navigate } },
        { provide: AuthService, useValue: {} },
        { provide: ToasterService, useValue: { error: showError } }
      ]
    });
    const request = new HttpRequest('POST', '/api/auth/speed-reading/login', {});
    const response = TestBed.runInInjectionContext(() => errorInterceptor(request, () =>
      throwError(() => new HttpErrorResponse({
        status: 403,
        url: request.url,
        error: {
          success: false,
          code: 'Auth.CaptchaFailed',
          message: 'Güvenlik doğrulaması tamamlanamadı. Lütfen tekrar deneyin.'
        }
      }))));

    await expectAsync(lastValueFrom(response)).toBeRejected();

    expect(showError).toHaveBeenCalledOnceWith(
      'Güvenlik doğrulaması tamamlanamadı. Lütfen tekrar deneyin.'
    );
    expect(navigate).not.toHaveBeenCalled();
  });
});
