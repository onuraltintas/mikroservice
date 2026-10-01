import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { StaffProduct } from '../auth/staff-auth.models';

export interface StaffNotification {
  id: string;
  title: string;
  message: string;
  type?: string | number;
  createdAt?: string;
  isRead: boolean;
  actionUrl?: string;
}

export interface SpeedReadingNotificationPreference {
  id: string;
  notificationType: number;
  enableInApp: boolean;
  enableEmail: boolean;
  enablePush: boolean;
  preferredTime?: string;
}

@Injectable({ providedIn: 'root' })
export class StaffNotificationsService {
  private readonly http = inject(HttpClient);
  private readonly coachingUrl = '/api/notifications';
  private readonly speedReadingUrl = '/api/speed-reading/notifications';

  getNotifications(product: StaffProduct): Observable<StaffNotification[]> {
    const url = product === 'speed-reading' ? this.speedReadingUrl : this.coachingUrl;
    return this.http.get<StaffNotification[] | { items?: StaffNotification[] }>(
      `${url}?pageNumber=1&pageSize=100`
    ).pipe(map(response => Array.isArray(response) ? response : response.items ?? []));
  }

  markAsRead(product: StaffProduct, id: string): Observable<unknown> {
    const url = product === 'speed-reading' ? this.speedReadingUrl : this.coachingUrl;
    const method = product === 'speed-reading' ? 'put' : 'post';
    return method === 'put'
      ? this.http.put(`${url}/${encodeURIComponent(id)}/mark-read`, {})
      : this.http.post(`${url}/${encodeURIComponent(id)}/mark-as-read`, {});
  }

  markAllAsRead(product: StaffProduct): Observable<unknown> {
    const url = product === 'speed-reading' ? this.speedReadingUrl : this.coachingUrl;
    const method = product === 'speed-reading' ? 'put' : 'post';
    return method === 'put'
      ? this.http.put(`${url}/mark-all-read`, {})
      : this.http.post(`${url}/mark-all-as-read`, {});
  }

  deleteNotification(product: StaffProduct, id: string): Observable<unknown> {
    const url = product === 'speed-reading' ? this.speedReadingUrl : this.coachingUrl;
    return this.http.delete(`${url}/${encodeURIComponent(id)}`);
  }

  getSpeedReadingPreferences(): Observable<SpeedReadingNotificationPreference[]> {
    return this.http.get<SpeedReadingNotificationPreference[]>(`${this.speedReadingUrl}/preferences`);
  }

  saveSpeedReadingPreferences(preferences: SpeedReadingNotificationPreference[]): Observable<unknown> {
    return this.http.put(`${this.speedReadingUrl}/preferences`, preferences);
  }
}
