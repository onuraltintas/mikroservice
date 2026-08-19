import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { environment } from '../../../environments/environment';

export interface SupportRequestDto {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  subject: string;
  message: string;
  isProcessed: boolean;
  adminNote?: string;
  createdAt: string;
  updatedAt?: string;
}

export interface PagedSupportRequests {
  items: SupportRequestDto[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
}

@Injectable({ providedIn: 'root' })
export class SupportAdminService {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiUrl}/support/requests`;

  getAll(page = 1, pageSize = 25, isProcessed?: boolean, search = '') {
    let params = new HttpParams().set('pageNumber', page).set('pageSize', pageSize);
    if (isProcessed !== undefined) params = params.set('isProcessed', isProcessed);
    if (search.trim()) params = params.set('search', search.trim());
    return this.http.get<PagedSupportRequests>(this.url, { params });
  }

  process(id: string, adminNote?: string) {
    return this.http.post<void>(`${this.url}/${id}/process`, { adminNote });
  }

  reply(id: string, replyMessage: string) {
    return this.http.post<void>(`${this.url}/${id}/reply`, { replyMessage });
  }
}
