import { ToasterService } from '../../../core/services/toaster.service';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, Router } from '@angular/router';
import { of, Subject, throwError } from 'rxjs';
import { CoachingStudyPlanningService, StudyPlan } from '../../../core/services/coaching-study-planning.service';
import { CoachingLearningSetupService } from '../../../core/services/coaching-learning-setup.service';
import { StudentStudyPlansComponent } from './student-study-plans.component';

describe('StudentStudyPlansComponent', () => {
  it('defaults to the active plan and puts the picker above the full-width workspace', async () => {
    const { component, service, fixture } = await setup();
    fixture.detectChanges();
    expect(component.filter).toBe('Active'); expect(service.list).toHaveBeenCalledWith(1, 'Active');
    expect(component.selected()?.id).toBe('plan');
    expect(fixture.nativeElement.querySelector('.workspace > aside')).toBeNull();
    expect(fixture.nativeElement.querySelector('.plan-toolbar')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('.plan-picker').open).toBe(false);
  });
  it('finds the existing draft while the default active filter hides it', async () => {
    const { component, service } = await setup();
    const draft = { ...active, id: 'existing-draft', status: 'Draft' as const };
    service.list.mockReturnValueOnce(of({ items: [{ ...draft, tasks: undefined }], totalCount: 1, pageNumber: 1, pageSize: 20 }));
    service.get.mockReturnValueOnce(of(draft));
    await component.newDraft();
    expect(service.list).toHaveBeenLastCalledWith(1, 'Draft');
    expect(component.selected()?.id).toBe('existing-draft'); expect(service.create).not.toHaveBeenCalled();
  });
  it('keeps the active plan and completion fields if draft lookup fails', async () => {
    const { component, service } = await setup(); const selected = component.selected();
    service.list.mockReturnValueOnce(throwError(() => ({ status: 503 })));
    await component.newDraft();
    expect(component.selected()).toBe(selected); expect(component.editing).toBe(false);
    expect(component.error()).toContain('Taslak'); expect(component.busy()).toBe(false);
  });
  const active: StudyPlan = { id: 'plan', version: 4, title: 'Planım', status: 'Active', tasks: [
    { id: 'task', plannedDate: '2026-10-06', title: 'Matematik', plannedMinutes: 30, topicId: null,
      isPinned: true, isCompleted: false, actualMinutes: null, completedAt: null }
  ] };
  async function setup(requestedPlanId: string | null = null, requestedTaskId: string | null = null) {
    const service = {
      list: vi.fn(() => of({ items: [{ ...active, tasks: undefined }], totalCount: 1, pageNumber: 1, pageSize: 20 })),
      get: vi.fn(() => of(active)), create: vi.fn(() => of({ ...active, status: 'Draft' })),
      replace: vi.fn(() => of({ ...active, status: 'Draft' })), publish: vi.fn(() => of(active)),
      setRemainingScope:vi.fn(()=>of({...active,version:5})),
      recordRealization:vi.fn(()=>of({...active,version:5})),
      complete: vi.fn(() => of({ ...active, version: 5 })), reschedule: vi.fn(() => of({ ...active, version: 5 })),
      saveCalendarTask:vi.fn(()=>of({...active,version:5})),deleteCalendarTask:vi.fn(()=>of({...active,version:5})),
      archive: vi.fn(() => of({ ...active, status: 'Archived' as const, version: 5 }))
    };
    const router = { navigate: vi.fn(() => Promise.resolve(true)) };
    TestBed.configureTestingModule({ imports: [StudentStudyPlansComponent], providers: [
      { provide: Router, useValue: router },
      {provide:CoachingLearningSetupService,useValue:{catalog:()=>of({lessons:[{id:'math',name:'Matematik'}]}),catalogForStudent:()=>of({lessons:[{id:'math',name:'Matematik'}]})}},
      { provide: CoachingStudyPlanningService, useValue: service },
      { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap(requestedPlanId ? { plan: requestedPlanId, ...(requestedTaskId ? {task:requestedTaskId} : {}) } : {}) } } }
    ] });
    const fixture = TestBed.createComponent(StudentStudyPlansComponent);
    fixture.detectChanges(); await fixture.whenStable();
    return { fixture, component: fixture.componentInstance, service, router };
  }
  it('records selected actual work dates separately from the planned date for partial work and completion',async()=>{
    const{component,service}=await setup();const task=component.selected()!.tasks[0];
    component.workDates[task.id]='2026-09-28';component.actualMinutes[task.id]=12;component.remainingMinutes[task.id]=18;component.partial(task);
    expect(service.recordRealization).toHaveBeenCalledWith('plan','task',expect.objectContaining({workedOn:'2026-09-28',actualMinutes:12,isCompleted:false}));
    component.workDates[task.id]='2026-10-05';component.complete(task,30,10);
    expect(service.complete).toHaveBeenCalledWith('plan','task',5,30,10,'2026-10-05');
  });
  it('shows dated work and legacy provenance and corrects a work date with its old date and reason',async()=>{
    const{component,fixture,service}=await setup();const task={...component.selected()!.tasks[0],actualMinutes:20,actualQuestions:8,workDays:[{workedOn:'2026-09-28',minutes:20,questions:8,isLegacyDate:true}]};
    component.selected.set({...active,tasks:[task]});fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('fiili tarih bilinmiyor');
    component.startCorrection(task);component.correction!.reassignFromDate='2026-09-28';component.correction!.workedOn='2026-10-05';component.correction!.reason='Fiili gün yanlış seçilmiş';component.saveCorrection();
    expect(service.recordRealization).toHaveBeenCalledWith('plan','task',expect.objectContaining({actualMinutes:20,actualQuestions:8,workedOn:'2026-10-05',reassignFromDate:'2026-09-28',reason:'Fiili gün yanlış seçilmiş'}),true);
  });
  it('saves cumulative partial work without completing and keeps correction inputs on conflict',async()=>{
    const {component,service}=await setup();const task=component.selected()!.tasks[0];
    component.actualMinutes[task.id]=12;component.actualQuestions[task.id]=4;component.remainingMinutes[task.id]=18;component.remainingQuestions[task.id]=6;component.partial(task);
    expect(service.recordRealization).toHaveBeenCalledWith('plan','task',{expectedVersion:4,actualMinutes:12,actualQuestions:4,isCompleted:false,remainingMinutes:18,remainingQuestions:6});
    expect(service.complete).not.toHaveBeenCalled();
    component.selected.set({...active,version:5,status:'Archived',tasks:[{...task,isCompleted:true,actualMinutes:30,actualQuestions:10}]});
    component.startCorrection(component.selected()!.tasks[0]);component.correction!.minutes=25;component.correction!.reason='Yanlış süre girildi';
    service.recordRealization.mockReturnValueOnce(throwError(()=>({status:409,error:{message:'Plan değişti'}})));
    component.saveCorrection();expect(component.correction?.minutes).toBe(25);expect(component.correction?.reason).toBe('Yanlış süre girildi');
    expect(service.recordRealization).toHaveBeenLastCalledWith('plan','task',{expectedVersion:5,actualMinutes:25,actualQuestions:10,isCompleted:true,reason:'Yanlış süre girildi',workedOn:component.today},true);
  });
  it('declares remaining scope without fabricating actual work and filters preserved continuations out of manual replacements',async()=>{
    const{component,service}=await setup();const task=component.selected()!.tasks[0];component.remainingMinutes[task.id]=18;component.remainingQuestions[task.id]=6;
    component.saveRemainingScope(task);expect(service.setRemainingScope).toHaveBeenCalledWith('plan','task',4,18,6,null);expect(service.recordRealization).not.toHaveBeenCalled();expect(service.complete).not.toHaveBeenCalled();
    component.remainingMinutes[task.id]=null;component.remainingQuestions[task.id]=null;
    service.get.mockReturnValueOnce(of({...active,status:'Draft',tasks:[{...task,predecessorTaskId:'old'}]}));await component.open('plan');
    expect(component.editTasks).toHaveLength(0);
  });
  it('requires a declared remaining scope and shows the predecessor without copying previous actual work',async()=>{
    const {component,service,fixture}=await setup();const task=component.selected()!.tasks[0];
    component.actualMinutes[task.id]=12;component.remainingMinutes[task.id]=null;component.partial(task);expect(service.recordRealization).not.toHaveBeenCalled();
    expect(component.error()).toContain('kalan süreyi');
    component.selected.set({...active,tasks:[{...task,predecessorTaskId:'old-task',originalTaskId:'root',plannedMinutes:18,actualMinutes:null}]});fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('önceki gerçekleşmeler kaynak görevde korunur');
  });
  it('can withdraw an erroneous realization with a reason while preserving its history',async()=>{
    const {component,service}=await setup();const task={...component.selected()!.tasks[0],actualMinutes:20,isCompleted:true};
    component.startCorrection(task);Object.assign(component.correction!,{minutes:0,questions:null,completed:false,reason:'Yanlışlıkla kaydedildi'});
    component.saveCorrection();expect(service.recordRealization).toHaveBeenCalledWith('plan','task',{expectedVersion:4,actualMinutes:0,actualQuestions:null,isCompleted:false,reason:'Yanlışlıkla kaydedildi',workedOn:component.today},true);
  });
  it('rejects a short correction reason and denies student realization writes in staff view',async()=>{
    const {component,service}=await setup();const task={...component.selected()!.tasks[0],actualMinutes:20};
    component.startCorrection(task);component.correction!.reason='az';component.saveCorrection();expect(service.recordRealization).not.toHaveBeenCalled();
    component.staffStudentId='student';component.partial(task);component.correction!.reason='Öğretmen değişikliği';component.saveCorrection();expect(service.recordRealization).not.toHaveBeenCalled();
  });
  it('requires current continuation scope for an ancestor scope correction after further progress and submits its version',async()=>{
    const{component,service,fixture}=await setup();
    const task={...active.tasks[0],actualMinutes:20,remainingMinutes:40,remainingQuestions:12,continuation:{revisionId:'current',revisionVersion:7,taskId:'copy',isCompleted:false,hasOwnCompletion:false,remainingMinutes:15,remainingQuestions:5,requiresExplicitScope:true}};
    component.selected.set({...active,status:'Archived',tasks:[task]});component.startCorrection(task);
    Object.assign(component.correction!,{remainingMinutes:30,remainingQuestions:9,reason:'Kalan kapsam yanlış girilmiş'});
    fixture.detectChanges();expect(fixture.nativeElement.textContent).toContain('Aktif devamda güncel kalan süre');
    component.saveCorrection();expect(service.recordRealization).not.toHaveBeenCalled();
    Object.assign(component.correction!,{continuationMinutes:10,continuationQuestions:3});component.saveCorrection();
    expect(service.recordRealization).toHaveBeenCalledWith('plan','task',expect.objectContaining({remainingMinutes:30,continuationScope:{revisionId:'current',expectedVersion:7,remainingMinutes:10,remainingQuestions:3}}),true);
  });
  it('does not require continuation scope for effort-only changes or an independently completed continuation',async()=>{
    const{component,service}=await setup();
    const task={...active.tasks[0],actualMinutes:20,remainingMinutes:40,continuation:{revisionId:'current',revisionVersion:7,taskId:'copy',isCompleted:false,hasOwnCompletion:false,remainingMinutes:15,remainingQuestions:5,requiresExplicitScope:true}};
    component.selected.set({...active,status:'Archived',tasks:[task]});component.startCorrection(task);
    Object.assign(component.correction!,{minutes:18,reason:'Yalnız gerçekleşen süre hatalı'});component.saveCorrection();
    expect(service.recordRealization).toHaveBeenCalledWith('plan','task',expect.not.objectContaining({continuationScope:expect.anything()}),true);
    task.continuation.hasOwnCompletion=true;task.continuation.isCompleted=true;component.startCorrection(task);component.correction!.remainingMinutes=30;
    expect(component.continuationScopeRequired(task)).toBe(false);
  });
  it('explains inherited completion without fabricating effort and labels reconciliation history',async()=>{
    const{component,fixture}=await setup();
    component.selected.set({...active,tasks:[{...active.tasks[0],isCompleted:true,actualMinutes:null,completionSourceTaskId:'source',realizationHistory:[{at:'2026-10-24T12:00:00Z',actorUserId:'student',action:'ContinuationReconciliation',previousMinutes:null,previousQuestions:null,previouslyCompleted:false,previousCompletedAt:null,minutes:0,questions:null,completed:true,reason:'Geçmiş düzeltmeyle uzlaştırıldı'}]}]});
    fixture.detectChanges();expect(fixture.nativeElement.textContent).toContain('Fiili süre ve sorular yapıldıkları kayıtta korunur');expect(fixture.nativeElement.textContent).toContain('Devam ilerlemesi uzlaştırıldı');
  });
  it('creates a calendar study with the chosen time and dynamic lesson, keeping the modal on a conflict',async()=>{
    const {component,service,fixture}=await setup();
    await component.calendarCommand({kind:'add',taskIds:[],date:'2026-10-22',startMinute:1110});
    expect(component.calendarForm?.lessonId).toBe('math');expect(component.calendarForm?.time).toBe('18:30');
    component.calendarForm!.title='Soru çözümü';
    service.saveCalendarTask.mockReturnValueOnce(throwError(()=>({status:409,error:{message:'Saat dolu'}})));
    await component.saveCalendarForm();expect(component.calendarForm).not.toBeNull();
    expect(service.saveCalendarTask).toHaveBeenCalledWith('plan',null,{expectedVersion:4,title:'Soru çözümü',plannedDate:'2026-10-22',startMinute:1110,plannedMinutes:30,lessonId:'math'});
    await component.saveCalendarForm();expect(component.calendarForm).toBeNull();fixture.detectChanges();
  });
  it('updates untouched completion defaults after changing duration while preserving a real input',async()=>{
    const {component,service}=await setup();
    await component.calendarCommand({kind:'edit',taskIds:['task'],date:'2026-10-06',startMinute:540});
    component.calendarForm!.minutes=60;
    service.saveCalendarTask.mockReturnValueOnce(of({...active,version:5,tasks:[{...active.tasks[0],plannedMinutes:60}]}));
    await component.saveCalendarForm();expect(component.actualMinutes['task']).toBe(60);
    const toaster=TestBed.inject(ToasterService);vi.mocked(toaster.confirm).mockClear();await component.canLeavePage();expect(toaster.confirm).not.toHaveBeenCalled();
    component.actualMinutes['task']=17;
    await component.calendarCommand({kind:'edit',taskIds:['task'],date:'2026-10-06',startMinute:540});component.calendarForm!.minutes=90;
    service.saveCalendarTask.mockReturnValueOnce(of({...active,version:6,tasks:[{...active.tasks[0],plannedMinutes:90}]}));
    await component.saveCalendarForm();expect(component.actualMinutes['task']).toBe(17);
  });
  it('selects an individual outcome piece for completion and never allows teacher completion',async()=>{
    const {component,service}=await setup();
    await component.calendarCommand({kind:'complete',taskIds:['task'],date:'2026-10-06',startMinute:540});
    component.calendarForm!.actualMinutes=24;component.calendarForm!.actualQuestions=0;await component.saveCalendarForm();
    expect(service.complete).toHaveBeenCalledWith('plan','task',4,24,0);
    component.staffStudentId='student';await component.calendarCommand({kind:'complete',taskIds:['task'],date:'2026-10-06',startMinute:540});
    expect(component.calendarForm).toBeNull();
  });
  it('removes only the selected pending study after central confirmation and blocks readonly actions',async()=>{
    const {component,service}=await setup();await component.calendarCommand({kind:'delete',taskIds:['task'],date:'2026-10-06',startMinute:540});
    await component.saveCalendarForm();expect(service.deleteCalendarTask).toHaveBeenCalledWith('plan','task',4);expect(component.calendarForm).toBeNull();
    component.readOnly=true;await component.calendarCommand({kind:'edit',taskIds:['task'],date:'2026-10-06',startMinute:540});expect(component.calendarForm).toBeNull();
  });
  it('opens the active plan when entering without a plan link', async () => {
    const { component, service } = await setup();
    expect(component.selected()?.id).toBe('plan');
    expect(service.get).toHaveBeenCalledTimes(1);
  });
  it('opens the exact task from a weekly decision link without completing or editing work',async ()=>{
    const{component,service}=await setup('plan','task');expect(component.selectedTaskIds()).toEqual(['task']);expect(component.visibleTasks().map(x=>x.id)).toEqual(['task']);
    expect(service.complete).not.toHaveBeenCalled();expect(service.replace).not.toHaveBeenCalled();
  });
  it('opens the exact tracking task even outside the first page without altering work', async () => {
    const { component, service, fixture } = await setup();
    component.selected.set(null); component.staffStudentId = 'student-3';
    component.initialPlanId = 'plan'; component.initialTaskId = 'task';
    await component.open('plan'); fixture.detectChanges(); await fixture.whenStable();
    expect(service.get).toHaveBeenLastCalledWith('plan', 'student-3');
    expect(component.planView()).toBe('calendar'); expect(component.selectedTaskIds()).toEqual(['task']);
    expect(component.visibleTasks().map(t => t.id)).toEqual(['task']);
    expect(service.replace).not.toHaveBeenCalled(); expect(service.complete).not.toHaveBeenCalled();
  });
  it('refreshes an externally updated plan without discarding draft or completion inputs', async () => {
    const { component, service } = await setup();
    const count = service.get.mock.calls.length;
    component.dirty = true; await component.refreshAfterExternalChange(); expect(service.get).toHaveBeenCalledTimes(count);
    component.dirty = false; component.actualMinutes['task'] = 17; await component.refreshAfterExternalChange();
    expect(service.get).toHaveBeenCalledTimes(count); expect(component.actualMinutes['task']).toBe(17);
    component.actualMinutes['task'] = active.tasks[0].plannedMinutes; await component.refreshAfterExternalChange(); expect(service.get).toHaveBeenCalledTimes(count + 1);
  });
  it('opens generated yearly plans in a calendar without changing saved work',async ()=>{
    const {component,service,fixture}=await setup();
    const generated:StudyPlan={...active,weeklySource:{draftVersion:1,learningContextVersion:2,startDate:'2026-10-01',endDate:'2027-06-01',reuseFreedSlots:false,fingerprint:'saved',activeRevisionId:null,activeRevisionVersion:null,timeZoneId:'Europe/Istanbul'},tasks:[{...active.tasks[0],plannedStartMinute:1080,weeklyLessonId:'math',lessonName:'Matematik'}]};
    const before=JSON.stringify(generated);service.get.mockReturnValue(of(generated));await component.open('plan');fixture.detectChanges(); await fixture.whenStable();
    expect(fixture.nativeElement.querySelector('app-study-plan-calendar')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('[aria-label="Plan görünümü"]')).not.toBeNull();
    expect(JSON.stringify(generated)).toBe(before);expect(service.replace).not.toHaveBeenCalled();expect(service.complete).not.toHaveBeenCalled();
  });
  it('uses existing completion inputs from calendar details and preserves unrelated unsaved work on updates',async ()=>{
    const{component,service,fixture}=await setup();const generated:StudyPlan={...active,weeklySource:{draftVersion:1,learningContextVersion:2,startDate:'2026-10-01',endDate:'2027-06-01',reuseFreedSlots:false,fingerprint:'saved',activeRevisionId:null,activeRevisionVersion:null},tasks:[active.tasks[0],{...active.tasks[0],id:'second'}]};
    service.get.mockReturnValue(of(generated));await component.open('plan');component.selectCalendarTasks(['task','second']);fixture.detectChanges(); await fixture.whenStable();expect(fixture.nativeElement.querySelectorAll('article.task')).toHaveLength(2);
    component.actualMinutes['second']=17;component.actualQuestions['second']=0;service.complete.mockReturnValue(of({...generated,version:5,tasks:generated.tasks.map(t=>t.id==='task'?{...t,isCompleted:true,actualMinutes:25}:t)}));
    component.complete(generated.tasks[0],25,0);fixture.detectChanges(); await fixture.whenStable();expect(component.planView()).toBe('calendar');expect(component.actualMinutes['second']).toBe(17);expect(component.actualQuestions['second']).toBe(0);expect(component.selectedTaskIds()).toEqual(['task','second']);
    expect(service.complete).toHaveBeenCalledWith('plan','task',4,25,0);expect(component.selected()?.tasks[0].isCompleted).toBe(true);expect(service.replace).not.toHaveBeenCalled();
  });
  it('focuses the details again when another calendar card is selected',async()=>{
    const{component,fixture}=await setup();component.planView.set('calendar');component.selectCalendarTasks(['task']);fixture.detectChanges(); await fixture.whenStable();
    const detail=fixture.nativeElement.querySelector('.calendar-task-detail') as HTMLElement;expect(document.activeElement).toBe(detail);detail.blur();
    component.selectCalendarTasks(['task']);fixture.detectChanges(); await fixture.whenStable();expect(document.activeElement).toBe(detail);
  });
  it('shows generated weekly drafts with their times and publish controls without a manual editor',async ()=>{
    const{component,service,fixture}=await setup();service.get.mockReturnValue(of({...active,status:'Draft',weeklySource:{startDate:'2027-05-24',endDate:'2027-06-01'},tasks:[{...active.tasks[0],plannedStartMinute:1080,lessonName:'Matematik'}]} as any));
    await component.open('plan');fixture.detectChanges(); await fixture.whenStable();expect(component.editing).toBe(false);expect(fixture.nativeElement.textContent).toContain('18:00');expect(fixture.nativeElement.textContent).toContain('Planı yayımla');expect(fixture.nativeElement.querySelector('input[name="planTitle"]')).toBeNull();
  });
  it('shows the saved outcome and estimate version to students and staff', async () => {
    const { component, service, fixture } = await setup();
    service.get.mockReturnValue(of({ ...active, tasks: [{ ...active.tasks[0], outcomes: [{ outcomeId: 'o', code: 'M.8.1.1.1', name: 'Çarpanları bulur', estimateId: 'e', estimateNumber: 2, allocatedMinutes: 30, origin: 'Catalog' }] }] } as any));
    await component.open('plan'); fixture.detectChanges(); await fixture.whenStable();
    expect(fixture.nativeElement.textContent).toContain('M.8.1.1.1');
    expect(fixture.nativeElement.textContent).toContain('süre sürümü 2');
  });
  it('keeps the address aligned with the opened and newly saved plan', async () => {
    const { component, service, router } = await setup('old-plan');
    service.get.mockReturnValue(of({ ...active, id: 'new-plan' }));
    await component.open('new-plan');
    expect(router.navigate).toHaveBeenLastCalledWith([], expect.objectContaining({ queryParams: { plan: 'new-plan' }, queryParamsHandling: 'merge', replaceUrl: true }));
    service.create.mockReturnValue(of({ ...active, id: 'draft-plan', status: 'Draft' }));
    await component.newDraft(); component.editTitle = 'Yeni plan';
    component.editTasks[0].title = 'Test'; component.saveDraft();
    expect(router.navigate).toHaveBeenLastCalledWith([], expect.objectContaining({ queryParams: { plan: 'draft-plan' } }));
  });
  it('protects unsaved completion inputs on navigation, refresh and plan switching', async () => {
    const { component, service } = await setup('plan');
    const confirm = vi.spyOn(TestBed.inject(ToasterService), 'confirm').mockResolvedValue(false);
    component.actualMinutes['task'] = 17; component.actualQuestions['task'] = 8;
    expect(await component.canLeavePage()).toBe(false);
    const event = new Event('beforeunload', { cancelable: true });
    window.dispatchEvent(event as BeforeUnloadEvent);
    expect(event.defaultPrevented).toBe(false);
    service.get.mockClear(); await component.open('another-plan');
    expect(service.get).not.toHaveBeenCalled();
    expect(component.actualMinutes['task']).toBe(17);
    expect(component.actualQuestions['task']).toBe(8);
    confirm.mockResolvedValue(true);
    expect(await component.canLeavePage()).toBe(true);
    confirm.mockRestore();
  });
  it('does not warn when completion fields are unchanged and preserves explicit zero changes', async () => {
    const { component } = await setup('plan');
    const confirm = vi.spyOn(TestBed.inject(ToasterService), 'confirm').mockResolvedValue(false);
    expect(await component.canLeavePage()).toBe(true);
    expect(confirm).not.toHaveBeenCalled();
    component.actualQuestions['task'] = 0;
    expect(await component.canLeavePage()).toBe(false);
    confirm.mockRestore();
  });
  it('shows validation beside the task and associates it with the invalid input', async () => {
    const { component, fixture, service } = await setup('plan');
    component.complete(active.tasks[0], 0); fixture.detectChanges(); await fixture.whenStable();
    const task = fixture.nativeElement.querySelector('article.task');
    const alert = task.querySelector('[role="alert"]');
    expect(alert?.textContent).toContain('1–1440');
    const minutes = task.querySelector('input[type="number"]');
    expect(minutes.getAttribute('aria-invalid')).toBe('true');
    expect(minutes.getAttribute('aria-describedby')).toBe(alert.id);
    component.complete(active.tasks[0], 17, -1); fixture.detectChanges(); await fixture.whenStable();
    expect(task.querySelector('[role="alert"]').textContent).toContain('0–100000');
    expect(service.complete).not.toHaveBeenCalled();
  });
  it('keeps other unsaved task inputs after completing one task and on a failed save', async () => {
    const { component, service } = await setup('plan');
    const second = { ...active.tasks[0], id: 'second' };
    const plan = { ...active, tasks: [active.tasks[0], second] };
    service.get.mockReturnValue(of(plan)); await component.open('plan');
    component.actualMinutes['second'] = 19; component.actualQuestions['second'] = 0;
    service.complete.mockReturnValue(of({ ...plan, version: 5, tasks: [{ ...active.tasks[0], isCompleted: true, actualMinutes: 17, actualQuestions: 8 }, second] }));
    component.complete(active.tasks[0], 17, 8);
    expect(component.actualMinutes['second']).toBe(19);
    expect(component.actualQuestions['second']).toBe(0);
    service.complete.mockReturnValue(throwError(() => ({ status: 503 })));
    component.complete(second, 19, 0);
    expect(component.actualMinutes['second']).toBe(19);
    expect(component.actualQuestions['second']).toBe(0);
  });
  it('lists plans, opens detail and offers active task controls', async () => {
    const { fixture, component, service } = await setup();
    await component.open('plan');
    fixture.detectChanges(); await fixture.whenStable();
    expect(service.get).toHaveBeenCalledWith('plan');
    expect(fixture.nativeElement.textContent).toContain('Matematik');
    expect(fixture.nativeElement.textContent).toContain('Tamamla');
  });
  it('opens the requested own plan from the daily focus link only once', async () => {
    const { component, service } = await setup('plan');
    expect(service.get).toHaveBeenCalledWith('plan');
    expect(component.selected()?.id).toBe('plan');
    component.loadList();
    expect(service.get).toHaveBeenCalledTimes(1);
  });
  it('requires explicit publication confirmation', async () => {
    const { component, service } = await setup();
    component.selected.set({ ...active, status: 'Draft' });
    component.publish();
    expect(service.publish).not.toHaveBeenCalled();
    component.publishConfirmed = true;
    component.publish();
    expect(service.publish).toHaveBeenCalledWith('plan', 4);
  });
  it('retains draft source and planned questions and submits them when saved', async () => {
    const { fixture, component, service } = await setup();
    const draft = { ...active, status: 'Draft' as const, tasks: [{ ...active.tasks[0], sourceName: 'Kitap s.20', plannedQuestions: 25 }] };
    service.get.mockReturnValue(of(draft)); await component.open('plan'); fixture.detectChanges(); await fixture.whenStable();
    expect(component.editTasks[0]).toMatchObject({ sourceName: 'Kitap s.20', plannedQuestions: 25 });
    const sourceLabel = Array.from(fixture.nativeElement.querySelectorAll('label')).find((label: any) => label.textContent.includes('Kaynak / sayfa')) as HTMLLabelElement;
    expect(sourceLabel?.querySelector('input')).toBeTruthy();
    component.dirty = true; component.saveDraft();
    expect(service.replace).toHaveBeenCalledWith('plan', 4, { title: 'Planım', tasks: [expect.objectContaining({ sourceName: 'Kitap s.20', plannedQuestions: 25 })] });
  });
  it('shows why the plan engine chose a task (roadmap O7)', async () => {
    const { fixture, component, service } = await setup();
    service.get.mockReturnValue(of({ ...active, tasks: [{ ...active.tasks[0], planKind: 'Spaced', planReason: 'Aralıklı tekrar: konuyu bitirdikten 7 gün sonra' }] }));
    await component.open('plan'); fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
    const text = Array.from(fixture.nativeElement.querySelectorAll('[data-testid="plan-reason"]')).map((x: any) => x.textContent).join(' ');
    expect(text).toContain('Aralıklı tekrar · Aralıklı tekrar: konuyu bitirdikten 7 gün sonra');
  });
  it('submits explicit zero questions and rejects negative and fractional counts', async () => {
    const { component, service } = await setup(); await component.open('plan');
    component.complete(active.tasks[0], 25, -1); component.complete(active.tasks[0], 25, 1.5);
    expect(service.complete).not.toHaveBeenCalled();
    component.complete(active.tasks[0], 25, 0);
    expect(service.complete).toHaveBeenCalledWith('plan', 'task', 4, 25, 0);
  });
  it('archives only a clean draft after explicit confirmation so regeneration is possible', async () => {
    const { component, service } = await setup();
    const confirm = vi.spyOn(TestBed.inject(ToasterService), 'confirm').mockResolvedValue(false);
    component.selected.set({ ...active, status: 'Draft' });
    await component.archiveDraft(); expect(service.archive).not.toHaveBeenCalled();
    confirm.mockResolvedValue(true);
    component.dirty = true; await component.archiveDraft(); expect(service.archive).not.toHaveBeenCalled();
    component.dirty = false; await component.archiveDraft();
    expect(service.archive).toHaveBeenCalledWith('plan', 4);
    expect(component.selected()?.status).toBe('Archived');
    expect(component.knownDraftId()).toBeNull();
    confirm.mockRestore();
  });
  it('does not mutate completed or archived tasks', async () => {
    const { component, service } = await setup();
    component.selected.set({ ...active, status: 'Archived' });
    component.complete(active.tasks[0], 30);
    expect(service.complete).not.toHaveBeenCalled();
    component.selected.set({ ...active, tasks: [{ ...active.tasks[0], isCompleted: true }] });
    component.reschedule(component.selected()!.tasks[0], '2026-10-08');
    expect(service.reschedule).not.toHaveBeenCalled();
  });
  it('sends current plan version and clears busy state on conflicts', async () => {
    const { component, service } = await setup();
    component.selected.set(active);
    service.complete.mockReturnValue(throwError(() => ({ status: 409, error: { message: 'Plan değişti.' } })));
    component.complete(active.tasks[0], 25);
    expect(service.complete).toHaveBeenCalledWith('plan', 'task', 4, 25, null);
    expect(component.busy()).toBe(false);
    expect(component.error()).toContain('Plan değişti');
    expect(component.selected()?.version).toBe(4);
  });
  it('blocks duplicate submissions and invalid durations', async () => {
    const { component, service } = await setup();
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
  it('does not discard an unsaved draft when selecting another plan', async () => {
    const { component, service } = await setup();
    await component.newDraft();
    component.dirty = true;
    service.get.mockClear();
    await component.open('plan');
    expect(service.get).not.toHaveBeenCalled();
    expect(component.error()).toContain('kaydet');
  });
  it('asks before leaving unsaved work and protects browser refresh', async () => {
    const { component } = await setup();
    component.dirty = true;
    const confirm = vi.spyOn(TestBed.inject(ToasterService), 'confirm').mockResolvedValue(false);
    expect(await component.canLeavePage()).toBe(false);
    confirm.mockResolvedValue(true);
    expect(await component.canLeavePage()).toBe(true);
    const event = new Event('beforeunload', { cancelable: true });
    window.dispatchEvent(event as BeforeUnloadEvent);
    expect(event.defaultPrevented).toBe(false);
    confirm.mockRestore();
  });
  it('opens an existing draft instead of resetting it into a second draft', async () => {
    const { component, service } = await setup();
    component.selected.set({ ...active, status: 'Draft' });
    await component.newDraft();
    expect(service.get).toHaveBeenCalledWith('plan');
    expect(component.selected()?.id).toBe('plan');
  });
  it('blocks navigation and warns on refresh during an in-flight write', async () => {
    const { component } = await setup();
    component.busy.set(true);
    component.dirty = false;
    expect(await component.canLeavePage()).toBe(false);
    const event = new Event('beforeunload', { cancelable: true });
    window.dispatchEvent(event as BeforeUnloadEvent);
    expect(event.defaultPrevented).toBe(false);
  });
});

beforeEach(() => { TestBed.overrideProvider(ToasterService, {useValue:{confirm:vi.fn().mockResolvedValue(true),success:vi.fn(),error:vi.fn(),info:vi.fn(),warning:vi.fn()}}); });
