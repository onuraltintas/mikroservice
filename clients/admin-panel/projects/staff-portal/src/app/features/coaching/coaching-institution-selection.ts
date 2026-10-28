import { HttpInterceptorFn } from '@angular/common/http';
import { Injectable, signal } from '@angular/core';
import { environment } from '../../../environments/environment';

export const COACHING_INSTITUTION_HEADER = 'X-Coaching-Institution';
const STORAGE_KEY = 'staff.coaching.institution';
const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/**
 * Roadmap O13: the branch an admin of several institutions works on. It is only a preference: the coaching service
 * asks Identity whether the admin manages that institution and falls back to their first institution otherwise.
 */
@Injectable({ providedIn: 'root' })
export class CoachingInstitutionSelection {
  readonly current = signal<string | null>(read());

  choose(institutionId: string | null): void {
    const value = institutionId && GUID.test(institutionId) ? institutionId : null;
    this.current.set(value);
    try {
      if (value) localStorage.setItem(STORAGE_KEY, value); else localStorage.removeItem(STORAGE_KEY);
    } catch { /* storage may be unavailable; the choice still applies to this page */ }
  }
}

function read(): string | null {
  try {
    const value = localStorage.getItem(STORAGE_KEY);
    return value && GUID.test(value) ? value : null;
  } catch { return null; }
}

/** Adds the chosen branch to coaching API calls (`/coaching/...` and `/coaching-admin/...`). */
export const coachingInstitutionInterceptor: HttpInterceptorFn = (request, next) => {
  const apiBase = environment.apiUrl.replace(/\/+$/, '');
  if (!request.url.startsWith(`${apiBase}/coaching`) || request.headers.has(COACHING_INSTITUTION_HEADER)) return next(request);
  const chosen = read();
  return next(chosen ? request.clone({ setHeaders: { [COACHING_INSTITUTION_HEADER]: chosen } }) : request);
};
