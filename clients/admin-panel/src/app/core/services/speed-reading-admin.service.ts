import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { environment } from '../../../environments/environment';

export interface SpeedReadingCapabilities {
  mode: 'Standalone' | 'Platform' | string;
  coachingIntegrationEnabled: boolean;
  notificationIntegrationEnabled: boolean;
  subscriptionIntegrationEnabled: boolean;
}

export interface SpeedReadingPage<T> {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
}

export interface SpeedReadingExerciseType {
  id: string;
  name: string;
  displayName: string;
  description: string;
  iconName: string;
  colorCode: string;
  sortOrder: number;
  isActive: boolean;
  engineType: string;
  categoryId: string | null;
}

@Injectable({ providedIn: 'root' })
export class SpeedReadingAdminService {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiUrl}/speed-reading`;

  getCapabilities() {
    return this.http.get<SpeedReadingCapabilities>(`${this.url}/capabilities`);
  }

  getExerciseTypes(pageNumber = 1, pageSize = 20) {
    const params = new HttpParams()
      .set('pageNumber', pageNumber)
      .set('pageSize', pageSize);

    return this.http.get<SpeedReadingPage<SpeedReadingExerciseType>>(
      `${this.url}/exercise-types`,
      { params }
    );
  }
}
