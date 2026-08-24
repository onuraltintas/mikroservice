import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../../environments/environment';

// Student Dashboard Types
export interface StudentDashboard {
  overview: StudentOverview;
  readingSpeedTrend: ChartDataPoint[];
  comprehensionTrend: ChartDataPoint[];
  recentActivities: RecentActivity[];
  // upcomingAssignments removed - Assignment system deprecated
  goals: StudentGoals;
  recentAchievements: Achievement[];
}

export interface StudentOverview {
  totalExercisesCompleted: number;
  totalReadingSessionsCompleted: number;
  averageReadingSpeed: number;
  averageComprehensionScore: number;
  currentKDP: number;
  totalStudyTimeMinutes: number;
  // assignmentsCompleted, assignmentsPending removed - Assignment system deprecated
  currentStreak: number;
  improvementPercentage: number;
}

export interface ChartDataPoint {
  label: string;
  value: number;
}

export interface RecentActivity {
  activityDate: string;
  activityType: string;
  activityName: string;
  score: number;
  readingSpeed: number;
}

// AssignmentProgress interface removed - Assignment system deprecated

export interface StudentGoals {
  dailyGoalMinutes: number;
  minutesCompletedToday: number;
  targetWPM: number;
  currentWPM: number;
  targetComprehension: number;
  currentComprehension: number;
}

export interface Achievement {
  title: string;
  description: string;
  icon: string;
  achievedDate: string;
}

// Teacher Dashboard Types
export interface TeacherDashboard {
  overview: TeacherOverview;
  studentPerformances: StudentPerformance[];
  classKDPTrend: ChartDataPoint[];
  // recentAssignments removed - Assignment system deprecated
  classDistribution: ClassDistribution;
  categoryPerformance: CategoryPerformance[];
}

export interface TeacherOverview {
  totalStudents: number;
  activeStudents: number;
  // totalAssignments, pendingAssignments removed - Assignment system deprecated
  classAverageKDP: number;
  classAverageComprehension: number;
  totalExercisesCompleted: number;
  classImprovementRate: number;
}

export interface StudentPerformance {
  studentId: string;
  studentName: string;
  // assignmentsCompleted removed - Assignment system deprecated
  exercisesCompleted: number;
  averageKDP: number;
  averageComprehension: number;
  lastActivity?: string;
  performanceLevel: string;
}

// AssignmentStatus interface removed - Assignment system deprecated

export interface ClassDistribution {
  studentsExcellent: number;
  studentsGood: number;
  studentsAverage: number;
  studentsNeedsImprovement: number;
}

export interface CategoryPerformance {
  categoryName: string;
  averageScore: number;
  totalAttempts: number;
}

// Admin Dashboard Types
export interface AdminDashboard {
  overview: AdminOverview;
  userGrowthTrend: ChartDataPoint[];
  activityTrend: ChartDataPoint[];
  topInstitutions: InstitutionStats[];
  platformHealth: PlatformHealth;
  popularContent: ContentUsage[];
  systemMetrics: SystemMetrics;
}

export interface AdminOverview {
  totalUsers: number;
  totalStudents: number;
  totalTeachers: number;
  totalInstitutions: number;
  activeUsersToday: number;
  activeUsersThisWeek: number;
  totalExercises: number;
  totalReadingTexts: number;
  totalExercisePrograms: number; // Duolingo-style exercise programs
  exercisesCompletedToday: number;
  exercisesCompletedThisWeek: number;
  exercisesCompletedThisMonth: number;
}

export interface InstitutionStats {
  institutionId: string;
  institutionName: string;
  studentCount: number;
  teacherCount: number;
  averageKDP: number;
  totalExercisesCompleted: number;
}

export interface PlatformHealth {
  overallHealthScore: number;
  userEngagementRate: number;
  contentQualityScore: number;
  systemUptime: number;
  errorCount24h: number;
}

export interface ContentUsage {
  contentId: string;
  contentTitle: string;
  contentType: string;
  usageCount: number;
  averageCompletionTime: number;
  averageScore: number;
}

export interface SystemMetrics {
  totalDatabaseSize: number;
  totalNotificationsSent: number;
  totalEmailsSent: number;
  totalReportsGenerated: number;
  lastBackupDate: string;
}

@Injectable({
  providedIn: 'root'
})
export class DashboardService {
  private http = inject(HttpClient);
  private apiUrl = `${environment.apiUrl}/admin-dashboard`;

  // Student Dashboard
  getStudentDashboard(): Observable<StudentDashboard> {
    return this.http.get<StudentDashboard>(`${this.apiUrl}/student`);
  }

  getStudentDashboardById(studentId: string): Observable<StudentDashboard> {
    return this.http.get<StudentDashboard>(`${this.apiUrl}/student/${studentId}`);
  }

  // Teacher Dashboard
  getTeacherDashboard(): Observable<TeacherDashboard> {
    return this.http.get<TeacherDashboard>(`${environment.apiUrl}/teacher-dashboard`);
  }

  getTeacherDashboardById(teacherId: string): Observable<TeacherDashboard> {
    return this.http.get<TeacherDashboard>(`${this.apiUrl}/teacher/${teacherId}`);
  }

  getInstitutionDashboardById(institutionId: string): Observable<TeacherDashboard> {
    return this.http.get<TeacherDashboard>(`${this.apiUrl}/institution/${institutionId}`);
  }

  // Admin Dashboard
  getAdminDashboard(): Observable<AdminDashboard> {
    return this.http.get<AdminDashboard>(`${this.apiUrl}`);
  }

  // Helper methods
  getPerformanceLevel(kdp: number): string {
    if (kdp >= 300) return 'Mükemmel';
    if (kdp >= 200) return 'İyi';
    if (kdp >= 100) return 'Ortalama';
    return 'Geliştirilmeli';
  }

  getPerformanceColor(kdp: number): string {
    if (kdp >= 300) return 'success';
    if (kdp >= 200) return 'primary';
    if (kdp >= 100) return 'warning';
    return 'danger';
  }

  calculateProgressPercentage(current: number, target: number): number {
    if (target === 0) return 0;
    return Math.min(100, Math.round((current / target) * 100));
  }

  formatDuration(minutes: number): string {
    const hours = Math.floor(minutes / 60);
    const mins = minutes % 60;
    if (hours > 0) {
      return `${hours}s ${mins}dk`;
    }
    return `${mins}dk`;
  }

  getStreakIcon(streak: number): string {
    if (streak >= 30) return '🔥🔥🔥';
    if (streak >= 14) return '🔥🔥';
    if (streak >= 7) return '🔥';
    return '⭐';
  }
}
