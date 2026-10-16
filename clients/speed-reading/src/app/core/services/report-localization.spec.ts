import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ReportsService } from './reports.service';
import { StudentActivityReportComponent } from '../../features/student/reports/student-activity-report.component';
import { StudentSeriesReportComponent } from '../../features/student/reports/student-series-report.component';
import { StudentDashboardReportComponent } from '../../features/student/reports/student-dashboard-report.component';
import { speedReadingReportLabel } from '../../../../../shared/utils/speed-reading-report-labels';

describe('Turkish student report labels', () => {
  it('translates known display codes, preserving Turkish/custom labels and identifiers', () => {
    for (const [input, expected] of [
      ['Above Average', 'Ortalamanın üzerinde'], ['Average', 'Ortalama'], ['Below Average', 'Ortalamanın altında'],
      ['No benchmark', 'Karşılaştırma verisi yok'],
      ['grouping', 'Gruplayarak okuma'], ['guided_reading', 'Rehberli okuma'],
      ['rsvp', 'Seri okuma'], ['subvocalization', 'İç ses azaltma'],
      ['comprehension', 'Anlama'], ['error-analysis', 'Hata analizi'], ['free-reading', 'Serbest okuma'],
      ['regression', 'Geriye dönüşü azaltma'], ['scanning', 'Tarayarak okuma'], ['skimming', 'Göz gezdirerek okuma'],
      ['Strong', 'Güçlü'], ['Needs Improvement', 'Geliştirilmeli'], ['high', 'Yüksek'], ['medium', 'Orta'], ['low', 'Düşük'],
      ['not-measured', 'Ölçülmedi'], ['exercise_success', 'Egzersiz başarısı'], ['comprehension', 'Anlama'],
      [' Regression_Reduction ', 'Geriye dönüşü azaltma'], ['vocabulary_builder', 'Kelime hazinesi'],
      ['Monday', 'Pazartesi'], ['Sunday', 'Pazar'], ['science', 'Bilim'],
      ['Özel kategori', 'Özel kategori'], ['Veri yok', 'Veri yok'], ['constructor', 'constructor'], ['toString', 'toString']
    ]) expect(speedReadingReportLabel(input)).withContext(input).toBe(expected);
    expect(speedReadingReportLabel(undefined)).toBe('—');
    expect(speedReadingReportLabel(null)).toBe('—');
    expect(speedReadingReportLabel('')).toBe('—');
  });
  it('formats report durations and month names in Turkish', () => {
    expect(StudentActivityReportComponent.prototype.formatMinutes(75)).toBe('1 sa 15 dk');
    expect(StudentActivityReportComponent.prototype.formatMinutes(5)).toBe('5 dk');
    expect(StudentSeriesReportComponent.prototype.formatDate('2026-10-16T12:00:00Z')).toBe('16 Eki');
    expect(StudentDashboardReportComponent.prototype.formatDate('2026-10-16T12:00:00Z')).toBe('16 Eki 2026');
  });
  it('translates categories, chart labels and improvement areas while retaining scores and category identifiers', () => {
    TestBed.configureTestingModule({ providers: [ReportsService, provideHttpClient(), provideHttpClientTesting()] });
    const http = TestBed.inject(HttpTestingController);
    let report: any;
    TestBed.inject(ReportsService).getStudentComprehensionReport('self', new Date('2026-10-01'), new Date('2026-10-16')).subscribe(value => report = value);
    http.expectOne(request => request.url.endsWith('/student/comprehension')).flush({
      dateFrom: '2026-10-01', dateTo: '2026-10-16', averageComprehension: 0, totalQuestionsAttempted: 1,
      benchmark: { studentValue: 0, institutionAverage: 0, platformAverage: 0 },
      categories: [{ categoryName: 'regression', value: 0, questionsAttempted: 1, correctAnswers: 0 }],
      questionTypes: [{ type: 'Inference', value: 0 }], bloomLevels: [{ label: 'Understanding', value: 0 }], weakAreas: ['regression'], strongAreas: []
    });
    expect(report.categoryBreakdown[0]).toEqual(jasmine.objectContaining({ categoryId: 'regression', categoryName: 'Geriye dönüşü azaltma', comprehensionRate: 0, questionsAnswered: 1 }));
    expect(report.categoryComprehensionChart.data[0].name).toBe('Geriye dönüşü azaltma');
    expect(report.improvementAreas[0].area).toBe('Geriye dönüşü azaltma');
    expect(report.questionTypeChart.data[0].name).toBe('Çıkarım');
    expect(report.bloomLevelChart.data[0].name).toBe('Anlama');
    expect(report.dataAvailable).toBeTrue();
    http.verify();
  });
});
