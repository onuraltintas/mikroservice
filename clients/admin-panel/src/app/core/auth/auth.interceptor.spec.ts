import { PLATFORM_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse, HttpRequest } from '@angular/common/http';
import { Router } from '@angular/router';
import { lastValueFrom, throwError } from 'rxjs';
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
});
