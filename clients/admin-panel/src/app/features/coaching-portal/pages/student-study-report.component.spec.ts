import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { StudentStudyReportComponent } from './student-study-report.component';

describe('StudentStudyReportComponent', () => {
  it('automatically loads a rolling local week and its previous week for session preparation', () => {
    vi.useFakeTimers(); vi.setSystemTime(new Date(2026, 9, 13, 0, 30));
    try {
      const { component, fixture, http } = setup(); fixture.componentRef.setInput('staffStudentId', 'student-one'); fixture.componentRef.setInput('autoWeekly', true); fixture.detectChanges();
      const current = http.expectOne(r => r.url.endsWith('/staff/students/student-one/study/report') && r.params.get('fromDate') === '2026-10-07' && r.params.get('toDate') === '2026-10-13');
      const previous = http.expectOne(r => r.params.get('fromDate') === '2026-09-30' && r.params.get('toDate') === '2026-10-06');
      component.useLastWeek(); http.expectNone(() => true);
      current.flush({ success: true, data: { ...weeklyData, fromDate: '2026-10-07', toDate: '2026-10-13' } });
      previous.flush({ success: true, data: { ...weeklyData, fromDate: '2026-09-30', toDate: '2026-10-06' } });
      expect(component.compareWeekly).toBe(true); expect(component.previousReport()?.toDate).toBe('2026-10-06');
    } finally { vi.useRealTimers(); }
  });
  const weeklyData = { fromDate: '2026-10-19', toDate: '2026-10-25', source: 'StudentReported', reason: 'CurrentStatusOfScheduledWork',
    scheduledTasks: 2, completedTasks: 1, completionPercentage: 50, plannedMinutes: 90, actualMinutes: 40, topics: [] };
  it('compares adjacent seven-day periods without treating missing actual minutes as zero', () => {
    const { component, fixture, http } = setup(); component.fromDate = '2026-10-19'; component.toDate = '2026-10-25'; component.compareWeekly = true; component.load();
    const current = http.expectOne(r => r.params.get('fromDate') === '2026-10-19');
    const previous = http.expectOne(r => r.params.get('fromDate') === '2026-10-12' && r.params.get('toDate') === '2026-10-18');
    current.flush({ success: true, data: weeklyData }); expect(component.report()).toBeNull();
    previous.flush({ success: true, data: { ...weeklyData, fromDate: '2026-10-12', toDate: '2026-10-18', completedTasks: 0, completionPercentage: 0, actualMinutes: null } });
    expect(component.previousReport()?.actualMinutes).toBeNull(); fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Önceki hafta'); expect(fixture.nativeElement.textContent).toContain('Kayıt yok');
    expect(fixture.nativeElement.textContent).toContain('12.10.2026 – 18.10.2026');
  });
  it('rejects comparisons that are not seven days or have no valid previous week', () => {
    const { component, http } = setup(); component.compareWeekly = true; component.fromDate = '2026-10-01'; component.toDate = '2026-10-14'; component.load();
    http.expectNone(() => true); expect(component.error()).toBeTruthy();
    component.fromDate = '0001-01-08'; component.toDate = '0001-01-14'; component.load();
    http.expectNone(() => true); expect(component.error()).toBeTruthy(); expect(component.busy()).toBe(false);
  });
  it('cancels both weekly report requests when the selected staff student changes', () => {
    const { component, fixture, http } = setup(); fixture.componentRef.setInput('staffStudentId', 'student-one'); fixture.detectChanges();
    component.compareWeekly = true; component.fromDate = '2026-10-19'; component.toDate = '2026-10-25'; component.load();
    const requests = http.match(r => r.url.endsWith('/staff/students/student-one/study/report')); expect(requests.length).toBe(2);
    fixture.componentRef.setInput('staffStudentId', 'student-two'); fixture.detectChanges();
    expect(requests.every(request => request.cancelled)).toBe(true); expect(component.previousReport()).toBeNull(); expect(component.report()).toBeNull();
  });
  it('clears both periods if either weekly request fails', () => {
    const { component, http } = setup(); component.compareWeekly = true; component.fromDate = '2026-10-19'; component.toDate = '2026-10-25'; component.load();
    http.expectOne(r => r.params.get('fromDate') === '2026-10-19').flush({ success: true, data: weeklyData });
    http.expectOne(r => r.params.get('fromDate') === '2026-10-12').flush({}, { status: 403, statusText: 'Forbidden' });
    expect(component.report()).toBeNull(); expect(component.previousReport()).toBeNull(); expect(component.busy()).toBe(false); expect(component.error()).toBeTruthy();
  });
  it('orders repeat priorities by wrong or empty question share and preserves their source', () => {
    const { component, fixture } = setup(); component.report.set({ ...weeklyData, lessonResults: [
      { source: 'StudentReported', examType: 'LGS', lessonName: 'Matematik', topicName: 'Kesirler', questionCount: 10, correct: 3, wrong: 4, empty: 3 },
      { source: 'TeacherRecorded', examType: 'LGS', lessonName: 'Matematik', topicName: 'Geometri', questionCount: 20, correct: 14, wrong: 6, empty: 0 },
      { source: 'StudentReported', examType: 'LGS', questionCount: 0, correct: 0, wrong: 0, empty: 0 },
      { source: 'StudentReported', examType: 'LGS', questionCount: 10, correct: 10, wrong: 0, empty: 0 }
    ] });
    expect(component.priorities().map(topic => topic.topicName)).toEqual(['Kesirler', 'Geometri']); fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Tekrar öncelikleri'); expect(fixture.nativeElement.textContent).toContain('7 yanlış/boş / 10 soru');
    expect(fixture.nativeElement.textContent).toContain('Öğretmen kaydı');
  });
  it('uses the protected admin report endpoint and cancels it when student selection changes', () => {
    const { fixture, component, http } = setup();
    fixture.componentRef.setInput('adminStudentId', 'student-one'); fixture.detectChanges(); component.load();
    const first = http.expectOne(r => r.url.endsWith('/coaching-admin/students/student-one/study/report'));
    fixture.componentRef.setInput('adminStudentId', 'student-two'); fixture.detectChanges();
    expect(first.cancelled).toBe(true); expect(component.report()).toBeNull();
  });
  it('shows calculated attainment separately from manual progress and explains missing evidence', () => {
    const { component, fixture, http } = setup(); component.load();
    http.expectOne(r => r.url.endsWith('/reports')).flush({ success: true, data: {
      fromDate: component.fromDate, toDate: component.toDate, reason: 'NoScheduledTasks', source: 'StudentReported',
      scheduledTasks: 0, completedTasks: 0, completionPercentage: null, plannedMinutes: 0, actualMinutes: null, topics: [],
      goals: [{ goalId: 'goal', title: 'Puan hedefim', source: 'Unspecified', recordedProgress: 30, isCompleted: false,
        targetScore: 400, targetMaxScore: 500, targetExamType: 'LGS', scoreAssessment: {
          reason: 'LatestMatchingResultPerSource', comparisons: [{ resultId: 'result', examId: 'exam',
            source: 'StudentReported', examDate: '2026-10-02T00:00:00Z', score: 320,
            targetAttainmentPercentage: 80, remainingScore: 80, targetReached: false }] } },
        { goalId: 'empty', title: 'Kanıt yok', source: 'Unspecified', recordedProgress: 0, isCompleted: false,
          scoreAssessment: { reason: 'NoMatchingResults', comparisons: [] } }]
    } });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Puan hedefine erişim: %80');
    expect(fixture.nativeElement.textContent).toContain('Kalan puan: 80');
    expect(fixture.nativeElement.textContent).toContain('Kaydedilen ilerleme: %30');
    expect(fixture.nativeElement.textContent).toContain('uyumlu sınav sonucu yok');
  });
  it('renders recorded goal progress separately from exam achievement and historical dates', () => {
    const { fixture, component, http } = setup(); component.load();
    http.expectOne(r => r.url.endsWith('/reports')).flush({ success: true, data: {
      fromDate: component.fromDate, toDate: component.toDate, reason: 'NoScheduledTasks', source: 'StudentReported',
      scheduledTasks: 0, completedTasks: 0, completionPercentage: null, plannedMinutes: 0, actualMinutes: null, topics: [],
      goals: [{ goalId: 'own', title: 'LGS hedefim', source: 'Unspecified', recordedProgress: 30, isCompleted: false,
        targetDate: '2027-06-01T00:00:00Z', targetScore: 400, targetExamType: 'LGS', targetSubject: null },
        { goalId: 'teacher', title: 'Düzenli çalışma', source: 'TeacherSet', recordedProgress: 100, isCompleted: true }]
    } });
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent;
    expect(text).toContain('Güncel hedeflerim');
    expect(text).toContain('Kaydedilen ilerleme: %30');
    expect(text).toContain('Öğretmenin belirlediği hedef');
    expect(text).toContain('Belirleyen kişi kaydedilmemiş');
    expect(text).toContain('Hedef puanı: 400');
    expect(text).toContain('Otomatik başarı veya yerleşme tahmini değildir');
    expect(text).toContain('Seçilen dönemin geçmiş durumunu göstermez');
  });
  afterEach(() => TestBed.inject(HttpTestingController).verify());
  function setup() {
    TestBed.configureTestingModule({ imports: [StudentStudyReportComponent], providers: [provideHttpClient(), provideHttpClientTesting()] });
    const fixture = TestBed.createComponent(StudentStudyReportComponent);
    return { fixture, component: fixture.componentInstance, http: TestBed.inject(HttpTestingController) };
  }
  it('defaults to the local calendar day rather than the previous UTC day', () => {
    vi.useFakeTimers(); vi.setSystemTime(new Date(2026, 9, 3, 0, 30));
    const iso = vi.spyOn(Date.prototype, 'toISOString').mockReturnValue('2026-10-02T21:30:00.000Z');
    try {
      const { component } = setup();
      expect(component.toDate).toBe('2026-10-03');
      expect(component.fromDate).toBe('2026-09-04');
    } finally { iso.mockRestore(); vi.useRealTimers(); }
  });
  it('passes the selected period and renders no data distinctly from zero', () => {
    const { fixture, component, http } = setup();
    component.fromDate = '2026-10-01'; component.toDate = '2026-10-02'; component.load();
    const request = http.expectOne(r => r.url.endsWith('/coaching/study-planning/reports'));
    expect(request.request.params.get('fromDate')).toBe('2026-10-01');
    expect(request.request.params.get('toDate')).toBe('2026-10-02');
    request.flush({ success: true, data: { fromDate: '2026-10-01', toDate: '2026-10-02', source: 'StudentReported', reason: 'NoScheduledTasks',
      scheduledTasks: 0, completedTasks: 0, completionPercentage: null, plannedMinutes: 0, actualMinutes: null, topics: [] } });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Bu döneme planlanmış çalışma yok');
    expect(fixture.nativeElement.textContent).toContain('Öğrenci beyanı');
    expect(fixture.nativeElement.textContent).not.toContain('%0');
  });
  it('rejects reversed dates and clears stale data on failure', () => {
    const { component, http } = setup();
    component.fromDate = '2026-10-02'; component.toDate = '2026-10-01'; component.load();
    http.expectNone(r => r.url.endsWith('/reports'));
    expect(component.error()).toBeTruthy();
    component.toDate = '2026-10-03'; component.load(); component.load();
    http.expectOne(r => r.url.endsWith('/reports')).flush({}, { status: 500, statusText: 'Error' });
    expect(component.report()).toBeNull(); expect(component.busy()).toBe(false);
    expect(component.error()).toBeTruthy();
  });
  it('labels exam sources separately and shows topic study and answer statistics', () => {
    const { fixture, component, http } = setup(); component.load();
    http.expectOne(r => r.url.endsWith('/reports')).flush({ success: true, data: {
      fromDate: component.fromDate, toDate: component.toDate, reason: 'CurrentStatusOfScheduledWork', source: 'StudentReported',
      scheduledTasks: 1, completedTasks: 0, completionPercentage: 0, plannedMinutes: 30, actualMinutes: null,
      topics: [{ topicId: 'topic', topicName: 'Kesirler', scheduledTasks: 1, completedTasks: 0, plannedMinutes: 30, actualMinutes: null }],
      examGroups: [{ source: 'StudentReported', examType: 'Mock', maxScore: 100, count: 1, averagePercentage: 80 },
        { source: 'TeacherRecorded', examType: 'Mock', maxScore: 100, count: 1, averagePercentage: 70 }],
      lessonResults: [{ source: 'StudentReported', examType: 'Mock', lessonName: 'Matematik', topicName: 'Kesirler', questionCount: 10, correct: 8, wrong: 1, empty: 1 }]
    } });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Öğretmen kaydı');
    expect(fixture.nativeElement.textContent).toContain('Kesirler');
    expect(fixture.nativeElement.textContent).toContain('Matematik');
    expect(fixture.nativeElement.textContent).toContain('8 / 1 / 1');
  });
});
