import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';

export interface ProvinceOption {
  id: string;
  name: string;
}

export interface DistrictOption extends ProvinceOption {
  provinceId: string;
}

@Injectable({ providedIn: 'root' })
export class LocationService {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiUrl}/locations`;

  getProvinces() {
    return this.http.get<ProvinceOption[]>(`${this.url}/provinces`);
  }

  getDistricts(provinceId: string) {
    return this.http.get<DistrictOption[]>(
      `${this.url}/provinces/${encodeURIComponent(provinceId)}/districts`);
  }
}
