import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Institution, CreateInstitutionRequest, UpdateInstitutionRequest } from '../models/institution.model';

@Injectable({
  providedIn: 'root'
})
export class InstitutionsService {
  private readonly http = inject(HttpClient);
  private readonly API_URL = `${environment.apiUrl}/v1/institutions`;

  // Get all institutions with optional filter
  getInstitutions(searchTerm?: string, isActive?: boolean): Observable<Institution[]> {
    let params = new HttpParams();
    if (searchTerm) {
      params = params.set('searchTerm', searchTerm);
    }
    if (isActive !== undefined) {
      params = params.set('isActive', isActive.toString());
    }
    return this.http.get<Institution[]>(this.API_URL, { params });
  }

  // Get single institution by ID
  getInstitutionById(id: string): Observable<Institution> {
    return this.http.get<Institution>(`${this.API_URL}/${id}`);
  }

  // Create new institution
  createInstitution(request: CreateInstitutionRequest): Observable<Institution> {
    return this.http.post<Institution>(this.API_URL, request);
  }

  // Update existing institution
  updateInstitution(id: string, request: UpdateInstitutionRequest): Observable<Institution> {
    return this.http.put<Institution>(`${this.API_URL}/${id}`, request);
  }

  // Delete institution
  deleteInstitution(id: string): Observable<void> {
    return this.http.delete<void>(`${this.API_URL}/${id}`);
  }

  // Activate institution
  activateInstitution(id: string): Observable<void> {
    return this.http.post<void>(`${this.API_URL}/${id}/activate`, {});
  }

  // Deactivate institution
  deactivateInstitution(id: string): Observable<void> {
    return this.http.post<void>(`${this.API_URL}/${id}/deactivate`, {});
  }
}
