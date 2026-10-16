import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { SpeedReadingStudentReportComponent } from './speed-reading-institution-student-report.component';

describe('SpeedReadingStudentReportComponent', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [SpeedReadingStudentReportComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it.each([false, true])('localizes benchmark and category labels in the scoped report (teacher=%s)', teacherScoped => {
    const fixture = TestBed.createComponent(SpeedReadingStudentReportComponent);
    fixture.componentRef.setInput('institutionId', 'institution-1');
    fixture.componentRef.setInput('teacherScoped', teacherScoped);
    fixture.componentRef.setInput('student', { userId: 'student-1', firstName: 'Ayşe', lastName: 'Yılmaz' });
    fixture.detectChanges();
    flushStudentReport(http, 'student-1', teacherScoped);
    const report = fixture.componentInstance.report()!;
    fixture.componentInstance.report.set({ ...report,
      readingSpeed: { ...report.readingSpeed, benchmark: { ...report.readingSpeed.benchmark, performanceLevel: 'Below Average' } },
      comprehension: { ...report.comprehension, benchmark: { ...report.comprehension.benchmark, performanceLevel: 'Above Average' },
        strongAreas: ['regression'], weakAreas: ['science', 'Özel kategori'] }
    });
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent;
    for (const label of ['Ortalamanın altında', 'Ortalamanın üzerinde', 'Geriye dönüşü azaltma', 'Bilim', 'Özel kategori']) expect(text).toContain(label);
    for (const label of ['Above Average', 'Below Average', 'regression', 'science']) expect(text).not.toContain(label);
    expect(fixture.componentInstance.report()!.comprehension.strongAreas).toEqual(['regression']);
  });

  it.each([false, true])('shows no speed data for unmeasured reading sessions in teacher scope %s', teacherScoped => {
    const fixture = TestBed.createComponent(SpeedReadingStudentReportComponent);
    fixture.componentRef.setInput('institutionId', 'institution-1');
    fixture.componentRef.setInput('teacherScoped', teacherScoped);
    fixture.componentRef.setInput('student', { userId: 'student-1', firstName: 'Ayşe', lastName: 'Yılmaz' });
    fixture.detectChanges();
    flushStudentReport(http, 'student-1', teacherScoped);
    const report = fixture.componentInstance.report()!;
    fixture.componentInstance.report.set({ ...report,
      summary: { ...report.summary, readingSessions: 8, averageWpm: 0 },
      readingSpeed: { ...report.readingSpeed, averageWpm: 0, recommendations: ['Ölçümsüz öneri'],
        benchmark: { studentValue: 0, institutionAverage: 300, platformAverage: 300, performanceLevel: 'Below Average' } },
    });
    fixture.detectChanges();
    const speedCard = fixture.nativeElement.querySelector('[aria-labelledby="reading-speed-report-title"]') as HTMLElement;
    expect(speedCard.textContent).toContain('Veri yok');
    expect(speedCard.textContent).not.toContain('Below Average');
    expect(speedCard.textContent).not.toContain('WPM');
    expect(speedCard.textContent).not.toContain('Ölçümsüz öneri');
    expect(fixture.nativeElement.querySelector('.summary-grid .metric-card strong').textContent).toContain('Veri yok');
  });

  it('shows speed, comprehension, activity and recommendation details for a selected student', () => {
    const fixture = TestBed.createComponent(SpeedReadingStudentReportComponent);
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
    const report = fixture.componentInstance.report()!;
    const activity = report.activity!;
    const original = activity.recentActivities[0];
    fixture.componentInstance.report.set({ ...report, activity: { ...activity, recentActivities: [
      { ...original, contentTitle: 'Bağımsız kelime', wpm: null, comprehension: null, successRate: 100 },
      { ...original, contentTitle: 'Ölçülen sıfır', successRate: 0, isPassed: false },
      { ...original, contentTitle: 'Ölçülmeyen göz', wpm: null, comprehension: null, successRate: null, isMeasured: false, isPassed: false },
    ] } });
    fixture.detectChanges();
    const rows = Array.from(fixture.nativeElement.querySelectorAll('tr')) as HTMLTableRowElement[];
    expect(rows.find(row => row.textContent?.includes('Bağımsız kelime'))?.textContent).toContain('100%');
    expect(rows.find(row => row.textContent?.includes('Ölçülen sıfır'))?.cells[4].textContent?.trim()).toBe('0%');
    const unmeasured = rows.find(row => row.textContent?.includes('Ölçülmeyen göz'))?.textContent;
    expect(unmeasured).toContain('Ölçülmedi');
    expect(unmeasured).not.toContain('0%');
  });

  it('reloads analytics when a different student is selected in the same report view', () => {
    const fixture = TestBed.createComponent(SpeedReadingStudentReportComponent);
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
    const fixture = TestBed.createComponent(SpeedReadingStudentReportComponent);
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
    expect(fixture.nativeElement.textContent).not.toContain('bağlantısını kontrol');
    expect(fixture.nativeElement.textContent).not.toContain('Kurum kapsamını kontrol');
    expect(fixture.nativeElement.textContent).toContain('sunucu');
    fixture.nativeElement.querySelector('[data-testid="retry-student-report"]').click();
    flushStudentReport(http);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Ayşe Yılmaz');
  });

  it('uses teacher-scoped report endpoints when embedded in the teacher workspace', () => {
    const fixture = TestBed.createComponent(SpeedReadingStudentReportComponent);
    fixture.componentRef.setInput('teacherScoped', true);
    fixture.componentRef.setInput('student', {
      userId: 'student-1',
      firstName: 'Ayşe',
      lastName: 'Yılmaz',
      gradeLevel: 8,
    });
    fixture.detectChanges();

    const requests = ['summary', 'reading-speed', 'comprehension', 'activity'].map((path) =>
      http.expectOne(
        (request) =>
          request.url === `/api/speed-reading/analytics/teacher/students/student-1/${path}`,
      ),
    );
    requests[0].flush({
      readingSessions: 2,
      averageWpm: 245,
      totalReadingMinutes: 10,
      exercisesCompleted: 2,
      averageSuccessRate: 85,
      currentLevel: 4,
    });
    requests[1].flush({
      averageWpm: 245,
      improvementRate: 5,
      benchmark: { institutionAverage: 220, performanceLevel: 'İyi' },
      recommendations: [],
    });
    requests[2].flush({
      averageComprehension: 80,
      improvementRate: 3,
      totalQuestionsAttempted: 5,
      benchmark: { institutionAverage: 75, performanceLevel: 'İyi' },
      weakAreas: [],
      strongAreas: [],
    });
    requests[3].flush({
      dataAvailable: false,
      unavailableReason: 'Etkinlik yok',
      recentActivities: [],
      currentStreak: {},
      studyTime: {},
    });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Ayşe Yılmaz');
    expect(fixture.nativeElement.textContent).toContain('Karşılaştırma ortalaması');
    expect(fixture.nativeElement.textContent).not.toContain('Kurum ortalaması');
  });
});

function expectStudentReportRequests(http: HttpTestingController, studentId = 'student-1', teacherScoped = false) {
  return ['summary', 'reading-speed', 'comprehension', 'activity'].map((path) =>
    http.expectOne(
      (request) =>
        request.url ===
        `/api/speed-reading/analytics/${teacherScoped ? 'teacher' : 'institutions/institution-1'}/students/${studentId}/${path}`,
    ),
  );
}

function flushStudentReport(http: HttpTestingController, studentId = 'student-1', teacherScoped = false): void {
  expectStudentReportRequests(http, studentId, teacherScoped).forEach((request, index) => {
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
