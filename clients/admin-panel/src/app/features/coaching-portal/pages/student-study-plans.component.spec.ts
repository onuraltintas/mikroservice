import { TestBed } from '@angular/core/testing';
import { of, Subject, throwError } from 'rxjs';
import { CoachingStudyPlanningService, StudyPlan } from '../../../core/services/coaching-study-planning.service';
import { StudentStudyPlansComponent } from './student-study-plans.component';

describe('StudentStudyPlansComponent', () => {
  const active: StudyPlan = { id: 'plan', version: 4, title: 'Planım', status: 'Active', tasks: [
    { id: 'task', plannedDate: '2026-10-06', title: 'Matematik', plannedMinutes: 30, topicId: null,
      isPinned: true, isCompleted: false, actualMinutes: null, completedAt: null }
  ] };
  function setup() {
    const service = {
      list: vi.fn(() => of({ items: [{ ...active, tasks: undefined }], totalCount: 1, pageNumber: 1, pageSize: 20 })),
      get: vi.fn(() => of(active)), create: vi.fn(() => of({ ...active, status: 'Draft' })),
      replace: vi.fn(() => of({ ...active, status: 'Draft' })), publish: vi.fn(() => of(active)),
      complete: vi.fn(() => of({ ...active, version: 5 })), reschedule: vi.fn(() => of({ ...active, version: 5 })),
      archive: vi.fn(() => of({ ...active, status: 'Archived' as const, version: 5 }))
    };
    TestBed.configureTestingModule({ imports: [StudentStudyPlansComponent], providers: [{ provide: CoachingStudyPlanningService, useValue: service }] });
    const fixture = TestBed.createComponent(StudentStudyPlansComponent);
    fixture.detectChanges();
    return { fixture, component: fixture.componentInstance, service };
  }
  it('lists plans, opens detail and offers active task controls', () => {
    const { fixture, component, service } = setup();
    component.open('plan');
    fixture.detectChanges();
    expect(service.get).toHaveBeenCalledWith('plan');
    expect(fixture.nativeElement.textContent).toContain('Matematik');
    expect(fixture.nativeElement.textContent).toContain('Tamamla');
  });
  it('requires explicit publication confirmation', () => {
    const { component, service } = setup();
    component.selected.set({ ...active, status: 'Draft' });
    component.publish();
    expect(service.publish).not.toHaveBeenCalled();
    component.publishConfirmed = true;
    component.publish();
    expect(service.publish).toHaveBeenCalledWith('plan', 4);
  });
  it('archives only a clean draft after explicit confirmation so regeneration is possible', () => {
    const { component, service } = setup();
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false);
    component.selected.set({ ...active, status: 'Draft' });
    component.archiveDraft(); expect(service.archive).not.toHaveBeenCalled();
    confirm.mockReturnValue(true);
    component.dirty = true; component.archiveDraft(); expect(service.archive).not.toHaveBeenCalled();
    component.dirty = false; component.archiveDraft();
    expect(service.archive).toHaveBeenCalledWith('plan', 4);
    expect(component.selected()?.status).toBe('Archived');
    expect(component.knownDraftId()).toBeNull();
    confirm.mockRestore();
  });
  it('does not mutate completed or archived tasks', () => {
    const { component, service } = setup();
    component.selected.set({ ...active, status: 'Archived' });
    component.complete(active.tasks[0], 30);
    expect(service.complete).not.toHaveBeenCalled();
    component.selected.set({ ...active, tasks: [{ ...active.tasks[0], isCompleted: true }] });
    component.reschedule(component.selected()!.tasks[0], '2026-10-08');
    expect(service.reschedule).not.toHaveBeenCalled();
  });
  it('sends current plan version and clears busy state on conflicts', () => {
    const { component, service } = setup();
    component.selected.set(active);
    service.complete.mockReturnValue(throwError(() => ({ status: 409, error: { message: 'Plan değişti.' } })));
    component.complete(active.tasks[0], 25);
    expect(service.complete).toHaveBeenCalledWith('plan', 'task', 4, 25);
    expect(component.busy()).toBe(false);
    expect(component.error()).toContain('Plan değişti');
    expect(component.selected()?.version).toBe(4);
  });
  it('blocks duplicate submissions and invalid durations', () => {
    const { component, service } = setup();
    component.selected.set(active);
    component.complete(active.tasks[0], 0);
    expect(service.complete).not.toHaveBeenCalled();
    const pending = new Subject<StudyPlan>();
    service.complete.mockReturnValue(pending);
    component.complete(active.tasks[0], 30);
    component.complete(active.tasks[0], 30);
    expect(service.complete).toHaveBeenCalledTimes(1);
    pending.complete();
    expect(component.busy()).toBe(false);
  });
  it('does not discard an unsaved draft when selecting another plan', () => {
    const { component, service } = setup();
    component.newDraft();
    component.dirty = true;
    component.open('plan');
    expect(service.get).not.toHaveBeenCalled();
    expect(component.error()).toContain('kaydedin');
  });
  it('asks before leaving unsaved work and protects browser refresh', () => {
    const { component } = setup();
    component.dirty = true;
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false);
    expect(component.canLeavePage()).toBe(false);
    confirm.mockReturnValue(true);
    expect(component.canLeavePage()).toBe(true);
    const event = new Event('beforeunload', { cancelable: true });
    component.beforeUnload(event as BeforeUnloadEvent);
    expect(event.defaultPrevented).toBe(true);
    confirm.mockRestore();
  });
  it('opens an existing draft instead of resetting it into a second draft', () => {
    const { component, service } = setup();
    component.selected.set({ ...active, status: 'Draft' });
    component.newDraft();
    expect(service.get).toHaveBeenCalledWith('plan');
    expect(component.selected()?.id).toBe('plan');
  });
  it('blocks navigation and warns on refresh during an in-flight write', () => {
    const { component } = setup();
    component.busy.set(true);
    component.dirty = false;
    expect(component.canLeavePage()).toBe(false);
    const event = new Event('beforeunload', { cancelable: true });
    component.beforeUnload(event as BeforeUnloadEvent);
    expect(event.defaultPrevented).toBe(true);
  });
});
