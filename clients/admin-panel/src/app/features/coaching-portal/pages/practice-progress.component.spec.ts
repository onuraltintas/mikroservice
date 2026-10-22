import { DeferBlockBehavior } from '@angular/core/testing';
import { ToasterService } from '../../../core/services/toaster.service';
import { CoachingPracticeAdaptationService } from '../../../core/services/coaching-practice-adaptation.service';
import { TestBed } from '@angular/core/testing';
import { of, Subject, throwError } from 'rxjs';
import { CoachingPracticeProgressService, PracticeProgress } from '../../../core/services/coaching-practice-progress.service';
import { CoachingStudyPlanningService } from '../../../core/services/coaching-study-planning.service';
import { PracticeProgressComponent } from './practice-progress.component';
import { DeferBlockState } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { PracticeAdaptationComponent } from './practice-adaptation.component';
import { WeeklyReviewComponent } from './weekly-review.component';
import { CoachingWeeklyReviewService } from '../../../core/services/coaching-weekly-review.service';

const sample: PracticeProgress = {
  fromDate:'2026-10-01',toDate:'2026-10-20',today:'2026-10-20',timeZoneId:'Europe/Istanbul',scheduledTasks:3,completedTasks:1,overdueTasks:1,pinnedOverdueTasks:0,
  recoveryReason:'ReviewBeforeApplying',recovery:[{planId:'plan',expectedVersion:3,taskIds:['task'],title:'Matematik',previousDate:'2026-10-19',previousStartMinute:600,plannedDate:'2026-10-21',startMinute:1110,minutes:60}],
  trends:[{examCode:'TYT',name:'TYT',source:'StudentReported',change:5,points:[{examId:'exam',title:'Deneme',examDate:'2026-10-19T09:00:00Z',net:60}],sections:[{code:'MAT',name:'Matematik',questionCount:40,averageNet:15,netPercentage:37.5,needsReview:true}]}],
  goals:[{goalId:'goal',title:'Hedefim',examCode:'TYT',targetNet:75,catalogName:'Üniversite programı',catalogYear:2026,scoreYear:null,placementStage:null,minimumScore:350,rank:null,percentile:null,comparisons:[{source:'StudentReported',examDate:'2026-10-19T09:00:00Z',latestNet:60,remainingNet:15}]}]
};
async function setup() {
  const service={get:vi.fn(()=>of(structuredClone(sample)))}; const plans={move:vi.fn(()=>of({}))};
  TestBed.configureTestingModule({deferBlockBehavior: DeferBlockBehavior.Manual,imports:[PracticeProgressComponent],providers:[{provide:CoachingWeeklyReviewService,useValue:{get:vi.fn(()=>of({weekStart:'2026-10-12',today:'2026-10-20',reflection:null,summary:{scheduledTasks:0,completedTasks:0,plannedMinutes:0,actualMinutes:null},decisions:[],totalDecisions:0,decisionPage:1,canReflect:true,canDecide:false}))}},{provide:CoachingPracticeAdaptationService,useValue:{get:vi.fn(()=>of({exams:[],advice:[]}))}},{provide:CoachingPracticeProgressService,useValue:service},{provide:CoachingStudyPlanningService,useValue:plans}]});
  await TestBed.compileComponents();
  const fixture=TestBed.createComponent(PracticeProgressComponent);fixture.detectChanges(); await fixture.whenStable();return {fixture,component:fixture.componentInstance,service,plans};
}
describe('PracticeProgressComponent',async()=>{
  afterEach(()=>vi.restoreAllMocks());
  it('retains the weekly form across tabs, guards navigation and refresh, and reloads an approved student scope change',async()=>{
    const {fixture,component,service}=await setup();fixture.componentRef.setInput('tabbed',true);fixture.detectChanges(); await fixture.whenStable();
    const blocks=await fixture.getDeferBlocks();await blocks[1].render(DeferBlockState.Complete);fixture.detectChanges(); await fixture.whenStable();
    const child=fixture.debugElement.query(By.directive(WeeklyReviewComponent)).componentInstance as WeeklyReviewComponent;
    child.reflection.successes='Kaybolmayan taslak';child.dirty=true;
    component.activeTab.set('work');fixture.detectChanges(); await fixture.whenStable();component.activeTab.set('goals');fixture.detectChanges(); await fixture.whenStable();component.activeTab.set('work');fixture.detectChanges(); await fixture.whenStable();
    expect(fixture.debugElement.query(By.directive(WeeklyReviewComponent)).componentInstance).toBe(child);expect(child.reflection.successes).toBe('Kaybolmayan taslak');
    const calls=service.get.mock.calls.length;await component.load();expect(service.get).toHaveBeenCalledTimes(calls);expect(component.canRefresh()).toBe(false);
    vi.spyOn(TestBed.inject(ToasterService), 'confirm').mockResolvedValue(false);expect(await component.canLeavePage()).toBe(false);
    fixture.componentRef.setInput('staffStudentId','next-student');fixture.detectChanges(); await fixture.whenStable();expect(service.get).toHaveBeenLastCalledWith('next-student','','');
    expect(child.staffStudentId).toBe('next-student');expect(child.dirty).toBe(false);
  });
  it('keeps report dates, pending approval and the same analysis editor across accessible tabs',async()=>{
    const {fixture,component,service}=await setup();
    fixture.componentRef.setInput('tabbed',true);fixture.detectChanges(); await fixture.whenStable();
    const tabs=()=>Array.from(fixture.nativeElement.querySelectorAll('[role="tab"]')) as HTMLButtonElement[];
    expect(tabs().map(tab=>tab.textContent!.trim())).toEqual(['Hedefler','Denemeler','Konu analizi','Çalışma raporu']);
    component.fromDate='2026-10-02';component.review(component.report()!.recovery[0]);component.approved=true;
    tabs()[2].click();fixture.detectChanges(); await fixture.whenStable();
    const blocks=await fixture.getDeferBlocks();await blocks[0].render(DeferBlockState.Complete);fixture.detectChanges(); await fixture.whenStable();
    const child=fixture.debugElement.query(By.directive(PracticeAdaptationComponent)).componentInstance as PracticeAdaptationComponent;
    child.dirty=true;
    const confirm=vi.spyOn(TestBed.inject(ToasterService), 'confirm');const reads=service.get.mock.calls.length;
    tabs()[3].click();fixture.detectChanges(); await fixture.whenStable();tabs()[2].click();fixture.detectChanges(); await fixture.whenStable();
    expect(fixture.debugElement.query(By.directive(PracticeAdaptationComponent)).componentInstance).toBe(child);
    expect(child.dirty).toBe(true);expect(component.fromDate).toBe('2026-10-02');expect(component.approved).toBe(true);
    expect(component.selected()).not.toBeNull();expect(service.get).toHaveBeenCalledTimes(reads);expect(confirm).not.toHaveBeenCalled();
    expect(tabs()[2].getAttribute('aria-selected')).toBe('true');
    tabs()[2].dispatchEvent(new KeyboardEvent('keydown',{key:'ArrowRight',bubbles:true}));fixture.detectChanges(); await fixture.whenStable();
    expect(tabs()[3].getAttribute('aria-selected')).toBe('true');
    expect(document.activeElement).toBe(tabs()[3]);
  });
  it('offers the teacher plan as a separate tab and retains it after report failure',async()=>{
    const {fixture,component,service}=await setup();fixture.componentRef.setInput('tabbed',true);
    fixture.componentRef.setInput('showPlanTab',true);fixture.detectChanges(); await fixture.whenStable();
    service.get.mockReturnValueOnce(throwError(()=>({status:500})));await component.load();fixture.detectChanges(); await fixture.whenStable();
    const tabs=Array.from(fixture.nativeElement.querySelectorAll('[role="tab"]')) as HTMLButtonElement[];
    expect(tabs.map(tab=>tab.textContent!.trim())).toContain('Çalışma planı');
    tabs[0].click();fixture.detectChanges(); await fixture.whenStable();expect(tabs[0].getAttribute('aria-selected')).toBe('true');
    expect(fixture.nativeElement.querySelector('[role="alert"]')).toBeTruthy();
  });
  it('loads the deferred analysis in the same teacher scope and protects unsaved analysis during report refresh',async()=>{
    const {component,fixture,service}=await setup();fixture.componentRef.setInput('staffStudentId','assigned');fixture.detectChanges(); await fixture.whenStable();
    const blocks=await fixture.getDeferBlocks();await blocks[0].render(DeferBlockState.Complete);fixture.detectChanges(); await fixture.whenStable();
    const child=fixture.debugElement.query(By.directive(PracticeAdaptationComponent)).componentInstance as PracticeAdaptationComponent;
    expect(child.staffStudentId).toBe('assigned');child.dirty=true;
    const count=service.get.mock.calls.length;await component.load();expect(service.get).toHaveBeenCalledTimes(count);
    expect(component.error()).toContain('kaydet veya kapat');
  });
  it('loads on first render and keeps scope, source and unknown placement year explicit',async()=>{
    const {fixture,service}=await setup();expect(service.get).toHaveBeenCalledWith(null,'','');
    expect(fixture.nativeElement.textContent).toContain('Öğrenci beyanı');expect(fixture.nativeElement.textContent).toContain('TYT');
    expect(fixture.nativeElement.textContent).not.toContain('Puan yılı: Belirtilmemiş');expect(fixture.nativeElement.textContent).toContain('Kaynak taban puanı: 350');expect(fixture.nativeElement.textContent).toContain('hedefe kalan 15.00 net');
  });
  it('requires review and explicit confirmation, writes once and refreshes proposals',async()=>{
    const {component,plans,service}=await setup();const pending=new Subject<any>();plans.move.mockReturnValueOnce(pending);
    component.review(component.report()!.recovery[0]);component.apply();expect(plans.move).not.toHaveBeenCalled();
    component.approved=true;component.apply();component.apply();expect(plans.move).toHaveBeenCalledTimes(1);
    expect(plans.move).toHaveBeenCalledWith('plan',3,['task'],'2026-10-21',1110,null);expect(await component.canLeavePage()).toBe(false);
    pending.next({});pending.complete();expect(service.get).toHaveBeenCalledTimes(2);expect(component.selected()).toBeNull();expect(await component.canLeavePage()).toBe(true);
  });
  it('rejects a blind retry after conflict and respects teacher scope and read-only mode',async()=>{
    const {component,fixture,plans,service}=await setup();fixture.componentRef.setInput('staffStudentId','student-2');fixture.detectChanges(); await fixture.whenStable();
    expect(service.get).toHaveBeenLastCalledWith('student-2','','');plans.move.mockReturnValueOnce(throwError(()=>({status:409})));
    component.review(component.report()!.recovery[0]);component.approved=true;component.apply();expect(component.stale()).toBe(true);
    component.approved=true;component.apply();expect(plans.move).toHaveBeenCalledTimes(1);
    component.readOnly=true;component.stale.set(false);component.apply();expect(plans.move).toHaveBeenCalledTimes(1);
  });
  it('cancels stale reads and hides old report data after a failed refresh',async()=>{
    const {component,fixture,service}=await setup();const pending=new Subject<PracticeProgress>();service.get.mockReturnValueOnce(pending);
    await component.load();fixture.componentRef.setInput('staffStudentId','new');fixture.detectChanges(); await fixture.whenStable();expect(pending.observed).toBe(false);
    service.get.mockReturnValueOnce(throwError(()=>({status:500})));await component.load();expect(component.report()).toBeNull();expect(component.error()).toBeTruthy();
  });
});

beforeEach(() => { TestBed.overrideProvider(ToasterService, {useValue:{confirm:vi.fn().mockResolvedValue(true),success:vi.fn(),error:vi.fn(),info:vi.fn(),warning:vi.fn()}}); });
