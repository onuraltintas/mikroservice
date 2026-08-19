import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';

export interface EmailTemplateDto {
  id: string;
  templateName: string;
  category: string;
  subject: string;
  body: string;
  isActive: boolean;
  createdAt: string;
  updatedAt?: string;
}

@Injectable({ providedIn: 'root' })
export class EmailTemplateService {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiUrl}/email-templates`;

  getAll() {
    return this.http.get<EmailTemplateDto[]>(this.url);
  }

  create(request: { templateName: string; category: string; subject: string; body: string }) {
    return this.http.post<{ templateId: string }>(this.url, request);
  }

  update(id: string, request: { category: string; subject: string; body: string; isActive: boolean }) {
    return this.http.put<void>(`${this.url}/${id}`, request);
  }
}
