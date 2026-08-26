import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../../environments/environment';

export interface EmailTemplate {
    id: string;
    name: string;
    code: string;
    subject: string;
    body: string;
    description: string;
    availableVariables: string;
    isActive: boolean;
    createdAt: string;
    lastModifiedAt?: string;
}

export interface CreateEmailTemplateRequest {
    name: string;
    code: string;
    subject: string;
    body: string;
    description: string;
    availableVariables: string;
    isActive: boolean;
}

export interface UpdateEmailTemplateRequest {
    name: string;
    code: string;
    subject: string;
    body: string;
    description: string;
    availableVariables: string;
    isActive: boolean;
}

@Injectable({
    providedIn: 'root'
})
export class EmailTemplatesService {
    private readonly http = inject(HttpClient);
    private readonly API_URL = `${environment.speedReadingApiUrl}/email-templates`;

    getTemplates(): Observable<EmailTemplate[]> {
        return this.http.get<any>(this.API_URL).pipe(
            map(response => Array.isArray(response) ? response : (response?.items ?? []))
        );
    }

    getTemplate(id: string): Observable<EmailTemplate> {
        return this.http.get<any>(`${this.API_URL}/${id}`).pipe(
            map(response => response?.data ?? response)
        );
    }

    createTemplate(request: CreateEmailTemplateRequest): Observable<EmailTemplate> {
        return this.http.post<any>(this.API_URL, request).pipe(
            map(response => response?.data ?? response)
        );
    }

    updateTemplate(id: string, request: UpdateEmailTemplateRequest): Observable<void> {
        return this.http.put<void>(`${this.API_URL}/${id}`, request);
    }

    deleteTemplate(id: string): Observable<void> {
        return this.http.delete<void>(`${this.API_URL}/${id}`);
    }

    previewTemplate(id: string, data: Record<string, string>): Observable<any> {
        return this.http.post<any>(`${this.API_URL}/${id}/preview`, data).pipe(
            map(response => response?.data ?? response)
        );
    }
}
