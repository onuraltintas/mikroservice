import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, Router } from '@angular/router';
import { of } from 'rxjs';
import { CoachingStudyPlanningService, StudyPlan, StudyTask } from '../../../core/services/coaching-study-planning.service';
import { CoachingLearningSetupService } from '../../../core/services/coaching-learning-setup.service';
import { StudentStudyPlansComponent } from './student-study-plans.component';

// T4 (31.10.2026): Planım for students — a week strip or a day-grouped list of compact task rows; one tap completes,
// everything else (partial work, remaining work, new date, correction) lives in a bottom sheet. Staff keep the full view.
const today = new Intl.DateTimeFormat('en-CA', { timeZone: 'Europe/Istanbul', year: 'numeric', month: '2-digit', day: '2-digit' }).format(new Date());
const addDays = (iso: string, days: number) => { const d = new Date(`${iso}T12:00:00Z`); d.setUTCDate(d.getUTCDate() + days); return d.toISOString().slice(0, 10); };
const task = (id: string, plannedDate: string, extra: Partial<StudyTask> = {}): StudyTask => ({
  id, plannedDate, title: `Çalışma ${id}`, plannedMinutes: 40, topicId: null, isPinned: false, isCompleted: false,
  actualMinutes: null, completedAt: null, lessonName: 'Matematik', plannedStartMinute: 600, ...extra
});
const plan: StudyPlan = { id: 'plan', version: 4, title: 'Ekim planı', status: 'Active', tasks: [
  task('a', today), task('b', today, { plannedStartMinute: 720, lessonName: 'Fizik' }),
  task('c', addDays(today, 1)), task('d', addDays(today, 2), { isCompleted: true, actualMinutes: 40, completedAt: `${today}T10:00:00Z` })
] };

async function setup(staffStudentId: string | null = null, requestedTaskId: string | null = null) {
  const service = {
    list: vi.fn(() => of({ items: [{ ...plan, tasks: undefined }], totalCount: 1, pageNumber: 1, pageSize: 20 })),
    get: vi.fn(() => of(plan)),
    complete: vi.fn(() => of({ ...plan, version: 5, tasks: plan.tasks.map(t => t.id === 'a' ? { ...t, isCompleted: true, actualMinutes: 40 } : t) })),
    recordRealization: vi.fn(() => of({ ...plan, version: 6 })),
    setRemainingScope: vi.fn(() => of(plan)), reschedule: vi.fn(() => of(plan)), create: vi.fn(), replace: vi.fn(), publish: vi.fn(), archive: vi.fn(),
    saveCalendarTask: vi.fn(), deleteCalendarTask: vi.fn()
  };
  TestBed.configureTestingModule({ imports: [StudentStudyPlansComponent], providers: [
    { provide: Router, useValue: { navigate: vi.fn(() => Promise.resolve(true)) } },
    { provide: CoachingLearningSetupService, useValue: { catalog: () => of({ lessons: [] }), catalogForStudent: () => of({ lessons: [] }) } },
    { provide: CoachingStudyPlanningService, useValue: service },
    { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap(requestedTaskId ? { plan: 'plan', task: requestedTaskId } : {}) } } }
  ] });
  const fixture = TestBed.createComponent(StudentStudyPlansComponent);
  fixture.componentInstance.staffStudentId = staffStudentId;
  fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
  return { fixture, component: fixture.componentInstance, service, el: fixture.nativeElement as HTMLElement };
}

