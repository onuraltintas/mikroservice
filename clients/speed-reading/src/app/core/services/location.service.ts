import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface City {
    id: string;
    name: string;
    plateCode: number;
}

export interface District {
    id: string;
    name: string;
    cityId: string;
}

@Injectable({
    providedIn: 'root'
})
export class LocationService {
    private readonly http = inject(HttpClient);
    private readonly API_URL = environment.apiUrl;

    getCities(): Observable<City[]> {
        return this.http.get<City[]>(`${this.API_URL}/v1/location/cities`);
    }

    getDistricts(cityId: string): Observable<District[]> {
        return this.http.get<District[]>(`${this.API_URL}/v1/location/districts/${cityId}`);
    }
}
