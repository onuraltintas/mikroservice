import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { map } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface CoachingTimeZone {
  timeZoneId: string | null; availabilityVersion: number | null; draftVersion: number | null;
}
export function detectedTimeZone(): string {
  try { return Intl.DateTimeFormat().resolvedOptions().timeZone || 'Europe/Istanbul'; }
  catch { return 'Europe/Istanbul'; }
}
@Injectable({providedIn:'root'})
export class CoachingTimeZoneService {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiUrl}/coaching/profiles/me/time-zone`;
  get() { return this.http.get<{data:CoachingTimeZone}>(this.url).pipe(map(r=>r.data)); }
  save(value:CoachingTimeZone) { return this.http.put<{data:CoachingTimeZone}>(this.url,value).pipe(map(r=>r.data)); }
}
