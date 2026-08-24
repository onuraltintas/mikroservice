import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import {
    TeacherContentAnalysisReport,
    TeacherTimeBasedProgressReport,
    TeacherClassOverviewReport,
    ChartData
} from '../models/report.model';

// Teacher-specific report interfaces matching backend DTOs
export interface TeacherStudentReadingSpeedReport {
    metadata: {
        reportTitle: string;
        reportType: string;
        generatedAt: Date;
        startDate: Date;
        endDate: Date;
        generatedFor: string;
        totalDays: number;
    };
    currentWPM: number;
    averageWPM: number;
    maxWPM: number;
    minWPM: number;
    wpmImprovement: number;
    wpmOverTime: Array<{
        name: string;
        series: Array<{ name: string; value: number }>;
    }>;
    wpmByExerciseType: Array<{
        name: string;
        series: Array<{ name: string; value: number }>;
    }>;
    recommendations: string[];
}

export interface TeacherStudentComprehensionReport {
    metadata: {
        reportTitle: string;
        reportType: string;
        generatedAt: Date;
        startDate: Date;
        endDate: Date;
        generatedFor: string;
        totalDays: number;
    };
    currentComprehension: number;
    averageComprehension: number;
    maxComprehension: number;
    minComprehension: number;
    comprehensionImprovement: number;
    comprehensionOverTime: Array<{
        name: string;
        series: Array<{ name: string; value: number }>;
    }>;
    weakAreas: string[];
    strongAreas: string[];
}

export interface TeacherStudentActivityReport {
    metadata: {
        reportTitle: string;
        reportType: string;
        generatedAt: Date;
        startDate: Date;
        endDate: Date;
        generatedFor: string;
        totalDays: number;
    };
    dataAvailable?: boolean;
    unavailableReason?: string;
    totalActivities: number;
    totalReadingMinutes: number;
    daysActive: number;
    averageDailyMinutes: number;
    currentStreak: number;
    longestStreak: number;
    activityByType: Array<{
        name: string;
        series: Array<{ name: string; value: number }>;
    }>;
    dailyActivity: Array<{
        name: string;
        series: Array<{ name: string; value: number }>;
    }>;
    mostActiveDay: string;
}

@Injectable({
    providedIn: 'root'
})
export class TeacherReportService {
    private readonly http = inject(HttpClient);
    private readonly speedReadingAnalyticsUrl = `${environment.speedReadingApiUrl}/analytics/teacher`;

    getStudentReadingSpeedTrend(studentId: string, days: number = 30): Observable<TeacherStudentReadingSpeedReport> {
        const { dateFrom, dateTo } = this.getDateRange(days);
        const params = new HttpParams()
            .set('dateFrom', dateFrom.toISOString())
            .set('dateTo', dateTo.toISOString());
        return this.http.get<StudentReadingSpeedAnalytics>(
            `${this.speedReadingAnalyticsUrl}/students/${studentId}/reading-speed`,
            { params }
        ).pipe(map(value => this.toTeacherReadingSpeedReport(value, studentId)));
    }

    getStudentComprehensionTrend(studentId: string, days: number = 30): Observable<TeacherStudentComprehensionReport> {
        const { dateFrom, dateTo } = this.getDateRange(days);
        const params = new HttpParams()
            .set('dateFrom', dateFrom.toISOString())
            .set('dateTo', dateTo.toISOString());
        return this.http.get<StudentComprehensionAnalytics>(
            `${this.speedReadingAnalyticsUrl}/students/${studentId}/comprehension`,
            { params }
        ).pipe(map(value => this.toTeacherComprehensionReport(value, studentId)));
    }

