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

export interface SpeedReadingInstitutionInvitationResponse {
  invitationId: string;
  status?: string;
  message?: string;
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

export interface SpeedReadingReportStudent {
  userId: string;
  firstName: string;
  lastName: string;
  gradeLevel?: number | null;
  teacherName?: string | null;
}

export interface SpeedReadingStudentReport {
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

export interface SpeedReadingInstitutionChartSeries {
  name: string;
  value: number;
}

export interface SpeedReadingInstitutionChartRow {
  name: string;
  series: SpeedReadingInstitutionChartSeries[];
}

export interface SpeedReadingInstitutionAssignmentReport {
  dateFrom: string;
  dateTo: string;
  dataAvailable: boolean;
  unavailableReason: string | null;
  assignmentInfo: {
    assignmentId: string;
    title: string;
    description: string;
    dueDate: string;
    assignedDate: string;
  } | null;
  completionStats: {
    totalStudents: number;
    completed: number;
    inProgress: number;
    notStarted: number;
    completionRate: number;
  } | null;
  performanceStats: {
    averageScore: number;
    medianScore: number;
    highestScore: number;
    lowestScore: number;
    standardDeviation: number;
  } | null;
  scoreDistribution: SpeedReadingInstitutionChartRow[];
  studentBreakdown: {
    studentId: string;
    studentName: string;
    status: string;
    score: number | null;
    completionTime: number | null;
    submittedAt: string | null;
  }[];
  timeStats: {
    averageCompletionTime: number;
    medianCompletionTime: number;
    fastestCompletion: number;
    slowestCompletion: number;
  } | null;
  assignmentCount: number;
}

export interface SpeedReadingInstitutionContentReport {
  dateFrom: string;
  dateTo: string;
  exerciseAnalysis: {
    exerciseTypeName: string;
    totalCompletions: number;
    activeStudents: number;
    averageScore: number;
    performanceLevel: string;
  }[];
  exerciseFrequencyChart: SpeedReadingInstitutionChartRow[];
  readingAnalysis: {
    difficultyLevel: number;
    totalReads: number;
    averageWpm: number;
    averageComprehension: number;
  }[];
  readingPerformanceChart: SpeedReadingInstitutionChartRow[];
}

export interface SpeedReadingInstitutionProgressReport {
  dateFrom: string;
  dateTo: string;
  weeklyProgressChart: SpeedReadingInstitutionChartRow[];
  monthlyProgressChart: SpeedReadingInstitutionChartRow[];
  activityIntensityChart: SpeedReadingInstitutionChartRow[];
  improvingStudents: {
    studentId: string;
    studentName: string;
    previousScore: number;
    currentScore: number;
    improvement: number;
    trend: string;
    metric: string;
  }[];
  decliningStudents: {
    studentId: string;
    studentName: string;
    previousScore: number;
    currentScore: number;
    improvement: number;
    trend: string;
    metric: string;
  }[];
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

  inviteMember(
    institutionId: string,
    email: string,
    role: SpeedReadingInstitutionMemberRole,
    teacherUserId?: string,
  ): Observable<SpeedReadingInstitutionInvitationResponse> {
    return this.http.post<SpeedReadingInstitutionInvitationResponse>(
      `${this.speedReadingUrl}/invitations/institutions/${encodeURIComponent(institutionId)}`,
      {
        email: email.trim(),
        role: role === 'Teacher' ? 2 : 1,
        ...(teacherUserId ? { teacherUserId } : {}),
      },
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

  getInstitutionAssignments(
    institutionId: string,
    dateFrom: Date,
    dateTo: Date,
  ): Observable<SpeedReadingInstitutionAssignmentReport> {
    return this.getInstitutionReport(institutionId, 'assignments', dateFrom, dateTo);
  }

  getInstitutionContentAnalysis(
    institutionId: string,
    dateFrom: Date,
    dateTo: Date,
  ): Observable<SpeedReadingInstitutionContentReport> {
    return this.getInstitutionReport(institutionId, 'content-analysis', dateFrom, dateTo);
  }

  getInstitutionTimeProgress(
    institutionId: string,
    dateFrom: Date,
    dateTo: Date,
  ): Observable<SpeedReadingInstitutionProgressReport> {
    return this.getInstitutionReport(institutionId, 'time-progress', dateFrom, dateTo);
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
  ): Observable<SpeedReadingStudentReport> {
    const baseUrl = `${this.speedReadingUrl}/analytics/institutions/${encodeURIComponent(institutionId)}/students/${encodeURIComponent(studentUserId)}`;
    const params = new HttpParams()
      .set('dateFrom', dateFrom.toISOString())
      .set('dateTo', dateTo.toISOString());
    return forkJoin({
      summary: this.http.get<SpeedReadingStudentReport['summary']>(`${baseUrl}/summary`, {
        params,
      }),
      readingSpeed: this.http.get<SpeedReadingStudentReport['readingSpeed']>(
        `${baseUrl}/reading-speed`,
        { params },
      ),
      comprehension: this.http.get<SpeedReadingStudentReport['comprehension']>(
        `${baseUrl}/comprehension`,
        { params },
      ),
      activity: this.http.get<SpeedReadingStudentReport['activity']>(`${baseUrl}/activity`, {
        params,
      }),
    });
  }

  private getInstitutionReport<T>(
    institutionId: string,
    report: 'assignments' | 'content-analysis' | 'time-progress',
    dateFrom: Date,
    dateTo: Date,
  ): Observable<T> {
    const params = new HttpParams()
      .set('dateFrom', dateFrom.toISOString())
      .set('dateTo', dateTo.toISOString());
    return this.http.get<T>(
      `${this.speedReadingUrl}/analytics/institutions/${encodeURIComponent(institutionId)}/${report}`,
      { params },
    );
  }
}
