import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
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
    params = params.set('pageNumber', '1');
    params = params.set('pageSize', '100');
    if (searchTerm) {
      params = params.set('search', searchTerm);
    }
    if (isActive !== undefined) {
      params = params.set('isActive', isActive.toString());
    }
    return this.http.get<any>(this.API_URL, { params }).pipe(
      map(response => (Array.isArray(response) ? response : (response?.items ?? []))
        .map((item: any) => ({
          id: item.id,
          name: item.name,
          contactEmail: item.email ?? item.contactEmail ?? '',
          phoneNumber: item.phone ?? item.phoneNumber,
          address: item.address,
          city: item.city,
          district: item.district,
          isActive: item.isActive,
          studentCount: item.studentCount ?? 0,
          teacherCount: item.teacherCount ?? 0,
          createdAt: item.createdAt ? new Date(item.createdAt) : new Date(item.subscriptionStartDate ?? 0)
        })))
    );
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
    return this.setActive(id, true);
  }

  // Deactivate institution
  deactivateInstitution(id: string): Observable<void> {
    return this.setActive(id, false);
  }

  setActive(id: string, isActive: boolean): Observable<void> {
    return this.http.post<void>(`${this.API_URL}/${id}/active`, { isActive });
  }
}
