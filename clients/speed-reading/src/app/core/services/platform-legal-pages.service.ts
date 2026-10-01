import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { map } from 'rxjs/operators';
import { environment } from '../../../environments/environment';

export interface PlatformLegalPage {
  slug: string;
  title: string;
  content: string;
  isPublished: boolean;
  version: number;
  createdAt: string;
  updatedAt?: string | null;
}

@Injectable({ providedIn: 'root' })
export class PlatformLegalPagesService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiUrl}/platform/legal-pages`;

  getPage(slug: string) {
    return this.http.get<{ success: boolean; data: PlatformLegalPage }>(`${this.apiUrl}/${encodeURIComponent(slug)}`)
      .pipe(map(response => response.data));
  }
}
