import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { environment } from '../../../environments/environment';

/** Roadmap O4: the student's own place in each institution-wide practice exam. */
export interface StudentInstitutionExamRank { examId: string; title: string; examName: string; examDate: string; net: number; score: number; rank: number; participants: number; topPercent: number }

@Injectable({ providedIn: 'root' })
export class CoachingInstitutionExamRanksService {
  private readonly http = inject(HttpClient);
  mine() { return this.http.get<StudentInstitutionExamRank[]>(`${environment.apiUrl}/coaching/students/me/institution-exams`); }
}
