import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { environment } from '../../../environments/environment';

/** Exam groups of the score types (Paket B, 05.11.2026). */
export type ScoreGroup = 'LGS' | 'YKS' | 'MEB-AGS';
/**
 * Roadmap O5: LGS, YKS and (Paket B, 05.11.2026) MEB-AGS score estimates from the student's practice exams. Ranks are null when
 * the model has no score-to-rank table (LGS and MEB-AGS: none is published); `margin` is the model's own ± (LGS, MEB-AGS).
 */
export interface ScoreTypeEstimate {
  scoreType: string; name: string; modelYear: number; score: number; scoreLow: number; scoreHigh: number;
  rank: number | null; rankBest: number | null; rankWorst: number | null; percentile: number | null; examsUsed: number; lastExamDate: string; includesDiplomaGrade: boolean;
  /** Paket B: absent in older responses. */
  group?: ScoreGroup; label?: string; margin?: number | null; scaleMin?: number; scaleMax?: number;
}
export interface ScoreTargetGap {
  targetName: string; scoreType: string; targetScore: number | null; targetRank: number | null; targetPercentile: number | null;
  scoreGap: number | null; netGap: number | null; rankGap: number | null; reached: boolean;
  /** 01.11.2026: 'minRawScore' when the target's score type is not computed (see ScoreEstimate.notComputed). */
  notComputedReason?: string | null;
}
/** 01.11.2026: a test's ham puan (doğru − yanlış/4), cut to hundredths, e.g. Türkçe or AYT Fen Bilimleri. */
export interface RawScoreValue { code: string; name: string; rawScore: number }
/** A failing condition: none of these tests reached the minimum (0,5 ham puan for YKS, 1 net for MEB-AGS); one would have been enough. */
export interface RawScoreCondition { anyOf: RawScoreValue[] }
/** 01.11.2026: a score type ÖSYM would not compute (2026 YKS Kılavuzu, Tablo 1D); it has no score or rank. */
export interface ScoreTypeNotComputed {
  scoreType: string; name: string; modelYear: number; notComputedReason: string; failedConditions: RawScoreCondition[]; examsUsed: number; lastExamDate: string;
  /** Paket B: 0,5 (YKS) or 1 (MEB-AGS: 1 net in every test); absent means 0,5. */
  minimumRawScore?: number; group?: ScoreGroup;
}
/** KM1: YDS and YÖKDİL scores are the correct answers out of 100 (1.25 per correct answer), no yearly model. Paket B: the YDS level (A–E). */
export interface LanguageScoreEstimate {
  examCode: string; name: string; score: number; scoreLow: number; scoreHigh: number; examsUsed: number; lastExamDate: string;
  level?: string | null; label?: string; exact?: boolean; scaleMax?: number;
}
/**
 * Paket B: the score the student works towards (the target's score type, else the purpose's: LGS, the YKS score type, MEB-AGS P2
 * of the ÖABT field or P1, YDS, the YÖKDİL field). `score` is null until there is a practice exam or when it is not computed.
 */
export interface ScoreGoalEstimate {
  scoreType: string; name: string; group: string; score: number | null; scoreLow: number | null; scoreHigh: number | null; margin: number | null;
  scaleMax: number; label: string; exact: boolean; level: string | null; notComputedReason: string | null;
}
/** KM1: the exam date an AGS, YDS or YÖKDİL student works towards. */
export interface ScoreTargetExam { name: string; date: string; daysLeft: number; source: 'Student' | 'Session' | 'Catalog' }
export interface ScoreEstimate {
  estimates: ScoreTypeEstimate[]; target: ScoreTargetGap | null; notice: string | null;
  languageScores?: LanguageScoreEstimate[]; targetExam?: ScoreTargetExam | null;
  /** 01.11.2026: absent in older responses. */
  notComputed?: ScoreTypeNotComputed[];
  /** Paket B: absent in older responses. */
  goal?: ScoreGoalEstimate | null;
}

/** KM4 (01.11.2026): the yearly models are derived from official statistics, so every view labels the estimate approximate. */
export const ESTIMATE_LABEL = 'Yaklaşık tahmin';
/** Paket B: YDS and YÖKDİL use ÖSYM's own rule on the practice exam, so they are not labelled approximate. */
export const EXACT_LABEL = 'Deneme sonucuna göre';

