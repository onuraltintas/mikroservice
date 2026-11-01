import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse, HttpRequest, HttpResponse } from '@angular/common/http';
import { Router } from '@angular/router';
import { lastValueFrom, of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { coachingSubscriptionRequiredInterceptor } from './coaching-subscription-required.interceptor';

describe('coaching subscription required interceptor', () => {
  function run(error: HttpErrorResponse, currentUrl = '/coaching-portal/dashboard') {
    const navigate = vi.fn().mockResolvedValue(true);
    TestBed.configureTestingModule({ providers: [{ provide: Router, useValue: { url: currentUrl, navigate } }] });
    const request = new HttpRequest('GET', '/api/assignments/student/me');
    const result = lastValueFrom(TestBed.runInInjectionContext(() =>
      coachingSubscriptionRequiredInterceptor(request, () => throwError(() => error))));
    return { navigate, result };
  }

  it('sends a student without a subscription to the subscription page and keeps the error', async () => {
    const error = new HttpErrorResponse({ status: 403, error: { code: 'CoachingSubscriptionRequired', message: 'Abonelik gerekli' } });
    const { navigate, result } = run(error);
    await expect(result).rejects.toBe(error);
    expect(navigate).toHaveBeenCalledWith(['/coaching-portal/account/payments'], { queryParams: { required: 1 } });
  });

  it('does not navigate again from the subscription page itself', async () => {
    const error = new HttpErrorResponse({ status: 403, error: { code: 'CoachingSubscriptionRequired' } });
    const { navigate, result } = run(error, '/coaching-portal/account/payments?required=1');
    await expect(result).rejects.toBe(error);
    expect(navigate).not.toHaveBeenCalled();
  });

  it('leaves other forbidden responses and successful responses alone', async () => {
    const { navigate, result } = run(new HttpErrorResponse({ status: 403, error: { code: 'Authorization.Forbidden' } }));
    await expect(result).rejects.toMatchObject({ status: 403 });
    expect(navigate).not.toHaveBeenCalled();

    const ok = new HttpResponse({ status: 200 });
    const response = await lastValueFrom(TestBed.runInInjectionContext(() =>
      coachingSubscriptionRequiredInterceptor(new HttpRequest('GET', '/api/x'), () => of(ok))));
    expect(response).toBe(ok);
  });
});
