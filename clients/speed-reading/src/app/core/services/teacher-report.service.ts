import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
    TeacherContentAnalysisReport,
    TeacherTimeBasedProgressReport,
    TeacherClassOverviewReport
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
    dailyGoalMinutes: number;
    daysGoalMet: number;
    goalAchievementRate: number;
}

@Injectable({
    providedIn: 'root'
})
export class TeacherReportService {
    private readonly http = inject(HttpClient);
    private readonly API_URL = `${environment.apiUrl}/v1/teachers`;

    getStudentReadingSpeedTrend(studentId: string, days: number = 30): Observable<TeacherStudentReadingSpeedReport> {
        return this.http.get<TeacherStudentReadingSpeedReport>(
            `${this.API_URL}/students/${studentId}/reading-speed-trend?days=${days}`
        );
    }

    getStudentComprehensionTrend(studentId: string, days: number = 30): Observable<TeacherStudentComprehensionReport> {
        return this.http.get<TeacherStudentComprehensionReport>(
            `${this.API_URL}/students/${studentId}/comprehension-trend?days=${days}`
        );
    }

    getStudentActivityReport(studentId: string, startDate?: Date, endDate?: Date): Observable<TeacherStudentActivityReport> {
        let url = `${this.API_URL}/students/${studentId}/activity-report`;
        const params: string[] = [];

        if (startDate) params.push(`startDate=${startDate.toISOString()}`);
        if (endDate) params.push(`endDate=${endDate.toISOString()}`);
        if (params.length > 0) url += `?${params.join('&')}`;

        return this.http.get<TeacherStudentActivityReport>(url);
    }

    getContentAnalysisReport(teacherId: string, startDate?: Date, endDate?: Date): Observable<TeacherContentAnalysisReport> {
        let url = `${this.API_URL}/${teacherId}/content-analysis`;
        const params: string[] = [];

        if (startDate) params.push(`startDate=${startDate.toISOString()}`);
        if (endDate) params.push(`endDate=${endDate.toISOString()}`);
        if (params.length > 0) url += `?${params.join('&')}`;

        return this.http.get<TeacherContentAnalysisReport>(url);
    }

    getTimeBasedProgressReport(teacherId: string, startDate?: Date, endDate?: Date): Observable<TeacherTimeBasedProgressReport> {
        let url = `${this.API_URL}/${teacherId}/progress-report`;
        const params: string[] = [];

        if (startDate) params.push(`startDate=${startDate.toISOString()}`);
        if (endDate) params.push(`endDate=${endDate.toISOString()}`);
        if (params.length > 0) url += `?${params.join('&')}`;

        return this.http.get<TeacherTimeBasedProgressReport>(url);
    }
}
