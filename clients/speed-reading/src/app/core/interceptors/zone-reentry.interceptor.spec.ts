import { HttpClient, HttpHandlerFn, HttpRequest, HttpResponse, provideHttpClient, withInterceptors } from '@angular/common/http';
import { NgZone } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Observable } from 'rxjs';
import { zoneReentryInterceptor } from './zone-reentry.interceptor';

describe('zoneReentryInterceptor (live test 16.11.2026, #50)', () => {
  it('hands an answer that arrives outside the zone to the application inside it', done => {
    // The innermost interceptor answers from outside Angular's zone, as the stuck pages saw it.
    const outside = (request: HttpRequest<unknown>, _next: HttpHandlerFn) => new Observable<HttpResponse<unknown>>(observer => {
      TestBed.inject(NgZone).runOutsideAngular(() => setTimeout(() => {
        expect(NgZone.isInAngularZone()).toBeFalse();
        observer.next(new HttpResponse({ body: { ok: true }, status: 200, url: request.url }));
        observer.complete();
      }));
    });
    TestBed.configureTestingModule({ providers: [provideHttpClient(withInterceptors([zoneReentryInterceptor, outside]))] });
    const zone = TestBed.inject(NgZone);
    zone.run(() => TestBed.inject(HttpClient).get('/api/x').subscribe({
      next: body => { expect(NgZone.isInAngularZone()).toBeTrue(); expect(body).toEqual({ ok: true }); },
      complete: () => { expect(NgZone.isInAngularZone()).toBeTrue(); done(); }
    }));
  });
});
