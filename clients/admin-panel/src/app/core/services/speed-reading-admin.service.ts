import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { environment } from '../../../environments/environment';

export interface SpeedReadingCapabilities {
  mode: 'Standalone' | 'Platform' | string;
  coachingIntegrationEnabled: boolean;
  notificationIntegrationEnabled: boolean;
  subscriptionIntegrationEnabled: boolean;
}

@Injectable({ providedIn: 'root' })
export class SpeedReadingAdminService {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiUrl}/speed-reading`;

  getCapabilities() {
    return this.http.get<SpeedReadingCapabilities>(`${this.url}/capabilities`);
  }
}
