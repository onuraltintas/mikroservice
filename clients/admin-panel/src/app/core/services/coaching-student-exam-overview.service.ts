import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { environment } from '../../../environments/environment';

/** T6 (31.10.2026): every exam of a student in one list — their own, their teacher's and their institution's. */
export type StudentExamSource = 'Student' | 'Teacher' | 'Institution';
export interface StudentExamSection { sectionCode: string; name: string; questionCount: number; correct: number; wrong: number; empty: number; net: number }
export interface StudentExamOverviewItem {
  examId: string; resultId: string; source: StudentExamSource; title: string; examDate: string; examType: string;
  examCode: string | null; examName: string | null; net: number | null; score: number; maxScore: number;
  sections: StudentExamSection[]; rank: number | null; participants: number | null; topPercent: number | null;
}
export interface StudentExamTrendPoint { examId: string; examDate: string; net: number; source: StudentExamSource }
export interface StudentExamTrend { examCode: string; examName: string; maxNet: number; points: StudentExamTrendPoint[] }
export interface StudentExamSubject {
  examCode: string; sectionCode: string; name: string; questionCount: number; averageNet: number; successRatio: number; examsUsed: number; weakest: boolean;
}
export interface StudentExamOverview { exams: StudentExamOverviewItem[]; trends: StudentExamTrend[]; subjects: StudentExamSubject[] }

@Injectable({ providedIn: 'root' })
export class CoachingStudentExamOverviewService {
  private readonly http = inject(HttpClient);
  /** `studentId` null is the signed-in student; a linked parent or a teacher passes the child's id. */
  get(studentId: string | null) {
    return this.http.get<StudentExamOverview>(`${environment.apiUrl}/coaching/students/${studentId ? encodeURIComponent(studentId) : 'me'}/exams`);
  }
}