/** Paket B: the exam group of an estimate; older responses have no group (LGS or YKS by score type). */
export function scoreGroup(item: Pick<ScoreTypeEstimate, 'scoreType' | 'group'>): ScoreGroup {
  return item.group ?? (item.scoreType === 'LGS' ? 'LGS' : 'YKS');
}

/**
 * One line on how the estimate is made: whose statistics (ÖSYM for YKS, MEB for LGS) and which years. Paket B: an LGS model
 * without a rank table and MEB-AGS get their own sentence (no placement data, a ± range instead).
 */
export function estimateMethodNote(estimates: readonly ScoreTypeEstimate[]): string | null {
  const ranked = estimates.filter(x => scoreGroup(x) !== 'MEB-AGS' && (scoreGroup(x) === 'YKS' || x.rank !== null));
  const notes: string[] = [];
  if (ranked.length) {
    const lgs = ranked.some(x => x.scoreType === 'LGS'), yks = ranked.some(x => x.scoreType !== 'LGS');
    const source = lgs && yks ? "ÖSYM ve MEB'in" : lgs ? "MEB'in" : "ÖSYM'nin";
    const years = ranked.map(x => x.modelYear);
    const from = Math.min(...years), to = Math.max(...years);
    const year = from === to ? `${to}` : `${from}–${to}`;
    notes.push(`Yöntem: netler ${source} ${year} sınav istatistikleriyle puana, puan da ${year} yerleştirme sonuçlarıyla sıralamaya çevrilir. Resmî hesap değildir.`);
  }
  if (estimates.some(x => scoreGroup(x) === 'LGS' && x.rank === null))
    notes.push("LGS: netler MEB'in puanlama yöntemiyle puana çevrilir; tam net 500 puandır. MEB test istatistiklerini yayımlamadığı için tam nete uzaklaştıkça aralık genişler. Resmî hesap değildir.");
  const ags = estimates.filter(x => scoreGroup(x) === 'MEB-AGS');
  if (ags.length)
    notes.push(`MEB-AGS: netler ÖSYM'nin ${Math.max(...ags.map(x => x.modelYear))} test ortalamaları ve standart sapmalarıyla 100'lük puana çevrilir; ÖSYM ölçeğin ortalamasını ve dağılımını yayımlamadığı için aralık gösterilir. Resmî hesap değildir.`);
  return notes.length ? notes.join(' ') : null;
}

/** 01.11.2026: why a score type has no score, in words for both students and staff (the sentence addresses neither). */
export function notComputedText(item: (Pick<ScoreTypeNotComputed, 'notComputedReason' | 'failedConditions'> & Partial<ScoreTypeNotComputed>) | null | undefined): string {
  const lead = 'Bu puan türünde puan hesaplanmaz';
  if (item?.notComputedReason !== 'minRawScore' || !item.failedConditions?.length) return `${lead}.`;
  const tests = item.failedConditions.map(condition => `${condition.anyOf.map(x => x.name).join(' ya da ')} testinden`).join(' ve ');
  // Paket B: MEB-AGS needs 1 ham puan (1 net) in every test (2026 MEB-AGS Kılavuzu 3.10).
  return item.minimumRawScore === 1 ? `${lead}: ${tests} en az 1 net gerekir.` : `${lead}: ${tests} en az 0,5 ham puan gerekir.`;
}

/** The ham puan of the tests named in the failing conditions, e.g. "Türkçe 0,25 · Temel Matematik 0". */
export function rawScoresText(item: ScoreTypeNotComputed): string {
  return item.failedConditions.flatMap(x => x.anyOf).map(x => `${x.name} ${x.rawScore.toLocaleString('tr-TR', { maximumFractionDigits: 2 })}`).join(' · ');
}

/** YKS ranks are read from placement scores (with OBP); without the diploma grade they look worse than they are. */
export function diplomaRankNote(estimates: readonly ScoreTypeEstimate[]): string | null {
  return estimates.some(x => scoreGroup(x) === 'YKS' && !x.includesDiplomaGrade)
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
