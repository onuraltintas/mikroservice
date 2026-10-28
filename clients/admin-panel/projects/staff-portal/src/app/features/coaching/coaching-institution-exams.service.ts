import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { environment } from '../../../environments/environment';

/** Roadmap O4: institution-wide practice exams. */
export interface InstitutionExam { id: string; title: string; examCode: string; examName: string; examDate: string; gradeLevel: number | null; maxScore: number; participants: number }
export interface InstitutionExamPage { items: InstitutionExam[]; totalCount: number; pageNumber: number; pageSize: number }
export interface InstitutionExamSection { sectionCode: string; name: string; questionCount: number; correct: number; wrong: number; empty: number; net: number }
export interface InstitutionExamRow { studentId: string; studentName: string; gradeLevel: number | null; classBranch: string | null; net: number; score: number; rank: number; topPercent: number; sections: InstitutionExamSection[] }
export interface InstitutionExamGroup { branch: string; participants: number; averageNet: number; bestNet: number }
export interface InstitutionExamReport { exam: InstitutionExam; results: InstitutionExamRow[]; branches: InstitutionExamGroup[]; sections: { code: string; name: string; averageNet: number }[] }
export interface InstitutionExamPreview {
  fingerprint: string; rows: number;
  accepted: { line: number; studentId: string; studentName: string; net: number; replaces: boolean }[];
  errors: { line: number; message: string }[];
}
export interface InstitutionExamCreate { title: string; examCode: string; examDate: string; gradeLevel: number | null; maxScore: number }

@Injectable({ providedIn: 'root' })
export class CoachingInstitutionExamsService {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiUrl}/coaching/institution/exams`;
  list(pageNumber = 1) { return this.http.get<InstitutionExamPage>(this.url, { params: new HttpParams().set('pageNumber', pageNumber).set('pageSize', 20) }); }
  create(request: InstitutionExamCreate) { return this.http.post<InstitutionExam>(this.url, request); }
  report(examId: string, branch: string | null = null) {
    const params = branch ? new HttpParams().set('branch', branch) : new HttpParams();
    return this.http.get<InstitutionExamReport>(`${this.url}/${encodeURIComponent(examId)}`, { params });
  }
  /** CSV text, or an Excel workbook as base64 (same columns as the template). */
  preview(examId: string, csv: string | null, xlsxBase64?: string) {
    return this.http.post<InstitutionExamPreview>(`${this.url}/${encodeURIComponent(examId)}/import/preview`, xlsxBase64 ? { xlsxBase64 } : { csv });
  }
  apply(examId: string, csv: string | null, fingerprint: string, xlsxBase64?: string) {
    return this.http.post<{ applied: number }>(`${this.url}/${encodeURIComponent(examId)}/import`, xlsxBase64 ? { xlsxBase64, fingerprint } : { csv, fingerprint });
  }
  template(examCode: string) { return this.http.get(`${this.url}/template`, { params: new HttpParams().set('examCode', examCode), responseType: 'blob' }); }
}
