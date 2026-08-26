import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  StartSessionRequest,
  StartSessionResponse,
  ActionData,
  ValidationResponse,
  CompleteSessionRequest,
  ExerciseResult,
  SessionProgressDto,
  ActiveSessionDto
} from '../models/exercise-session.model';

/**
 * Exercise Session Service - Refactored for ApiResponse<T> compatibility
 * 
 * Manages real-time exercise sessions with backend
 * Supports: SchulteTable, RSVP, Tachistoscope, SpeedReading
 * 
 * CHANGES:
 * - All HTTP calls work with backend's ApiResponse<T> format
 * - ApiResponseInterceptor automatically unwraps responses
 * - Service receives clean typed data (StartSessionResponse, ExerciseResult, etc.)
 */
@Injectable({
  providedIn: 'root'
})
export class ExerciseSessionService {
  private readonly http = inject(HttpClient);
  private readonly API_URL = `${environment.speedReadingApiUrl}/exercise-sessions`;
  private readonly speedReadingApiUrl = environment.speedReadingApiUrl;

  /**
   * Start a new exercise session
   * Backend returns: ApiResponse<StartSessionResponse>
   * Service receives: StartSessionResponse (auto-unwrapped)
   * @param request Exercise ID, optional reading text ID, optional assignment ID
   * @returns Session details with initial data and configuration
   */
  startSession(request: StartSessionRequest): Observable<StartSessionResponse> {
    return this.http.post<StartSessionResponse>(`${this.API_URL}/start`, request);
  }

  /**
   * Validate a student's action/move during exercise
   * Backend returns: ApiResponse<ValidationResponse>
   * Service receives: ValidationResponse (auto-unwrapped)
   * @param sessionId The active session ID
   * @param action Action data (click, answer, advance, etc.)
   * @returns Validation result with feedback
   */
  validateAction(sessionId: string, action: ActionData): Observable<ValidationResponse> {
    const request: ActionData = {
      ...action,
      actionId: action.actionId ?? crypto.randomUUID(),
      timestamp: action.timestamp ?? new Date()
    };
    return this.http.post<ValidationResponse>(`${this.API_URL}/${sessionId}/validate`, request);
  }

  /**
   * Complete an exercise session
   * Backend returns: ApiResponse<ExerciseResult>
   * Service receives: ExerciseResult (auto-unwrapped)
   * @param sessionId The session to complete
   * @param request Optional question answers for comprehension exercises
   * @returns Exercise result with score, XP, achievements
   */
  completeSession(sessionId: string, request: CompleteSessionRequest = {}): Observable<ExerciseResult> {
    return this.http.post<ExerciseResult>(`${this.API_URL}/${sessionId}/complete`, request);
  }

  /**
   * Pause an active session
   * Backend returns: ApiResponse<{ message: string }>
   * Service receives: { message: string } (auto-unwrapped)
   * @param sessionId The session to pause
   */
  pauseSession(sessionId: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.API_URL}/${sessionId}/pause`, {});
  }

  /**
   * Resume a paused session
   * Backend returns: ApiResponse<{ message: string }>
   * Service receives: { message: string } (auto-unwrapped)
   * @param sessionId The session to resume
   */
  resumeSession(sessionId: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.API_URL}/${sessionId}/resume`, {});
  }

  /**
   * Get current progress of a session
   * Backend returns: ApiResponse<SessionProgressDto>
   * Service receives: SessionProgressDto (auto-unwrapped)
   * @param sessionId The session ID
   * @returns Progress data (steps, accuracy, time)
   */
  getProgress(sessionId: string): Observable<SessionProgressDto> {
    return this.http.get<SessionProgressDto>(`${this.API_URL}/${sessionId}/progress`);
  }

  /**
   * Get session details
   * Backend returns: ApiResponse<any>
   * Service receives: any (auto-unwrapped)
   * @param sessionId The session ID
   * @returns Full session information
   */
  getSession(sessionId: string): Observable<any> {
    return this.http.get<any>(`${this.API_URL}/${sessionId}`);
  }

  /**
   * Get all active sessions for current user
   * Backend returns: ApiResponse<ActiveSessionDto[]>
   * Service receives: ActiveSessionDto[] (auto-unwrapped)
   * @returns List of active/paused sessions
   */
  getActiveSessions(): Observable<ActiveSessionDto[]> {
    return this.http.get<ActiveSessionDto[]>(`${this.speedReadingApiUrl}/progress/active-exercise-sessions`);
  }
}
