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

/** KM4 (01.11.2026): the yearly models are derived from official statistics, so every view labels the estimate approximate. */
export const ESTIMATE_LABEL = 'Yaklaşık tahmin';

/** One line on how the estimate is made: whose statistics (ÖSYM for YKS, MEB for LGS) and which years. */
export function estimateMethodNote(estimates: readonly ScoreTypeEstimate[]): string | null {
  if (!estimates.length) return null;
  const lgs = estimates.some(x => x.scoreType === 'LGS'), yks = estimates.some(x => x.scoreType !== 'LGS');
  const source = lgs && yks ? "ÖSYM ve MEB'in" : lgs ? "MEB'in" : "ÖSYM'nin";
  const years = estimates.map(x => x.modelYear);
  const from = Math.min(...years), to = Math.max(...years);
  const year = from === to ? `${to}` : `${from}–${to}`;
  return `Yöntem: netler ${source} ${year} sınav istatistikleriyle puana, puan da ${year} yerleştirme sonuçlarıyla sıralamaya çevrilir. Resmî hesap değildir.`;
}

/** YKS ranks are read from placement scores (with OBP); without the diploma grade they look worse than they are. */
export function diplomaRankNote(estimates: readonly ScoreTypeEstimate[]): string | null {
  return estimates.some(x => x.scoreType !== 'LGS' && !x.includesDiplomaGrade)
    ? 'Diploma notu eklenmediği için YKS sıralaması olduğundan geride görünebilir.' : null;
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
