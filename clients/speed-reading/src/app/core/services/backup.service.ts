import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface BackupInfo {
    fileName: string;
    createdAt: string;
    sizeInBytes: number;
    sizeFormatted: string;
}

export interface ScheduleConfig {
    enabled: boolean;
    cronExpression: string;
}

@Injectable({
    providedIn: 'root'
})
export class BackupService {
    private http = inject(HttpClient);
    private apiUrl = `${environment.apiUrl}/v1/backups`;

    getBackups(): Observable<BackupInfo[]> {
        return this.http.get<BackupInfo[]>(this.apiUrl);
    }

    createBackup(): Observable<{ message: string; fileName: string }> {
        return this.http.post<{ message: string; fileName: string }>(this.apiUrl, {});
    }

    deleteBackup(fileName: string): Observable<{ message: string }> {
        return this.http.delete<{ message: string }>(`${this.apiUrl}/${fileName}`);
    }

    downloadUrl(fileName: string): string {
        return `${this.apiUrl}/${fileName}/download`;
    }

    getSchedule(): Observable<ScheduleConfig> {
        return this.http.get<ScheduleConfig>(`${this.apiUrl}/schedule`);
    }

    configureSchedule(config: ScheduleConfig): Observable<{ message: string }> {
        return this.http.post<{ message: string }>(`${this.apiUrl}/schedule`, config);
    }
}
