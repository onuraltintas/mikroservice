import { HttpErrorResponse, HttpHeaders, HttpRequest } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { lastValueFrom, throwError } from 'rxjs';
import { AuthService } from '../services/auth.service';
import { ToasterService } from '../services/toaster.service';
import { errorInterceptor } from './error.interceptor';

describe('errorInterceptor', () => {
  it('explains the baseline training gate and redirects without a generic error toast', async () => {
    const navigate = jasmine.createSpy('navigate');
    const info = jasmine.createSpy('info');
    const error = jasmine.createSpy('error');
    TestBed.configureTestingModule({ providers: [
      { provide: Router, useValue: { navigate } },
      { provide: AuthService, useValue: {} },
      { provide: ToasterService, useValue: { error, info } }
    ] });
    const request = new HttpRequest('POST', '/api/speed-reading/assessment/attempts', {});
    const message = 'Programınız devam ederken başlangıç seviye tespit sınavı yapılamaz.';
    const response = TestBed.runInInjectionContext(() => errorInterceptor(request, () => throwError(() =>
      new HttpErrorResponse({ status: 400, error: { code: 'Assessment.BaselineDuringTraining', detail: message } }))));
    await expectAsync(lastValueFrom(response)).toBeRejected();
    expect(info).toHaveBeenCalledOnceWith(message);
    expect(error).not.toHaveBeenCalled();
    expect(navigate).toHaveBeenCalledOnceWith(['/student/dashboard']);
  });

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

  it('does not redirect or show a toast for best-effort logout cleanup failures', async () => {
    spyOn(console, 'error');
    const navigate = jasmine.createSpy('navigate');
    const showError = jasmine.createSpy('showError');
    TestBed.configureTestingModule({ providers: [
      { provide: Router, useValue: { navigate } },
      { provide: AuthService, useValue: {} },
      { provide: ToasterService, useValue: { error: showError } }
    ] });
    const request = new HttpRequest('DELETE', '/api/speed-reading/exercise-sessions/active', null, {
      headers: new HttpHeaders({ 'X-Skip-Error-Redirect': 'true', 'X-Skip-Error-Toast': 'true' })
    });
    const response = TestBed.runInInjectionContext(() => errorInterceptor(request, () => throwError(() =>
      new HttpErrorResponse({ status: 500, url: request.url }))));

    await expectAsync(lastValueFrom(response)).toBeRejected();

    expect(navigate).not.toHaveBeenCalled();
    expect(showError).not.toHaveBeenCalled();
  });
});
