import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { SpeedReadingInstitutionStudentReportComponent } from './speed-reading-institution-student-report.component';

describe('SpeedReadingInstitutionStudentReportComponent', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [SpeedReadingInstitutionStudentReportComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('shows speed, comprehension, activity and recommendation details for a selected student', () => {
    const fixture = TestBed.createComponent(SpeedReadingInstitutionStudentReportComponent);
    fixture.componentRef.setInput('institutionId', 'institution-1');
    fixture.componentRef.setInput('student', {
      userId: 'student-1',
      firstName: 'Ayşe',
      lastName: 'Yılmaz',
      role: 'Student',
      isMembershipActive: true,
      isActive: true,
      createdAt: '2026-09-04T00:00:00Z',
      gradeLevel: 8,
      teacherName: 'Mehmet Kaya',
      studentCount: 0,
    });
    fixture.detectChanges();
    flushStudentReport(http);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Ayşe Yılmaz');
    expect(fixture.nativeElement.textContent).toContain('Mehmet Kaya');
    expect(fixture.nativeElement.textContent).toContain('238');
    expect(fixture.nativeElement.textContent).toContain('82%');
    expect(fixture.nativeElement.textContent).toContain('Başarıyla sürdürüyor');
    expect(fixture.nativeElement.textContent).toContain('Özet çıkarma çalışmaları önerilir.');
    expect(fixture.nativeElement.textContent).toContain('Akıcı okuma');
  });

  it('reloads analytics when a different student is selected in the same report view', () => {
    const fixture = TestBed.createComponent(SpeedReadingInstitutionStudentReportComponent);
    fixture.componentRef.setInput('institutionId', 'institution-1');
    fixture.componentRef.setInput('student', {
      userId: 'student-1',
      firstName: 'Ayşe',
      lastName: 'Yılmaz',
      role: 'Student',
      isMembershipActive: true,
      isActive: true,
      createdAt: '2026-09-04T00:00:00Z',
      studentCount: 0,
    });
    fixture.detectChanges();
    flushStudentReport(http);
    fixture.detectChanges();

    fixture.componentRef.setInput('student', {
      userId: 'student-2',
      firstName: 'Selim',
      lastName: 'Kaya',
      role: 'Student',
      isMembershipActive: true,
      isActive: true,
      createdAt: '2026-09-04T00:00:00Z',
      studentCount: 0,
    });
    fixture.detectChanges();
    flushStudentReport(http, 'student-2');
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Selim Kaya');
  });

  it('shows a retry action when a scoped student analytics request fails', () => {
    const fixture = TestBed.createComponent(SpeedReadingInstitutionStudentReportComponent);
    fixture.componentRef.setInput('institutionId', 'institution-1');
    fixture.componentRef.setInput('student', {
      userId: 'student-1',
      firstName: 'Ayşe',
      lastName: 'Yılmaz',
      role: 'Student',
      isMembershipActive: true,
      isActive: true,
      createdAt: '2026-09-04T00:00:00Z',
      studentCount: 0,
    });
    fixture.detectChanges();
    const requests = expectStudentReportRequests(http);
    requests.slice(1).forEach((request) => request.flush({}));
    requests[0].flush({}, { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Öğrenci raporu yüklenemedi');
    fixture.nativeElement.querySelector('[data-testid="retry-student-report"]').click();
    flushStudentReport(http);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Ayşe Yılmaz');
  });
});

function expectStudentReportRequests(http: HttpTestingController, studentId = 'student-1') {
  return ['summary', 'reading-speed', 'comprehension', 'activity'].map((path) =>
    http.expectOne(
      (request) =>
        request.url ===
        `/api/speed-reading/analytics/institutions/institution-1/students/${studentId}/${path}`,
    ),
  );
}

function flushStudentReport(http: HttpTestingController, studentId = 'student-1'): void {
  expectStudentReportRequests(http, studentId).forEach((request, index) => {
    request.flush(
      [
        {
          readingSessions: 6,
          averageWpm: 238,
          averageComprehension: 82,
          totalReadingMinutes: 45,
          exercisesCompleted: 10,
          exercisesPassed: 8,
          averageSuccessRate: 80,
          latestWpm: 250,
          latestComprehension: 84,
          currentLevel: 4,
          currentStreak: 3,
          longestStreak: 8,
          totalXp: 120,
          milestonesEarned: 2,
          dailyGoalMinutes: 20,
          goalCompletionRate: 75,
          recentMilestones: [],
          daily: [],
        },
        {
          currentWpm: 250,
          averageWpm: 238,
          medianWpm: 235,
          minWpm: 180,
          maxWpm: 300,
          improvementRate: 12,
          trend: [],
          categories: [],
          benchmark: {
            studentValue: 238,
            institutionAverage: 220,
            platformAverage: 210,
            performanceLevel: 'Başarıyla sürdürüyor',
          },
          sessionsBelow200Wpm: 1,
          sessions200To400Wpm: 5,
          sessionsAbove400Wpm: 0,
          recommendations: ['Özet çıkarma çalışmaları önerilir.'],
        },
        {
          currentComprehension: 84,
          averageComprehension: 82,
          maxComprehension: 90,
          minComprehension: 70,
          improvementRate: 8,
          trend: [],
          categories: [],
          questionTypes: [],
          bloomLevels: [],
          totalQuestionsAttempted: 20,
          correctAnswers: 16,
          successRate: 80,
          benchmark: {
            studentValue: 82,
            institutionAverage: 76,
            platformAverage: 74,
            performanceLevel: 'İyi',
          },
          weakAreas: ['Özet çıkarma'],
          strongAreas: ['Ana fikir'],
        },
        {
          dataAvailable: true,
          unavailableReason: null,
          currentStreak: {
            days: 3,
            longestStreak: 8,
            lastActivityDate: '2026-09-29T00:00:00Z',
            isActive: true,
          },
          heatmap: [],
          hourlyDistribution: [],
          dailyDistribution: [],
          recentActivities: [
            {
              completedAt: '2026-09-29T10:00:00Z',
              activityType: 'Reading',
              contentId: 'content-1',
              contentTitle: 'Akıcı okuma',
              exerciseTypeName: null,
              difficultyLevel: 2,
              durationSeconds: 300,
              wpm: 250,
              comprehension: 84,
              successRate: 80,
              isMeasured: true,
              isPassed: true,
            },
          ],
          studyTime: {
            totalMinutes: 45,
            averageSessionLength: 15,
            totalSessions: 3,
            mostActiveHour: 10,
            mostActiveDay: 'Pazartesi',
            consistency: 80,
          },
        },
      ][index],
    );
  });
}
