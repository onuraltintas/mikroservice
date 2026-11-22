import { ToasterService } from '../../../core/services/toaster.service';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { StudentStudyPlansComponent } from './student-study-plans.component';

describe('shared staff plan editor', () => {
  beforeEach(() => TestBed.configureTestingModule({ imports: [StudentStudyPlansComponent], providers: [provideHttpClient(), provideHttpClientTesting()] }));
  afterEach(() => TestBed.inject(HttpTestingController).verify());
  const page = { items: [], totalCount: 0, pageNumber: 1, pageSize: 20 };

  it('22.11.2026: "Yeni taslak" opens an empty draft on its calendar; the teacher adds work there, names it and publishes it', async () => {
    const http = TestBed.inject(HttpTestingController);
    const base = '/api/coaching/staff/students/student-1/study';
    const fixture = TestBed.createComponent(StudentStudyPlansComponent);
    fixture.componentRef.setInput('staffStudentId', 'student-1');
    fixture.detectChanges(); await fixture.whenStable();
    http.expectOne(`${base}/plans?pageNumber=1&pageSize=20`).flush({ success: true, data: page });
    const component = fixture.componentInstance;
    await component.newDraft();
    // No form: an empty draft is created at once (the server adds any protected continuation).
    const create = http.expectOne(`${base}/plans`);
    expect(create.request.method).toBe('POST');
    expect(create.request.body.tasks).toEqual([]);
    expect(create.request.body.title).toMatch(/^Elle hazırlanan plan, \d{2}\.\d{2}\.\d{4}$/);
    const empty = { id: 'plan-1', version: 0, status: 'Draft', title: create.request.body.title, tasks: [] };
    create.flush({ success: true, data: empty });
    http.expectOne(`${base}/plans?pageNumber=1&pageSize=20`).flush({ success: true, data: { ...page, items: [empty], totalCount: 1 } });
    fixture.detectChanges(); await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;
    expect(component.planView()).toBe('calendar');
    expect(element.querySelector('app-study-plan-calendar')).not.toBeNull();
    expect(element.querySelector('[data-testid="draft-panel"]')?.textContent).toContain('Boş taslak');
    expect((element.querySelector('[data-testid="draft-guide"]') as HTMLDetailsElement).open).toBe(true);
    expect(element.querySelector('[data-testid="draft-empty-publish"]')?.textContent).toContain('en az bir çalışma eklenmeli');
    const publishButton = [...element.querySelectorAll('button')].find(b => b.textContent?.trim() === 'Planı yayımla') as HTMLButtonElement;
    expect(publishButton.disabled).toBe(true);
    expect(element.textContent).not.toContain('Taslağı kaydet');

    // Work is added on the calendar: lesson, then free work, a topic or a resource range.
    const adding = component.calendarCommand({ kind: 'add', taskIds: [], date: '2026-10-19', startMinute: 1080 });
    http.expectOne(`${base}/resources`).flush({ success: true, data: [] });
    http.expectOne(`${base}/learning-setup/catalog?pageNumber=1`).flush({ success: true, data: { curriculum: null, lessons: [{ id: 'math', name: 'Matematik' }], topics: { items: [], totalCount: 0, pageNumber: 1, pageSize: 50 }, missingDurationCount: 0, warnings: [] } });
    await adding;
    expect(component.calendarForm?.lessonId).toBe('math');
    component.calendarForm!.title = 'TEST Matematik';
    await component.saveCalendarForm();
    const add = http.expectOne(`${base}/plans/plan-1/tasks`);
    expect(add.request.body).toEqual(expect.objectContaining({ expectedVersion: 0, title: 'TEST Matematik', plannedDate: '2026-10-19', startMinute: 1080, plannedMinutes: 30, lessonId: 'math' }));
    const task = { id: 'task-1', title: 'TEST Matematik', plannedDate: '2026-10-19', plannedStartMinute: 1080, plannedMinutes: 30, topicId: null, isPinned: false, isCompleted: false, actualMinutes: null, completedAt: null, weeklyLessonId: 'math', lessonName: 'Matematik' };
    const draft = { ...empty, version: 1, tasks: [task] };
    add.flush({ success: true, data: draft });
    http.expectOne(`${base}/plans?pageNumber=1&pageSize=20`).flush({ success: true, data: { ...page, items: [draft], totalCount: 1 } });

    // Only the name is edited apart from the calendar.
    component.draftTitle = 'Koç haftalık planı';
    component.renameDraft();
    const rename = http.expectOne(`${base}/plans/plan-1/title`);
    expect([rename.request.method, rename.request.body]).toEqual(['PUT', { expectedVersion: 1, title: 'Koç haftalık planı' }]);
    const named = { ...draft, version: 2, title: 'Koç haftalık planı' };
    rename.flush({ success: true, data: named });
    http.expectOne(`${base}/plans?pageNumber=1&pageSize=20`).flush({ success: true, data: { ...page, items: [named], totalCount: 1 } });

    component.publishConfirmed = true; component.publish();
    http.expectOne(`${base}/plans/plan-1/publish`).flush({ success: true, data: { ...named, version: 3, status: 'Active' } });
    http.expectOne(`${base}/plans?pageNumber=1&pageSize=20`).flush({ success: true, data: { ...page, items: [named] } });
    component.planView.set('list'); fixture.detectChanges(); await fixture.whenStable();
    expect(element.textContent).toContain('TEST Matematik');
    expect(element.querySelector('[data-testid="draft-panel"]')).toBeNull();
    // Staff may record the remaining scope of an active plan, but only the student records
    // completed work (actual minutes, questions and the day it was done).
    const text = element.textContent as string;
    expect(text).not.toContain('Gerçek süre');
    expect(text).not.toContain('Fiili çalışma günü');
    expect(text).toContain('Yalnız kalan kapsamı kaydet');
  });

  it('opens the existing draft instead of making a second one', async () => {
    const http = TestBed.inject(HttpTestingController);
    const base = '/api/coaching/staff/students/student-1/study';
    const fixture = TestBed.createComponent(StudentStudyPlansComponent);
    fixture.componentRef.setInput('staffStudentId', 'student-1');
    fixture.detectChanges(); await fixture.whenStable();
    const draft = { id: 'plan-9', version: 0, status: 'Draft', title: 'Taslak', tasks: [] };
    http.expectOne(`${base}/plans?pageNumber=1&pageSize=20`).flush({ success: true, data: { ...page, items: [draft], totalCount: 1 } });
    await fixture.componentInstance.newDraft();
    http.expectOne(`${base}/plans/plan-9`).flush({ success: true, data: draft });
    http.expectNone(`${base}/plans`);
    expect(fixture.componentInstance.selected()?.id).toBe('plan-9');
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
    await fixture.componentInstance.newDraft();
    fixture.detectChanges(); await fixture.whenStable();
    expect(fixture.nativeElement.textContent).not.toContain('Yeni taslak');
  });
});

beforeEach(() => { TestBed.overrideProvider(ToasterService, {useValue:{confirm:vi.fn().mockResolvedValue(true),success:vi.fn(),error:vi.fn(),info:vi.fn(),warning:vi.fn()}}); });