    getStudentActivityReport(studentId: string, startDate?: Date, endDate?: Date): Observable<TeacherStudentActivityReport> {
        let params = new HttpParams();
        if (startDate) params = params.set('dateFrom', startDate.toISOString());
        if (endDate) params = params.set('dateTo', endDate.toISOString());
        return this.http.get<StudentActivityAnalytics>(
            `${this.speedReadingAnalyticsUrl}/students/${studentId}/activity`,
            { params }
        ).pipe(map(value => this.toTeacherActivityReport(value, studentId)));
    }

    getContentAnalysisReport(teacherId: string, startDate?: Date, endDate?: Date): Observable<TeacherContentAnalysisReport> {
        let params = new HttpParams();
        if (startDate) params = params.set('dateFrom', startDate.toISOString());
        if (endDate) params = params.set('dateTo', endDate.toISOString());
        void teacherId;
        return this.http.get<TeacherContentAnalysisAnalytics>(
            `${this.speedReadingAnalyticsUrl}/content-analysis`,
            { params }
        ).pipe(map(value => this.toTeacherContentAnalysisReport(value)));
    }

    getAdminContentAnalysisReport(teacherId: string, startDate?: Date, endDate?: Date): Observable<TeacherContentAnalysisReport> {
        let params = new HttpParams();
        if (startDate) params = params.set('dateFrom', startDate.toISOString());
        if (endDate) params = params.set('dateTo', endDate.toISOString());
        return this.http.get<TeacherContentAnalysisAnalytics>(
            `${environment.speedReadingApiUrl}/analytics/admin/teachers/${teacherId}/content-analysis`,
            { params }
        ).pipe(map(value => this.toTeacherContentAnalysisReport(value)));
    }

    getTimeBasedProgressReport(teacherId: string, startDate?: Date, endDate?: Date): Observable<TeacherTimeBasedProgressReport> {
        let params = new HttpParams();
        if (startDate) params = params.set('dateFrom', startDate.toISOString());
        if (endDate) params = params.set('dateTo', endDate.toISOString());
        void teacherId;
        return this.http.get<TeacherTimeProgressAnalytics>(
            `${this.speedReadingAnalyticsUrl}/time-progress`,
            { params }
        ).pipe(map(value => this.toTeacherTimeProgressReport(value)));
    }

    getAdminTimeBasedProgressReport(teacherId: string, startDate?: Date, endDate?: Date): Observable<TeacherTimeBasedProgressReport> {
        let params = new HttpParams();
        if (startDate) params = params.set('dateFrom', startDate.toISOString());
        if (endDate) params = params.set('dateTo', endDate.toISOString());
        return this.http.get<TeacherTimeProgressAnalytics>(
            `${environment.speedReadingApiUrl}/analytics/admin/teachers/${teacherId}/time-progress`,
            { params }
        ).pipe(map(value => this.toTeacherTimeProgressReport(value)));
    }

    private getDateRange(days: number): { dateFrom: Date; dateTo: Date } {
        const dateTo = new Date();
        const rangeDays = Math.min(Math.max(Math.trunc(days) || 1, 1), 366);
        const dateFrom = new Date(dateTo.getTime() - rangeDays * 24 * 60 * 60 * 1000);
        return { dateFrom, dateTo };
    }

    private toTeacherReadingSpeedReport(
        value: StudentReadingSpeedAnalytics,
        studentId: string
    ): TeacherStudentReadingSpeedReport {
        return {
            metadata: this.toTeacherMetadata(
                'Öğrenci Okuma Hızı Raporu',
                'TeacherStudentReadingSpeed',
                studentId,
                value.dateFrom,
                value.dateTo
            ),
            currentWPM: value.currentWpm,
            averageWPM: value.averageWpm,
            maxWPM: value.maxWpm,
            minWPM: value.minWpm,
            wpmImprovement: value.improvementRate,
            wpmOverTime: (value.trend ?? []).map(point => ({
                name: point.date,
                series: [{ name: 'WPM', value: point.value }]
            })),
            wpmByExerciseType: (value.categories ?? []).map(category => ({
                name: category.categoryName,
                series: [{ name: 'WPM', value: category.value }]
            })),
            recommendations: value.recommendations ?? []
        };
    }

