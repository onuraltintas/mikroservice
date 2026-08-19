import { PLATFORM_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse, HttpRequest, HttpResponse } from '@angular/common/http';
import { Router } from '@angular/router';
import { lastValueFrom, of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { AuthService } from './auth.service';
import { authInterceptor } from './auth.interceptor';

describe('authInterceptor', () => {
  it('should not redirect or logout while rendering on the server', async () => {
    const logout = vi.fn();
    const navigate = vi.fn();

    TestBed.configureTestingModule({
      providers: [
        { provide: PLATFORM_ID, useValue: 'server' },
        {
          provide: AuthService,
          useValue: {
            getToken: vi.fn().mockReturnValue(null),
            logout
          }
        },
        { provide: Router, useValue: { navigate } }
      ]
    });

    const request = new HttpRequest('GET', 'http://localhost:5000/api/users/me');
    const response$ = TestBed.runInInjectionContext(() =>
      authInterceptor(request, () =>
        throwError(() => new HttpErrorResponse({ status: 401, url: request.url }))));

    await expect(lastValueFrom(response$)).rejects.toMatchObject({ status: 401 });
    expect(logout).not.toHaveBeenCalled();
    expect(navigate).not.toHaveBeenCalled();
  });

  it('adds the bearer token to same-origin API requests', async () => {
    const forwarded = vi.fn((request: HttpRequest<unknown>) =>
      of(new HttpResponse({ status: 200, url: request.url })));

    TestBed.configureTestingModule({
      providers: [
        { provide: PLATFORM_ID, useValue: 'browser' },
        {
          provide: AuthService,
          useValue: {
            getToken: vi.fn().mockReturnValue('e2e-access-token'),
            logout: vi.fn()
          }
        },
        { provide: Router, useValue: { navigate: vi.fn() } }
      ]
    });

    const request = new HttpRequest('GET', '/api/users/me');
    const response$ = TestBed.runInInjectionContext(() => authInterceptor(request, forwarded));

    await expect(lastValueFrom(response$)).resolves.toMatchObject({ status: 200 });
    expect(forwarded).toHaveBeenCalledWith(
      expect.objectContaining({
        headers: expect.objectContaining({
          get: expect.any(Function)
        })
      })
    );
    expect(forwarded.mock.calls[0][0].headers.get('Authorization')).toBe('Bearer e2e-access-token');
    expect(forwarded.mock.calls[0][0].withCredentials).toBe(true);
  });

  it('refreshes once and retries the failed API request with the rotated token', async () => {
    const forwarded = vi.fn((request: HttpRequest<unknown>) =>
      forwarded.mock.calls.length === 1
        ? throwError(() => new HttpErrorResponse({ status: 401, url: request.url }))
        : of(new HttpResponse({ status: 200, url: request.url })));
    const getToken = vi.fn()
      .mockReturnValueOnce('expired-token')
      .mockReturnValue('rotated-token');
    const refreshSession = vi.fn().mockResolvedValue(true);
    const logout = vi.fn();

    TestBed.configureTestingModule({
      providers: [
        { provide: PLATFORM_ID, useValue: 'browser' },
        {
          provide: AuthService,
          useValue: { getToken, refreshSession, logout }
        },
        { provide: Router, useValue: { navigate: vi.fn() } }
      ]
    });

    const request = new HttpRequest('GET', '/api/users/me');
    const response$ = TestBed.runInInjectionContext(() => authInterceptor(request, forwarded));

    await expect(lastValueFrom(response$)).resolves.toMatchObject({ status: 200 });
    expect(refreshSession).toHaveBeenCalledOnce();
    expect(forwarded).toHaveBeenCalledTimes(2);
    expect(forwarded.mock.calls[1][0].headers.get('Authorization')).toBe('Bearer rotated-token');
    expect(logout).not.toHaveBeenCalled();
  });
});
