import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ReportsService } from './reports.service';

describe('Turkish student report labels', () => {
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
