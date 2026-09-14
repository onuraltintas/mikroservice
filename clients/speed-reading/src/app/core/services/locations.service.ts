import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface ProvinceOption {
  id: string;
  name: string;
}

export interface DistrictOption extends ProvinceOption {
  provinceId: string;
}

@Injectable({ providedIn: 'root' })
export class LocationsService {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiUrl}/locations`;

  getProvinces(search?: string): Observable<ProvinceOption[]> {
    const params = search?.trim()
      ? new HttpParams().set('search', search.trim())
      : undefined;
    return this.http.get<ProvinceOption[]>(`${this.url}/provinces`, { params });
  }

  getDistricts(provinceId: string, search?: string): Observable<DistrictOption[]> {
    const params = search?.trim()
      ? new HttpParams().set('search', search.trim())
      : undefined;
    return this.http.get<DistrictOption[]>(
      `${this.url}/provinces/${encodeURIComponent(provinceId)}/districts`,
      { params });
  }
}