    private toTeacherComprehensionReport(
        value: StudentComprehensionAnalytics,
        studentId: string
    ): TeacherStudentComprehensionReport {
        return {
            metadata: this.toTeacherMetadata(
                'Öğrenci Anlama Raporu',
                'TeacherStudentComprehension',
                studentId,
                value.dateFrom,
                value.dateTo
            ),
            currentComprehension: value.currentComprehension,
            averageComprehension: value.averageComprehension,
            maxComprehension: value.maxComprehension,
            minComprehension: value.minComprehension,
            comprehensionImprovement: value.improvementRate,
            comprehensionOverTime: (value.trend ?? []).map(point => ({
                name: point.date,
                series: [{ name: 'Anlama', value: point.value }]
            })),
            weakAreas: value.weakAreas ?? [],
            strongAreas: value.strongAreas ?? []
        };
    }

    private toTeacherActivityReport(
        value: StudentActivityAnalytics,
        studentId: string
    ): TeacherStudentActivityReport {
        const activeDays = (value.heatmap ?? []).filter(point => point.value > 0).length;
        return {
            metadata: this.toTeacherMetadata(
                'Öğrenci Aktivite Raporu',
                'TeacherStudentActivity',
                studentId,
                value.dateFrom,
                value.dateTo
            ),
            dataAvailable: value.dataAvailable,
            unavailableReason: value.unavailableReason ?? undefined,
            totalActivities: value.studyTime.totalSessions,
            totalReadingMinutes: value.studyTime.totalMinutes,
            daysActive: activeDays,
            averageDailyMinutes: activeDays > 0 ? value.studyTime.totalMinutes / activeDays : 0,
            currentStreak: value.currentStreak.days,
            longestStreak: value.currentStreak.longestStreak,
            activityByType: (value.hourlyDistribution ?? []).map(point => ({
                name: point.label,
                series: [{ name: 'Aktivite', value: point.value }]
            })),
            dailyActivity: (value.heatmap ?? []).map(point => ({
                name: point.date,
                series: [{ name: 'Aktivite', value: point.value }]
            })),
            mostActiveDay: value.studyTime.mostActiveDay
        };
    }

    private toTeacherMetadata(
        reportTitle: string,
        reportType: string,
        studentId: string,
        dateFrom: string,
        dateTo: string
    ) {
        const startDate = new Date(dateFrom);
        const endDate = new Date(dateTo);
        return {
            reportId: `${reportType}-${Date.now()}`,
            reportTitle,
            reportType,
            generatedAt: new Date(),
            generatedBy: 'teacher',
            startDate,
            endDate,
            generatedFor: studentId,
            totalDays: Math.max(1, Math.ceil((endDate.getTime() - startDate.getTime()) / (24 * 60 * 60 * 1000)))
        };
    }

    private toTeacherContentAnalysisReport(value: TeacherContentAnalysisAnalytics): TeacherContentAnalysisReport {
        return {
            metadata: this.toTeacherMetadata(
                'Öğretmen İçerik Analizi',
                'Teacher',
                'teacher',
                value.dateFrom,
                value.dateTo),
            exerciseAnalysis: value.exerciseAnalysis ?? [],
            exerciseFrequencyChart: value.exerciseFrequencyChart ?? [],
            readingAnalysis: (value.readingAnalysis ?? []).map(item => ({
                difficultyLevel: item.difficultyLevel,
                totalReads: item.totalReads,
                averageWPM: item.averageWpm,
                averageComprehension: item.averageComprehension
            })),
            readingPerformanceChart: value.readingPerformanceChart ?? []
        };
    }

