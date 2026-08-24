import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
    LegalDocument,
    UserLegalAcceptance,
    CreateLegalDocumentDto,
    AcceptLegalDocumentDto,
    LegalDocumentType
} from '../models/legal-document.model';

@Injectable({
    providedIn: 'root'
})
export class LegalDocumentService {
    private apiUrl = '/api/legal';

    constructor(private http: HttpClient) { }

    // Public endpoints
    getActiveDocuments(type?: LegalDocumentType, language: string = 'tr'): Observable<LegalDocument[]> {
        let params = new HttpParams().set('language', language);
        if (type) {
            params = params.set('type', type);
        }
        return this.http.get<LegalDocument[]>(`${this.apiUrl}/active`, { params });
    }

    getDocumentById(id: string): Observable<LegalDocument> {
        return this.http.get<LegalDocument>(`${this.apiUrl}/${id}`);
    }

    acceptDocument(dto: AcceptLegalDocumentDto): Observable<UserLegalAcceptance> {
        return this.http.post<UserLegalAcceptance>(`${this.apiUrl}/accept`, dto);
    }

    // Admin endpoints
    getAllDocuments(): Observable<LegalDocument[]> {
        return this.http.get<LegalDocument[]>(`${this.apiUrl}/admin/all`);
    }

    createDocument(dto: CreateLegalDocumentDto): Observable<LegalDocument> {
        return this.http.post<LegalDocument>(`${this.apiUrl}/admin`, dto);
    }

    updateDocument(id: string, dto: CreateLegalDocumentDto): Observable<LegalDocument> {
        return this.http.put<LegalDocument>(`${this.apiUrl}/admin/${id}`, dto);
    }

    deleteDocument(id: string): Observable<void> {
        return this.http.delete<void>(`${this.apiUrl}/admin/${id}`);
    }

    publishDocument(id: string): Observable<LegalDocument> {
        return this.http.post<LegalDocument>(`${this.apiUrl}/admin/${id}/publish`, {});
    }

    getUserAcceptances(): Observable<UserLegalAcceptance[]> {
        return this.http.get<UserLegalAcceptance[]>(`${this.apiUrl}/admin/acceptances`);
    }
}
