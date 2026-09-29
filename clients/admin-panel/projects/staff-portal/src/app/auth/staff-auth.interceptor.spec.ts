import { HttpErrorResponse, HttpRequest, HttpResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { lastValueFrom, of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { StaffAuthService } from './staff-auth.service';
import { staffAuthInterceptor } from './staff-auth.interceptor';

describe('staffAuthInterceptor', () => {
  it('attaches the current in-memory access token to API requests', async () => {
    const forward = vi.fn((request: HttpRequest<unknown>) =>
      of(new HttpResponse({ status: 200, body: request })));
    TestBed.configureTestingModule({
      providers: [{ provide: StaffAuthService, useValue: { getAccessToken: () => 'staff-token' } }]
    });

    const request = new HttpRequest('GET', '/api/coaching/students');
    const response = await lastValueFrom(TestBed.runInInjectionContext(() =>
      staffAuthInterceptor(request, forward)));
    const forwarded = (response as HttpResponse<HttpRequest<unknown>>).body;

    expect(forwarded?.headers.get('Authorization')).toBe('Bearer staff-token');
    expect(forwarded?.withCredentials).toBe(true);
  });

  it('refreshes once and retries an expired API request with the rotated token', async () => {
    const forward = vi.fn((request: HttpRequest<unknown>) =>
      forward.mock.calls.length === 1
        ? throwError(() => new HttpErrorResponse({ status: 401, url: request.url }))
        : of(new HttpResponse({ status: 200, url: request.url })));
    const getAccessToken = vi.fn()
      .mockReturnValueOnce('expired-token')
      .mockReturnValue('rotated-token');
    const refreshSession = vi.fn().mockResolvedValue(true);
    TestBed.configureTestingModule({
      providers: [{ provide: StaffAuthService, useValue: { getAccessToken, refreshSession } }]
    });

    const request = new HttpRequest('GET', '/api/coaching/students');
    const response = TestBed.runInInjectionContext(() => staffAuthInterceptor(request, forward));

    await expect(lastValueFrom(response)).resolves.toMatchObject({ status: 200 });
    expect(refreshSession).toHaveBeenCalledOnce();
    expect(forward).toHaveBeenCalledTimes(2);
    expect(forward.mock.calls[1][0].headers.get('Authorization')).toBe('Bearer rotated-token');
  });

  it('does not refresh or retry product login failures', async () => {
    const forward = vi.fn((request: HttpRequest<unknown>) =>
      throwError(() => new HttpErrorResponse({ status: 401, url: request.url })));
    const refreshSession = vi.fn();
    TestBed.configureTestingModule({
      providers: [{ provide: StaffAuthService, useValue: { getAccessToken: () => '', refreshSession } }]
    });

    const request = new HttpRequest('POST', '/api/auth/coaching/login', {});
    const response = TestBed.runInInjectionContext(() => staffAuthInterceptor(request, forward));

    await expect(lastValueFrom(response)).rejects.toMatchObject({ status: 401 });
    expect(refreshSession).not.toHaveBeenCalled();
    expect(forward).toHaveBeenCalledOnce();
  });
});
