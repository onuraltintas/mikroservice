import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { Subject, of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { CoachingTeacherTrackingComponent } from './coaching-teacher-tracking.component';
import { CoachingTeacherTrackingContext, CoachingTeacherTrackingService, TrackingPage } from './coaching-teacher-tracking.service';
import { ToasterService } from '../../../../../../src/app/core/services/toaster.service';
import { CoachingTeacherWorkspaceComponent } from './coaching-teacher-workspace.component';
import { By } from '@angular/platform-browser';

const sample: TrackingPage = { items: [], totalCount: 30, pageNumber: 2, pageSize: 25,
  overdueStudyCount: 20, overdueAssignmentCount: 8, awaitingReviewCount: 2, awaitingReflectionCount: 0, overdueDecisionCount: 0 };

describe('Teacher tracking context', () => {
  async function setup() {
    const api = { get: vi.fn(() => of(sample)) };
    TestBed.configureTestingModule({ imports: [CoachingTeacherTrackingComponent], providers: [provideHttpClient(),
      CoachingTeacherTrackingContext, { provide: CoachingTeacherTrackingService, useValue: api },
      { provide: ToasterService, useValue: { error: vi.fn(), success: vi.fn() } }] });
    await TestBed.compileComponents(); return { api, create() { const f = TestBed.createComponent(CoachingTeacherTrackingComponent); f.detectChanges(); return f; } };
  }
  it('restores applied filters, unsubmitted search and page after leaving the section and refreshes counts', async () => {
    const { api, create } = await setup(); const first = create(); const c = first.componentInstance;
    c.filters = { fromDate: '2026-10-01', toDate: '2026-10-21', mode: 'review', search: 'Ayşe' }; c.applyFilters();
    c.load(2); c.filters.search = 'henüz uygulanmadı'; first.destroy();
    api.get.mockReturnValueOnce(of({ ...sample, awaitingReviewCount: 1, totalCount: 29 }));
    const second = create();
    expect(api.get).toHaveBeenLastCalledWith(expect.objectContaining({ mode: 'review', search: 'Ayşe' }), 2);
    expect(second.componentInstance.filters.search).toBe('henüz uygulanmadı');
    expect(second.componentInstance.data()?.awaitingReviewCount).toBe(1);
    expect(second.nativeElement.textContent).toContain('henüz uygulanmadı');
  });
  it('keeps the requested page after a failed refresh and cancels late results on destruction', async () => {
    const { api, create } = await setup(); const f = create();
    api.get.mockReturnValueOnce(throwError(() => ({ status: 503 }))); f.componentInstance.refresh();
    f.componentInstance.refresh(); expect(api.get).toHaveBeenLastCalledWith(expect.anything(), 2);
    const late = new Subject<TrackingPage>(); api.get.mockReturnValueOnce(late); f.componentInstance.refresh(); f.destroy();
    late.next({ ...sample, pageNumber: 9 }); late.complete();
    create(); expect(api.get).toHaveBeenLastCalledWith(expect.anything(), 2);
  });
  it('returns from detail with fresh totals and uses the server-clamped last page', async () => {
    const { api, create } = await setup(); const f = create();
    await f.componentInstance.open({ kind: 'review', recordId: 'a', studentId: 's', studentName: 'Ayşe', title: 'Ödev', planId: null, date: '2026-10-21', startMinute: null, minutes: null, timeZoneId: 'Europe/Istanbul' });
    api.get.mockReturnValueOnce(of({ ...sample, pageNumber: 1, totalCount: 25, awaitingReviewCount: 0 }));
    await f.componentInstance.back(); expect(api.get).toHaveBeenLastCalledWith(expect.anything(), 2);
    expect(f.componentInstance.data()?.awaitingReviewCount).toBe(0);
    f.componentInstance.refresh(); expect(api.get).toHaveBeenLastCalledWith(expect.anything(), 1);
  });
  it('keeps context across actual workspace section destruction but clears it in a new workspace', async () => {
    const api = { get: vi.fn(() => of({ ...sample, pageNumber: 1 })) };
    TestBed.configureTestingModule({ imports: [CoachingTeacherWorkspaceComponent], providers: [provideHttpClient(),
      { provide: CoachingTeacherTrackingService, useValue: api },
      { provide: ToasterService, useValue: { error: vi.fn(), success: vi.fn() } }] });
    TestBed.overrideComponent(CoachingTeacherWorkspaceComponent, { set: {
      template: `@if(activeSection()==='tracking'){<staff-coaching-teacher-tracking #trackingPanel />}`
    } });
    await TestBed.compileComponents(); const f = TestBed.createComponent(CoachingTeacherWorkspaceComponent);
    await f.componentInstance.selectSection('tracking'); f.detectChanges();
    let c = f.debugElement.query(By.directive(CoachingTeacherTrackingComponent)).componentInstance as CoachingTeacherTrackingComponent;
    c.filters.mode = 'assignment'; c.filters.search = 'Biyoloji'; c.applyFilters();
    await f.componentInstance.selectSection('students'); f.detectChanges();
    await f.componentInstance.selectSection('tracking'); f.detectChanges();
    c = f.debugElement.query(By.directive(CoachingTeacherTrackingComponent)).componentInstance;
    expect(c.filters.mode).toBe('assignment'); expect(c.filters.search).toBe('Biyoloji');
    expect(api.get).toHaveBeenLastCalledWith(expect.objectContaining({ mode: 'assignment', search: 'Biyoloji' }), 1);
    f.destroy(); const other = TestBed.createComponent(CoachingTeacherWorkspaceComponent);
    await other.componentInstance.selectSection('tracking'); other.detectChanges();
    expect(api.get).toHaveBeenLastCalledWith(expect.objectContaining({ mode: 'all', search: '' }), 1);
  });
});