describe('Planım for students (T4)', () => {
  it('opens on a week strip with today selected and lists only that day as compact rows', async () => {
    const { el, component } = await setup();
    expect(component.studentView()).toBe('week');
    const days = el.querySelectorAll('.week-strip [role=tab]');
    expect(days.length).toBe(7);
    expect(el.querySelector('.week-strip [aria-selected=true]')?.getAttribute('data-date')).toBe(today);
    const rows = [...el.querySelectorAll('app-plan-task-row')];
    expect(rows.map(row => row.querySelector('.title')?.textContent?.trim())).toEqual(['Çalışma a', 'Çalışma b']);
    // The inline realization form is gone from the student page: no number inputs until a task is opened.
    expect(el.querySelectorAll('.plan-tasks input').length).toBe(0);
    expect(el.querySelector('.plan-status')?.textContent).toContain('1/4');
  });

  it('switches to another day from the strip', async () => {
    const { el, fixture } = await setup();
    (el.querySelector(`.week-strip [data-date="${addDays(today, 1)}"]`) as HTMLButtonElement).click();
    fixture.detectChanges();
    expect([...el.querySelectorAll('app-plan-task-row .title')].map(n => n.textContent?.trim())).toEqual(['Çalışma c']);
  });

  it('groups the list view by day', async () => {
    const { el, fixture, component } = await setup();
    component.studentView.set('list'); component.taskFilter = 'all'; fixture.detectChanges();
    const headings = [...el.querySelectorAll('.day-group h3')].map(h => h.getAttribute('data-date'));
    expect(headings).toEqual([today, addDays(today, 1), addDays(today, 2)]);
  });

  it('completes a task with one tap using its planned minutes and can undo it', async () => {
    const { el, fixture, service } = await setup();
    (el.querySelector('app-plan-task-row button.complete') as HTMLButtonElement).click();
    expect(service.complete).toHaveBeenCalledWith('plan', 'a', 4, 40, null);
    fixture.detectChanges();
    const undo = el.querySelector('app-plan-task-row button.undo') as HTMLButtonElement;
    expect(undo?.textContent).toContain('Geri al');
    undo.click();
    expect(service.recordRealization).toHaveBeenCalledWith('plan', 'a',
      expect.objectContaining({ actualMinutes: 0, actualQuestions: null, isCompleted: false, expectedVersion: 5 }), true);
  });

  it('opens a task in the bottom sheet with partial work, remaining work and a new date', async () => {
    const { el, fixture } = await setup();
    (el.querySelector('app-plan-task-row button.main') as HTMLButtonElement).click();
    fixture.detectChanges();
    const sheet = el.querySelector('cs-bottom-sheet [role=dialog]');
    expect(sheet?.getAttribute('aria-label')).toBe('Çalışma a');
    expect(sheet?.textContent).toContain('Kısmi kaydet');
    expect(sheet?.textContent).toContain('Tarihi değiştir');
    expect(sheet?.querySelectorAll('input[type=number]').length).toBeGreaterThan(0);
  });

  it('shows completion validation in the sheet of that task', async () => {
    const { el, fixture, component } = await setup();
    component.complete(plan.tasks[0], 0); fixture.detectChanges();
    const sheet = el.querySelector('cs-bottom-sheet [role=dialog]');
    expect(sheet?.querySelector('[role=alert]')?.textContent).toContain('1–1440');
  });

  it('opens the linked task of a decision in the sheet on its day', async () => {
    const { el, component } = await setup(null, 'c');
    expect(component.selectedDay()).toBe(addDays(today, 1));
    expect(el.querySelector('cs-bottom-sheet [role=dialog]')?.getAttribute('aria-label')).toBe('Çalışma c');
  });

  it('keeps plan management in an overflow menu', async () => {
    const { el } = await setup();
    expect(el.querySelector('.plan-toolbar')).toBeNull();
    const menu = el.querySelector('details.plan-menu');
    expect(menu?.querySelector('summary')?.getAttribute('aria-label')).toBe('Plan işlemleri');
    expect(menu?.textContent).toContain('Yeni taslak');
    expect(menu?.textContent).toContain('Haftalık tablo');
    expect(menu?.querySelector('select#plan-filter')).not.toBeNull();
  });

  it('uses plain words instead of internal terms on the student page', async () => {
    const { el, fixture } = await setup();
    (el.querySelector('app-plan-task-row button.main') as HTMLButtonElement).click(); fixture.detectChanges();
    expect(el.textContent).not.toMatch(/kapsam|parmak izi/i);
  });

  it('leaves the staff view with its full task cards', async () => {
    const { el } = await setup('student-1');
    expect(el.querySelector('app-plan-task-row')).toBeNull();
    expect(el.querySelector('.plan-toolbar')).not.toBeNull();
  });
});
