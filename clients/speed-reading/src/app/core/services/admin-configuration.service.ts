import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface AdminConfiguration {
  id: string;
  key: string;
  value: string;
  description: string;
  dataType: number;
  isPublic: boolean;
  group: string;
}

@Injectable({ providedIn: 'root' })
export class AdminConfigurationService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiUrl}/configurations`;

  getAll(): Observable<AdminConfiguration[]> {
    return this.http.get<AdminConfiguration[]>(this.apiUrl);
  }

  update(key: string, value: string): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/${encodeURIComponent(key)}`, { value });
  }

  refreshCache(): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.apiUrl}/refresh-cache`, {});
  }
}
