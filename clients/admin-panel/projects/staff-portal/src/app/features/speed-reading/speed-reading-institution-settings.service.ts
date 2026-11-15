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
      })));
  }

  updateInstitutionProfile(
    institutionId: string,
    request: UpdateSpeedReadingInstitutionProfileRequest,
  ): Observable<void> {
    return this.http.put<void>(`${this.institutionsUrl}/${encodeURIComponent(institutionId)}`, request);
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
