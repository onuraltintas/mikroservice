import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { StudentStudyReportComponent } from './student-study-report.component';

describe('StudentStudyReportComponent', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());
  function setup() {
    TestBed.configureTestingModule({ imports: [StudentStudyReportComponent], providers: [provideHttpClient(), provideHttpClientTesting()] });
    const fixture = TestBed.createComponent(StudentStudyReportComponent);
    return { fixture, component: fixture.componentInstance, http: TestBed.inject(HttpTestingController) };
  }
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
});
