import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { environment } from '../../../environments/environment';

/** Roadmap O5: LGS and YKS score and rank estimates from the student's practice exams. */
export interface ScoreTypeEstimate {
  scoreType: string; name: string; modelYear: number; score: number; scoreLow: number; scoreHigh: number;
  rank: number; rankBest: number; rankWorst: number; percentile: number | null; examsUsed: number; lastExamDate: string; includesDiplomaGrade: boolean;
}
export interface ScoreTargetGap {
  targetName: string; scoreType: string; targetScore: number | null; targetRank: number | null; targetPercentile: number | null;
  scoreGap: number | null; netGap: number | null; rankGap: number | null; reached: boolean;
}
/** KM1: YDS and YÖKDİL scores are the correct answers out of 100 (1.25 per correct answer), no yearly model. */
export interface LanguageScoreEstimate { examCode: string; name: string; score: number; scoreLow: number; scoreHigh: number; examsUsed: number; lastExamDate: string }
/** KM1: the exam date an AGS, YDS or YÖKDİL student works towards. */
export interface ScoreTargetExam { name: string; date: string; daysLeft: number; source: 'Student' | 'Session' | 'Catalog' }
export interface ScoreEstimate {
  estimates: ScoreTypeEstimate[]; target: ScoreTargetGap | null; notice: string | null;
  languageScores?: LanguageScoreEstimate[]; targetExam?: ScoreTargetExam | null;
}

@Injectable({ providedIn: 'root' })
export class CoachingScoreEstimateService {
  private readonly http = inject(HttpClient);
  /** `studentId` null is the signed-in student; a parent or teacher passes the child's id. */
  estimate(studentId: string | null, diplomaGrade: number | null = null) {
    const params = diplomaGrade == null ? new HttpParams() : new HttpParams().set('diplomaGrade', diplomaGrade);
    return this.http.get<ScoreEstimate>(`${environment.apiUrl}/coaching/students/${studentId ? encodeURIComponent(studentId) : 'me'}/score-estimate`, { params });
  }
}
