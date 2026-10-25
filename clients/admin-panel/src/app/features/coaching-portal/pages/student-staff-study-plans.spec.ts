import { ToasterService } from '../../../core/services/toaster.service';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { StudentStudyPlansComponent } from './student-study-plans.component';

describe('shared staff plan editor', () => {
  beforeEach(() => TestBed.configureTestingModule({ imports: [StudentStudyPlansComponent], providers: [provideHttpClient(), provideHttpClientTesting()] }));
  afterEach(() => TestBed.inject(HttpTestingController).verify());
  const page = { items: [], totalCount: 0, pageNumber: 1, pageSize: 20 };

  it('allows the teacher to draft and publish for the selected student', async () => {
    const http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(StudentStudyPlansComponent);
    fixture.componentRef.setInput('staffStudentId', 'student-1');
    fixture.detectChanges(); await fixture.whenStable();
    http.expectOne('/api/coaching/staff/students/student-1/study/plans?pageNumber=1&pageSize=20').flush({ success: true, data: page });
    const component = fixture.componentInstance;
    await component.newDraft(); component.editTitle = 'Koç haftalık planı';
    component.editTasks = [{ title: 'Matematik', plannedDate: '2026-10-19', plannedMinutes: 30, topicId: null, isPinned: false }];
    component.saveDraft();
    const create = http.expectOne('/api/coaching/staff/students/student-1/study/plans');
    const draft = { id: 'plan-1', version: 0, status: 'Draft', title: 'Koç haftalık planı', tasks: [{ id: 'task-1', ...component.editTasks[0], isCompleted: false, actualMinutes: null }] };
    create.flush({ success: true, data: draft });
    http.expectOne('/api/coaching/staff/students/student-1/study/plans?pageNumber=1&pageSize=20').flush({ success: true, data: { ...page, items: [draft] } });
    component.publishConfirmed = true; component.publish();
    http.expectOne('/api/coaching/staff/students/student-1/study/plans/plan-1/publish').flush({ success: true, data: { ...draft, version: 1, status: 'Active' } });
    http.expectOne('/api/coaching/staff/students/student-1/study/plans?pageNumber=1&pageSize=20').flush({ success: true, data: { ...page, items: [draft] } });
    fixture.detectChanges(); await fixture.whenStable();
    expect(fixture.nativeElement.textContent).toContain('Matematik');
    expect(fixture.nativeElement.querySelector('input[type="number"]')).toBeNull();
  });

  it('keeps the institution read-only and cancels a previous student request', async () => {
    const http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(StudentStudyPlansComponent);
    fixture.componentRef.setInput('staffStudentId', 'student-1');
    fixture.componentRef.setInput('readOnly', true); fixture.detectChanges(); await fixture.whenStable();
    const old = http.expectOne('/api/coaching/staff/students/student-1/study/plans?pageNumber=1&pageSize=20');
    fixture.componentRef.setInput('staffStudentId', 'student-2'); fixture.detectChanges(); await fixture.whenStable();
    expect(old.cancelled).toBe(true);
    http.expectOne('/api/coaching/staff/students/student-2/study/plans?pageNumber=1&pageSize=20').flush({ success: true, data: page });
    await fixture.componentInstance.newDraft(); fixture.componentInstance.saveDraft();
    fixture.detectChanges(); await fixture.whenStable();
    expect(fixture.nativeElement.textContent).not.toContain('Yeni taslak');
  });
});

beforeEach(() => { TestBed.overrideProvider(ToasterService, {useValue:{confirm:vi.fn().mockResolvedValue(true),success:vi.fn(),error:vi.fn(),info:vi.fn(),warning:vi.fn()}}); });
