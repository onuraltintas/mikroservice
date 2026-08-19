import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { environment } from '../../../environments/environment';

export interface InstitutionDto {
  id: string;
  name: string;
  type: number;
  logoUrl?: string;
  address?: string;
  city?: string;
  district?: string;
  phone?: string;
  email?: string;
  website?: string;
  licenseType: number;
  maxStudents: number;
  maxTeachers: number;
  subscriptionStartDate?: string;
  subscriptionEndDate?: string;
  isActive: boolean;
  studentCount: number;
  teacherCount: number;
  adminCount: number;
}

export interface PagedInstitutions {
  items: InstitutionDto[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
}

export interface InstitutionAdminDto {
  userId: string;
  email: string;
  firstName: string;
  lastName: string;
  role: number;
  isActive: boolean;
}

@Injectable({ providedIn: 'root' })
export class InstitutionService {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiUrl}/institutions`;

  getAll(page = 1, pageSize = 25, search = '', isActive?: boolean) {
    let params = new HttpParams().set('pageNumber', page).set('pageSize', pageSize);
    if (search.trim()) params = params.set('search', search.trim());
    if (isActive !== undefined) params = params.set('isActive', isActive);
    return this.http.get<PagedInstitutions>(this.url, { params });
  }

  create(request: { name: string; type: number; city?: string; email?: string }) {
    return this.http.post<{ institutionId: string }>(this.url, request);
  }

  update(id: string, request: Partial<InstitutionDto>) {
    return this.http.put<void>(`${this.url}/${id}`, request);
  }

  setActive(id: string, isActive: boolean) {
    return this.http.post<void>(`${this.url}/${id}/active`, { isActive });
  }

  assignAdmin(id: string, userId: string, role: number) {
    return this.http.post<void>(`${this.url}/${id}/admins`, { userId, role });
  }

  getAdmins(id: string) {
    return this.http.get<InstitutionAdminDto[]>(`${this.url}/${id}/admins`);
  }

  setAdminActive(id: string, userId: string, isActive: boolean) {
    return this.http.post<void>(`${this.url}/${id}/admins/${userId}/active`, { isActive });
  }
}
