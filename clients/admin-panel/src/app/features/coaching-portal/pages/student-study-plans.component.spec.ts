import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, Router } from '@angular/router';
import { of, Subject, throwError } from 'rxjs';
import { CoachingStudyPlanningService, StudyPlan } from '../../../core/services/coaching-study-planning.service';
import { StudentStudyPlansComponent } from './student-study-plans.component';

describe('StudentStudyPlansComponent', () => {
  const active: StudyPlan = { id: 'plan', version: 4, title: 'Planım', status: 'Active', tasks: [
    { id: 'task', plannedDate: '2026-10-06', title: 'Matematik', plannedMinutes: 30, topicId: null,
      isPinned: true, isCompleted: false, actualMinutes: null, completedAt: null }
  ] };
  function setup(requestedPlanId: string | null = null) {
    const service = {
      list: vi.fn(() => of({ items: [{ ...active, tasks: undefined }], totalCount: 1, pageNumber: 1, pageSize: 20 })),
      get: vi.fn(() => of(active)), create: vi.fn(() => of({ ...active, status: 'Draft' })),
      replace: vi.fn(() => of({ ...active, status: 'Draft' })), publish: vi.fn(() => of(active)),
      complete: vi.fn(() => of({ ...active, version: 5 })), reschedule: vi.fn(() => of({ ...active, version: 5 })),
      archive: vi.fn(() => of({ ...active, status: 'Archived' as const, version: 5 }))
    };
    const router = { navigate: vi.fn(() => Promise.resolve(true)) };
    TestBed.configureTestingModule({ imports: [StudentStudyPlansComponent], providers: [
      { provide: Router, useValue: router },
      { provide: CoachingStudyPlanningService, useValue: service },
      { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap(requestedPlanId ? { plan: requestedPlanId } : {}) } } }
    ] });
    const fixture = TestBed.createComponent(StudentStudyPlansComponent);
    fixture.detectChanges();
    return { fixture, component: fixture.componentInstance, service, router };
  }
  it('opens the active plan when entering without a plan link', () => {
    const { component, service } = setup();
    expect(component.selected()?.id).toBe('plan');
    expect(service.get).toHaveBeenCalledTimes(1);
  });
  it('keeps the address aligned with the opened and newly saved plan', () => {
    const { component, service, router } = setup('old-plan');
    service.get.mockReturnValue(of({ ...active, id: 'new-plan' }));
    component.open('new-plan');
    expect(router.navigate).toHaveBeenLastCalledWith([], expect.objectContaining({ queryParams: { plan: 'new-plan' }, queryParamsHandling: 'merge', replaceUrl: true }));
    service.create.mockReturnValue(of({ ...active, id: 'draft-plan', status: 'Draft' }));
    component.newDraft(); component.editTitle = 'Yeni plan';
    component.editTasks[0].title = 'Test'; component.saveDraft();
    expect(router.navigate).toHaveBeenLastCalledWith([], expect.objectContaining({ queryParams: { plan: 'draft-plan' } }));
  });
  it('protects unsaved completion inputs on navigation, refresh and plan switching', () => {
    const { component, service } = setup('plan');
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false);
    component.actualMinutes['task'] = 17; component.actualQuestions['task'] = 8;
    expect(component.canLeavePage()).toBe(false);
    const event = new Event('beforeunload', { cancelable: true });
    component.beforeUnload(event as BeforeUnloadEvent);
    expect(event.defaultPrevented).toBe(true);
    service.get.mockClear(); component.open('another-plan');
    expect(service.get).not.toHaveBeenCalled();
    expect(component.actualMinutes['task']).toBe(17);
    expect(component.actualQuestions['task']).toBe(8);
    confirm.mockReturnValue(true);
    expect(component.canLeavePage()).toBe(true);
    confirm.mockRestore();
  });
  it('does not warn when completion fields are unchanged and preserves explicit zero changes', () => {
    const { component } = setup('plan');
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false);
    expect(component.canLeavePage()).toBe(true);
    expect(confirm).not.toHaveBeenCalled();
    component.actualQuestions['task'] = 0;
    expect(component.canLeavePage()).toBe(false);
    confirm.mockRestore();
  });
  it('shows validation beside the task and associates it with the invalid input', () => {
    const { component, fixture, service } = setup('plan');
    component.complete(active.tasks[0], 0); fixture.detectChanges();
    const task = fixture.nativeElement.querySelector('article.task');
    const alert = task.querySelector('[role="alert"]');
    expect(alert?.textContent).toContain('1–1440');
    const minutes = task.querySelector('input[type="number"]');
    expect(minutes.getAttribute('aria-invalid')).toBe('true');
    expect(minutes.getAttribute('aria-describedby')).toBe(alert.id);
    component.complete(active.tasks[0], 17, -1); fixture.detectChanges();
    expect(task.querySelector('[role="alert"]').textContent).toContain('0–100000');
    expect(service.complete).not.toHaveBeenCalled();
  });
  it('lists plans, opens detail and offers active task controls', () => {
    const { fixture, component, service } = setup();
    component.open('plan');
    fixture.detectChanges();
    expect(service.get).toHaveBeenCalledWith('plan');
    expect(fixture.nativeElement.textContent).toContain('Matematik');
    expect(fixture.nativeElement.textContent).toContain('Tamamla');
  });
  it('opens the requested own plan from the daily focus link only once', () => {
    const { component, service } = setup('plan');
    expect(service.get).toHaveBeenCalledWith('plan');
    expect(component.selected()?.id).toBe('plan');
    component.loadList();
    expect(service.get).toHaveBeenCalledTimes(1);
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
  it('retains draft source and planned questions and submits them when saved', () => {
    const { fixture, component, service } = setup();
    const draft = { ...active, status: 'Draft' as const, tasks: [{ ...active.tasks[0], sourceName: 'Kitap s.20', plannedQuestions: 25 }] };
    service.get.mockReturnValue(of(draft)); component.open('plan'); fixture.detectChanges();
    expect(component.editTasks[0]).toMatchObject({ sourceName: 'Kitap s.20', plannedQuestions: 25 });
    const sourceLabel = Array.from(fixture.nativeElement.querySelectorAll('label')).find((label: any) => label.textContent.includes('Kaynak / sayfa')) as HTMLLabelElement;
    expect(sourceLabel?.querySelector('input')).toBeTruthy();
    component.dirty = true; component.saveDraft();
    expect(service.replace).toHaveBeenCalledWith('plan', 4, { title: 'Planım', tasks: [expect.objectContaining({ sourceName: 'Kitap s.20', plannedQuestions: 25 })] });
  });
  it('submits explicit zero questions and rejects negative and fractional counts', () => {
    const { component, service } = setup(); component.open('plan');
    component.complete(active.tasks[0], 25, -1); component.complete(active.tasks[0], 25, 1.5);
    expect(service.complete).not.toHaveBeenCalled();
    component.complete(active.tasks[0], 25, 0);
    expect(service.complete).toHaveBeenCalledWith('plan', 'task', 4, 25, 0);
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
    expect(service.complete).toHaveBeenCalledWith('plan', 'task', 4, 25, null);
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
    service.get.mockClear();
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
