import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface RSVPSession {
  id: string;
  userId: string;
  textId?: string;
  textContent: string;
  wordsPerMinute: number;
  fontFamily: string;
  fontSize: number;
  backgroundColor: string;
  textColor: string;
  totalWords: number;
  completedWords: number;
  sessionDuration: number;
  completed: boolean;
  createdAt: Date;
  completedAt?: Date;
}

export interface CreateRSVPSessionRequest {
  textId?: string;
  textContent: string;
  wordsPerMinute: number;
  fontFamily?: string;
  fontSize?: number;
  backgroundColor?: string;
  textColor?: string;
}

export interface UpdateRSVPSessionRequest {
  completedWords: number;
  sessionDuration: number;
  completed: boolean;
}

@Injectable({
  providedIn: 'root'
})
export class RsvpService {
  private apiUrl = `${environment.apiUrl}/v1/rsvp-sessions`;

  constructor(private http: HttpClient) { }

  createSession(request: CreateRSVPSessionRequest): Observable<RSVPSession> {
    return this.http.post<RSVPSession>(this.apiUrl, request);
  }

  getSession(id: string): Observable<RSVPSession> {
    return this.http.get<RSVPSession>(`${this.apiUrl}/${id}`);
  }

  getUserSessions(): Observable<RSVPSession[]> {
    return this.http.get<RSVPSession[]>(`${this.apiUrl}/user`);
  }

  updateSession(id: string, request: UpdateRSVPSessionRequest): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/${id}`, request);
  }

  deleteSession(id: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }
}
