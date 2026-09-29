import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { forkJoin, Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { SpeedReadingTeacherClassOverview } from './speed-reading-teacher.service';

export type SpeedReadingInstitutionMemberRole = 'Student' | 'Teacher';

export interface SpeedReadingInstitution {
  institutionId: string;
  institutionName: string;
}

export interface SpeedReadingInstitutionMember {
  userId: string;
  firstName: string;
  lastName: string;
  email?: string | null;
  role: SpeedReadingInstitutionMemberRole;
  isMembershipActive: boolean;
  isActive: boolean;
  createdAt: string;
  currentLevel?: number | null;
  gradeLevel?: number | null;
  targetWpm?: number | null;
  targetComprehension?: number | null;
  dailyGoalMinutes?: number | null;
  learningStyle?: string | null;
  teacherUserId?: string | null;
  teacherName?: string | null;
  studentCount: number;
}

export interface SpeedReadingInstitutionMemberPage {
  items: SpeedReadingInstitutionMember[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
}

export interface SpeedReadingInstitutionMemberFilters {
  searchTerm?: string;
  gradeLevel?: number;
  isActive?: boolean;
}

export interface SpeedReadingStudentAnalyticsBenchmark {
  studentValue: number;
  institutionAverage: number;
  platformAverage: number;
  performanceLevel: string;
}

export interface SpeedReadingInstitutionStudentReport {
  summary: {
    readingSessions: number;
    averageWpm: number;
    averageComprehension: number;
    totalReadingMinutes: number;
    exercisesCompleted: number;
    exercisesPassed: number;
    averageSuccessRate: number;
    currentLevel: number;
    currentStreak: number;
    longestStreak: number;
    totalXp: number;
    dailyGoalMinutes: number;
    goalCompletionRate: number;
  };
  readingSpeed: {
    averageWpm: number;
    improvementRate: number;
    benchmark: SpeedReadingStudentAnalyticsBenchmark;
    recommendations: string[];
  };
  comprehension: {
    averageComprehension: number;
    improvementRate: number;
    totalQuestionsAttempted: number;
    correctAnswers: number;
    successRate: number;
    benchmark: SpeedReadingStudentAnalyticsBenchmark;
    weakAreas: string[];
    strongAreas: string[];
  };
  activity: {
    dataAvailable: boolean;
    unavailableReason: string | null;
    currentStreak: {
      days: number;
      longestStreak: number;
      lastActivityDate: string | null;
      isActive: boolean;
    };
    recentActivities: {
      completedAt: string;
      activityType: string;
      contentTitle: string;
      durationSeconds: number;
      wpm: number | null;
      comprehension: number | null;
      successRate: number | null;
      isMeasured: boolean;
      isPassed: boolean;
    }[];
    studyTime: {
      totalMinutes: number;
      averageSessionLength: number;
      totalSessions: number;
      mostActiveHour: number;
      mostActiveDay: string;
      consistency: number;
    };
  };
}

@Injectable({ providedIn: 'root' })
export class SpeedReadingInstitutionService {
  private readonly http = inject(HttpClient);
  private readonly speedReadingUrl = `${environment.apiUrl}/speed-reading`;

  getMyInstitution(): Observable<SpeedReadingInstitution> {
    return this.http.get<SpeedReadingInstitution>(
      `${environment.apiUrl}/institution/speed-reading/me`,
    );
  }

  getMembers(
    institutionId: string,
    role: SpeedReadingInstitutionMemberRole,
    pageNumber = 1,
    pageSize = 25,
    filters: SpeedReadingInstitutionMemberFilters = {},
  ): Observable<SpeedReadingInstitutionMemberPage> {
    const page = Math.min(
      1_000,
      Math.max(1, Math.floor(Number.isFinite(pageNumber) ? pageNumber : 1)),
    );
    const size = Math.min(100, Math.max(1, Math.floor(Number.isFinite(pageSize) ? pageSize : 25)));
    let params = new HttpParams().set('pageNumber', page).set('pageSize', size).set('role', role);
    const search = filters.searchTerm?.trim();
    if (search) params = params.set('searchTerm', search);
    if (filters.gradeLevel !== undefined && role === 'Student') {
      params = params.set('gradeLevel', filters.gradeLevel);
    }
    if (filters.isActive !== undefined) params = params.set('isActive', filters.isActive);
    return this.http.get<SpeedReadingInstitutionMemberPage>(
      `${this.speedReadingUrl}/institutions/${encodeURIComponent(institutionId)}/members`,
      { params },
    );
  }

  getClassOverview(
    institutionId: string,
    dateFrom: Date,
    dateTo: Date,
  ): Observable<SpeedReadingTeacherClassOverview> {
    const params = new HttpParams()
      .set('dateFrom', dateFrom.toISOString())
      .set('dateTo', dateTo.toISOString());
    return this.http.get<SpeedReadingTeacherClassOverview>(
      `${this.speedReadingUrl}/analytics/institutions/${encodeURIComponent(institutionId)}/class-overview`,
      { params },
    );
  }

  updateStudentProfile(
    institutionId: string,
    studentUserId: string,
    gradeLevel: number | null,
    teacherUserId: string | null,
  ): Observable<void> {
    return this.http.put<void>(
      `${this.speedReadingUrl}/institutions/${encodeURIComponent(institutionId)}/members/${encodeURIComponent(studentUserId)}/student-profile`,
      { gradeLevel, teacherUserId },
    );
  }

  setMemberStatus(
    institutionId: string,
    userId: string,
    role: SpeedReadingInstitutionMemberRole,
    isActive: boolean,
  ): Observable<void> {
    return this.http.put<void>(
      `${this.speedReadingUrl}/institutions/${encodeURIComponent(institutionId)}/members/${encodeURIComponent(userId)}`,
      { role, isActive },
    );
  }

  getStudentReport(
    institutionId: string,
    studentUserId: string,
    dateFrom: Date,
    dateTo: Date,
  ): Observable<SpeedReadingInstitutionStudentReport> {
    const baseUrl = `${this.speedReadingUrl}/analytics/institutions/${encodeURIComponent(institutionId)}/students/${encodeURIComponent(studentUserId)}`;
    const params = new HttpParams()
      .set('dateFrom', dateFrom.toISOString())
      .set('dateTo', dateTo.toISOString());
    return forkJoin({
      summary: this.http.get<SpeedReadingInstitutionStudentReport['summary']>(
        `${baseUrl}/summary`,
        {
          params,
        },
      ),
      readingSpeed: this.http.get<SpeedReadingInstitutionStudentReport['readingSpeed']>(
        `${baseUrl}/reading-speed`,
        { params },
      ),
      comprehension: this.http.get<SpeedReadingInstitutionStudentReport['comprehension']>(
        `${baseUrl}/comprehension`,
        { params },
      ),
      activity: this.http.get<SpeedReadingInstitutionStudentReport['activity']>(
        `${baseUrl}/activity`,
        {
          params,
        },
      ),
    });
  }
}
