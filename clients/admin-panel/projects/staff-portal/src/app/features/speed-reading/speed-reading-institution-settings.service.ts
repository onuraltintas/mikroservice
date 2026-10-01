import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../../environments/environment';

export interface SpeedReadingInstitutionIdentity {
  institutionId: string;
  institutionName: string;
}

export interface SpeedReadingInstitutionProfile {
  id: string;
  name: string;
  code?: string | null;
  contactEmail: string;
  phoneNumber?: string | null;
  address?: string | null;
  provinceId?: string | null;
  districtId?: string | null;
  provinceName?: string | null;
  districtName?: string | null;
  studentCount: number;
  teacherCount: number;
  maxStudents?: number | null;
  maxTeachers?: number | null;
  subscriptionStartDate?: string | null;
  subscriptionEndDate?: string | null;
}

export interface SpeedReadingInstitutionAccess {
  institutionId: string;
  plan: { name: string };
  status: string;
  seatCount: number;
  usedSeatCount: number;
  startDate: string;
  endDate: string;
  paymentReference?: string | null;
}

export interface InstitutionLocationOption {
  id: string;
  name: string;
  provinceId?: string;
}

export interface UpdateSpeedReadingInstitutionProfileRequest {
  name: string;
  email: string;
  phone?: string;
  address?: string;
  provinceId?: string;
  districtId?: string;
}

@Injectable({ providedIn: 'root' })
export class SpeedReadingInstitutionSettingsService {
  private readonly http = inject(HttpClient);
  private readonly institutionsUrl = `${environment.apiUrl}/v1/institutions`;
  private readonly speedReadingUrl = `${environment.apiUrl}/speed-reading`;

  getMyInstitution(): Observable<SpeedReadingInstitutionIdentity> {
    return this.http.get<SpeedReadingInstitutionIdentity>(`${environment.apiUrl}/institution/speed-reading/me`);
  }

  getInstitutionProfile(institutionId: string): Observable<SpeedReadingInstitutionProfile> {
    return this.http.get<Record<string, unknown>>(`${this.institutionsUrl}/${encodeURIComponent(institutionId)}`)
      .pipe(map(item => ({
        id: String(item['id'] ?? institutionId),
        name: String(item['name'] ?? ''),
        code: typeof item['code'] === 'string' ? item['code'] : null,
        contactEmail: String(item['email'] ?? item['contactEmail'] ?? ''),
        phoneNumber: typeof (item['phone'] ?? item['phoneNumber']) === 'string' ? String(item['phone'] ?? item['phoneNumber']) : null,
        address: typeof item['address'] === 'string' ? item['address'] : null,
        provinceId: typeof item['provinceId'] === 'string' ? item['provinceId'] : null,
        districtId: typeof item['districtId'] === 'string' ? item['districtId'] : null,
        provinceName: typeof item['province'] === 'string' ? item['province'] : typeof item['city'] === 'string' ? item['city'] : null,
        districtName: typeof item['district'] === 'string' ? item['district'] : null,
        studentCount: Number(item['studentCount'] ?? 0),
        teacherCount: Number(item['teacherCount'] ?? 0),
        maxStudents: typeof item['maxStudents'] === 'number' ? item['maxStudents'] : null,
        maxTeachers: typeof item['maxTeachers'] === 'number' ? item['maxTeachers'] : null,
        subscriptionStartDate: typeof item['subscriptionStartDate'] === 'string' ? item['subscriptionStartDate'] : null,
        subscriptionEndDate: typeof item['subscriptionEndDate'] === 'string' ? item['subscriptionEndDate'] : null,
      })));
  }

  updateInstitutionProfile(
    institutionId: string,
    request: UpdateSpeedReadingInstitutionProfileRequest,
  ): Observable<void> {
    return this.http.put<void>(`${this.institutionsUrl}/${encodeURIComponent(institutionId)}`, request);
  }

  getMySpeedReadingAccess(): Observable<SpeedReadingInstitutionAccess | null> {
    return this.http.get<SpeedReadingInstitutionAccess | null>(
      `${this.speedReadingUrl}/subscriptions/institution-access/my`,
    );
  }

  getProvinces(): Observable<InstitutionLocationOption[]> {
    return this.http.get<InstitutionLocationOption[]>(`${environment.apiUrl}/locations/provinces`);
  }

  getDistricts(provinceId: string): Observable<InstitutionLocationOption[]> {
    return this.http.get<InstitutionLocationOption[]>(
      `${environment.apiUrl}/locations/provinces/${encodeURIComponent(provinceId)}/districts`,
    );
  }
}
