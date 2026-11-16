import { HttpInterceptorFn } from '@angular/common/http';
import { NgZone, inject } from '@angular/core';
import { Observable } from 'rxjs';

/**
 * Live test 16.11.2026 (#50): on a page opened by its address, the answer of a request could arrive outside Angular's zone.
 * The data was set, but the page was not drawn again until the next click ("Bildirimler yükleniyor…" stayed; the learning
 * path page stayed empty). Every answer is handed to the application inside the zone, so the page is drawn when it arrives.
 */
export const zoneReentryInterceptor: HttpInterceptorFn = (request, next) => {
  const zone = inject(NgZone);
  return new Observable(observer => {
    const subscription = next(request).subscribe({
      next: event => NgZone.isInAngularZone() ? observer.next(event) : zone.run(() => observer.next(event)),
      error: error => NgZone.isInAngularZone() ? observer.error(error) : zone.run(() => observer.error(error)),
      complete: () => NgZone.isInAngularZone() ? observer.complete() : zone.run(() => observer.complete())
    });
    return () => subscription.unsubscribe();
  });
};
