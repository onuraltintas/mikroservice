import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../../environments/environment';
import { Observable, map } from 'rxjs';

export enum ConfigurationDataType {
    String = 0,
    Number = 1,
    Boolean = 2,
    Json = 3,
    Secret = 4
}

export interface Configuration {
    id: string;
    key: string;
    value: string;
    description: string;
    dataType: ConfigurationDataType;
    isPublic: boolean;
    group: string;
}

export interface CreateConfigurationRequest {
    key: string;
    value: string;
    description: string;
    dataType: ConfigurationDataType;
    group: string;
    isPublic: boolean;
}

export interface UpdateConfigurationRequest {
    value: string;
}

@Injectable({
    providedIn: 'root'
})
export class ConfigurationService {
    private http = inject(HttpClient);
    private apiUrl = `${environment.apiUrl}/configurations`;

    getConfigurations(): Observable<Configuration[]> {
        return this.http.get<Configuration[]>(this.apiUrl).pipe(
            map(configs => configs.map(c => this.mapResponse(c)))
        );
    }

    getConfigurationValue(key: string): Observable<string> {
        // Request text response to avoid JSON parsing errors for simple string values
        return this.http.get(`${this.apiUrl}/${key}`, { responseType: 'text' });
    }

    getPublicConfigurationValue(key: string): Observable<string> {
        return this.http.get(`${this.apiUrl}/public/${key}`, { responseType: 'text' });
    }

    createConfiguration(request: CreateConfigurationRequest): Observable<Configuration> {
        return this.http.post<Configuration>(this.apiUrl, request).pipe(
            map(c => this.mapResponse(c))
        );
    }

    updateConfiguration(key: string, request: UpdateConfigurationRequest): Observable<void> {
        return this.http.put<void>(`${this.apiUrl}/${key}`, request);
    }

    deleteConfiguration(key: string): Observable<void> {
        return this.http.delete<void>(`${this.apiUrl}/${key}`);
    }

    refreshCache(): Observable<any> {
        return this.http.post(`${this.apiUrl}/refresh-cache`, {});
    }

    getDiscoveryServices(): Observable<string[]> {
        return this.http.get<string[]>(`${environment.apiUrl}/gateway/services`);
    }

    // Helper to fix Enum serialization issue (String -> Number) from Backend
    private mapResponse(config: Configuration): Configuration {
        // If backend returns Enum as String (e.g. "Boolean"), convert to Number (2)
        if (typeof config.dataType === 'string') {
            const typeStr = config.dataType as unknown as string;
            switch (typeStr) {
                case 'String': config.dataType = ConfigurationDataType.String; break;
                case 'Number': config.dataType = ConfigurationDataType.Number; break;
                case 'Boolean': config.dataType = ConfigurationDataType.Boolean; break;
                case 'Json': config.dataType = ConfigurationDataType.Json; break;
                case 'Secret': config.dataType = ConfigurationDataType.Secret; break;
                default: config.dataType = ConfigurationDataType.String;
            }
        }
        return config;
    }
}
