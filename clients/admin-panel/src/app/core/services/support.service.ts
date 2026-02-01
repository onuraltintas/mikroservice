import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { Observable } from 'rxjs';

export interface SupportRequest {
    firstName: string;
    lastName: string;
    email: string;
    subject: string;
    message: string;
}

@Injectable({
    providedIn: 'root'
})
export class SupportService {
    private http = inject(HttpClient);
    private apiUrl = `${environment.apiUrl}/support`;

    submitRequest(request: SupportRequest): Observable<string> {
        return this.http.post<string>(`${this.apiUrl}/submit`, request);
    }
}