    private toTeacherTimeProgressReport(value: TeacherTimeProgressAnalytics): TeacherTimeBasedProgressReport {
        return {
            metadata: this.toTeacherMetadata(
                'Öğretmen Zaman Bazlı İlerleme',
                'Teacher',
                'teacher',
                value.dateFrom,
                value.dateTo),
            weeklyProgressChart: value.weeklyProgressChart ?? [],
            monthlyProgressChart: value.monthlyProgressChart ?? [],
            activityIntensityChart: value.activityIntensityChart ?? [],
            improvingStudents: (value.improvingStudents ?? []).map(item => ({
                studentId: item.studentId,
                studentName: item.studentName,
                previousScore: item.previousScore,
                currentScore: item.currentScore,
                improvement: item.improvement,
                trend: item.trend === 'declining' ? 'declining' : 'improving'
            })),
            decliningStudents: (value.decliningStudents ?? []).map(item => ({
                studentId: item.studentId,
                studentName: item.studentName,
                previousScore: item.previousScore,
                currentScore: item.currentScore,
                improvement: item.improvement,
                trend: 'declining'
            }))
        };
    }
}

interface StudentReadingSpeedAnalytics {
    dateFrom: string;
    dateTo: string;
    currentWpm: number;
    averageWpm: number;
    maxWpm: number;
    minWpm: number;
    improvementRate: number;
    trend: StudentAnalyticsTrendPoint[];
    categories: StudentAnalyticsCategoryPoint[];
    recommendations: string[];
}

interface StudentComprehensionAnalytics {
    dateFrom: string;
    dateTo: string;
    currentComprehension: number;
    averageComprehension: number;
    maxComprehension: number;
    minComprehension: number;
    improvementRate: number;
    trend: StudentAnalyticsTrendPoint[];
    weakAreas: string[];
    strongAreas: string[];
}

interface StudentActivityAnalytics {
    dateFrom: string;
    dateTo: string;
    dataAvailable: boolean;
    unavailableReason?: string | null;
    currentStreak: StudentActivityStreak;
    heatmap: StudentActivityHeatmapPoint[];
    hourlyDistribution: StudentActivityDistributionPoint[];
    studyTime: StudentActivityStudyTime;
}

interface StudentActivityStreak {
    days: number;
    longestStreak: number;
    lastActivityDate?: string | null;
    isActive: boolean;
}

interface StudentActivityHeatmapPoint {
    date: string;
    value: number;
    level: number;
}

interface StudentActivityDistributionPoint {
    label: string;
    value: number;
}

interface StudentActivityStudyTime {
    totalMinutes: number;
    averageSessionLength: number;
    totalSessions: number;
    mostActiveHour: number;
    mostActiveDay: string;
    consistency: number;
}

interface TeacherContentAnalysisAnalytics {
    dateFrom: string;
    dateTo: string;
    exerciseAnalysis: TeacherExerciseAnalysis[];
    exerciseFrequencyChart: ChartData[];
    readingAnalysis: TeacherReadingAnalysis[];
    readingPerformanceChart: ChartData[];
}

interface TeacherExerciseAnalysis {
    exerciseTypeName: string;
    totalCompletions: number;
    activeStudents: number;
    averageScore: number;
    performanceLevel: string;
}

interface TeacherReadingAnalysis {
    difficultyLevel: number;
    totalReads: number;
    averageWpm: number;
    averageComprehension: number;
}

interface TeacherTimeProgressAnalytics {
    dateFrom: string;
    dateTo: string;
    weeklyProgressChart: ChartData[];
    monthlyProgressChart: ChartData[];
    activityIntensityChart: ChartData[];
    improvingStudents: TeacherProgressStudentAnalytics[];
    decliningStudents: TeacherProgressStudentAnalytics[];
}

interface TeacherProgressStudentAnalytics {
    studentId: string;
    studentName: string;
    previousScore: number;
    currentScore: number;
    improvement: number;
    trend: string;
}

interface StudentAnalyticsTrendPoint {
    date: string;
    value: number;
}

interface StudentAnalyticsCategoryPoint {
    categoryName: string;
    value: number;
}
