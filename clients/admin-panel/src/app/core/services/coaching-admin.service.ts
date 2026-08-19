import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { environment } from '../../../environments/environment';

export interface CoachingAdminAssignment {
  id: string;
  teacherId: string;
  institutionId?: string;
  title: string;
  status: string;
  dueDate: string;
  studentCount: number;
  submittedStudentCount: number;
  createdAt: string;
}

export interface CoachingAdminOverview {
  totalAssignments: number;
  activeAssignments: number;
  completedAssignments: number;
  cancelledAssignments: number;
  totalAssignmentStudents: number;
  submittedAssignmentStudents: number;
  totalExams: number;
  totalExamResults: number;
  totalSessions: number;
  upcomingSessions: number;
  totalGoals: number;
  completedGoals: number;
  recentAssignments: CoachingAdminAssignment[];
}

@Injectable({ providedIn: 'root' })
export class CoachingAdminService {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiUrl}/coaching-admin`;

  getOverview(recentLimit = 10) {
    const params = new HttpParams().set('recentLimit', recentLimit);
    return this.http.get<CoachingAdminOverview>(`${this.url}/overview`, { params });
  }
}
