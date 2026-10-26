import { ToasterService } from '../../../core/services/toaster.service';
import { TestBed } from '@angular/core/testing';
import { of, Subject, throwError } from 'rxjs';
import { CoachingWeeklyReviewService, DecisionStudyPreview, WeeklyDecision } from '../../../core/services/coaching-weekly-review.service';
import { WeeklyDecisionWorkComponent } from './weekly-decision-work.component';

const decision:WeeklyDecision={id:'decision',version:3,reviewWeekStart:'2026-10-12',title:'Paragraf tekrarı',note:'Çözümleri incele',dueOn:'2026-10-23',status:'Planned',completedAt:null,isOverdue:false,canEdit:true,history:[]};
const context={items:[],totalCount:0,pageNumber:1,plan:{id:'plan',version:7,startDate:'2026-10-01',endDate:'2027-06-01',timeZoneId:'Europe/Istanbul'}};
async function setup(){
  const api={workOptions:vi.fn(()=>of(context)),linkWork:vi.fn(()=>of(decision)),previewWork:vi.fn(),applyWork:vi.fn(()=>of(decision)),createWorkAssignment:vi.fn(()=>of(decision))};
  TestBed.configureTestingModule({imports:[WeeklyDecisionWorkComponent],providers:[{provide:CoachingWeeklyReviewService,useValue:api}]});
  const fixture=TestBed.createComponent(WeeklyDecisionWorkComponent);fixture.componentRef.setInput('studentId','student');fixture.componentRef.setInput('decision',structuredClone(decision));fixture.detectChanges(); await fixture.whenStable();
  return{fixture,component:fixture.componentInstance,api};
}
describe('WeeklyDecisionWorkComponent',()=>{
  afterEach(()=>vi.restoreAllMocks());
  it('selects both existing kinds without changing a calendar and only saves explicit links',async()=>{
    const{component,api}=await setup();component.select('task');component.kind='assignment';component.changeKind();component.select('assignment');
    expect(api.linkWork).not.toHaveBeenCalled();expect(api.previewWork).not.toHaveBeenCalled();
    component.link();expect(api.linkWork).toHaveBeenCalledWith('student',decision,'task','assignment');
    expect(api.workOptions).toHaveBeenLastCalledWith('student',decision,'assignment','',1);
  });
  it('requires a preview and confirmation and invalidates approval after any calendar input changes',async()=>{
    const{component,api}=await setup();await component.chooseMode('study');component.date='2026-10-22';
    api.previewWork.mockImplementation((_student,_row,proposal)=>of({proposal,fingerprint:'approved',title:'Paragraf',timeZoneId:'Europe/Istanbul',previousDate:null,previousStartMinute:null,otherWorkCount:1,dayMinutes:60,untimedWorkCount:0}));
    component.prepare();expect(api.previewWork).toHaveBeenCalledWith('student',decision,expect.objectContaining({expectedPlanVersion:7,expectedDecisionVersion:3,date:'2026-10-22',startMinute:1080,minutes:30}));
    component.apply();expect(api.applyWork).not.toHaveBeenCalled();component.confirmed=true;component.time='19:00';component.changed();expect(component.preview()).toBeNull();component.apply();expect(api.applyWork).not.toHaveBeenCalled();
    component.prepare();component.confirmed=true;component.apply();expect(api.applyWork).toHaveBeenCalledWith('student',decision,expect.objectContaining({fingerprint:'approved'}),expect.any(String));
  });
  it('preserves preview input after collision and blocks stale approval after a conflict',async()=>{
    const{component,api}=await setup();await component.chooseMode('study');component.date='2026-10-22';component.time='18:30';
    api.previewWork.mockReturnValueOnce(throwError(()=>({status:400,error:{message:'Çakışma var'}})));component.prepare();expect(component.date).toBe('2026-10-22');expect(component.error()).toBe('Çakışma var');
    api.previewWork.mockReturnValueOnce(throwError(()=>({status:409})));component.prepare();expect(component.stale()).toBe(true);component.prepare();component.apply();expect(api.previewWork).toHaveBeenCalledTimes(2);expect(api.applyWork).not.toHaveBeenCalled();
  });
  it('reuses the same assignment request identity after a transient failure and blocks duplicate pending writes',async()=>{
    const{component,api}=await setup();await component.chooseMode('assignment');component.dueAt='2026-10-23T18:00';component.changed();
    api.createWorkAssignment.mockReturnValueOnce(throwError(()=>({status:500})));component.createAssignment();const first=api.createWorkAssignment.mock.calls[0];
    const pending=new Subject<WeeklyDecision>();api.createWorkAssignment.mockReturnValue(pending);component.createAssignment();component.createAssignment();
    expect(api.createWorkAssignment).toHaveBeenCalledTimes(2);
    expect(api.createWorkAssignment.mock.calls[1]).toEqual(first);expect(await component.canLeavePage()).toBe(false);
    const saved=vi.fn();component.saved.subscribe(saved);pending.next(decision);pending.complete();expect(saved).toHaveBeenCalledTimes(1);expect(component.dirty).toBe(false);
  });
  it('guards dirty work and cancels stale reads when a different student or decision is selected',async()=>{
    const{component,fixture,api}=await setup();component.select('task');vi.spyOn(TestBed.inject(ToasterService), 'confirm').mockResolvedValue(false);await component.cancel();expect(await component.canLeavePage()).toBe(false);
    const pending=new Subject<typeof context>();api.workOptions.mockReturnValueOnce(pending);await component.load();expect(pending.observed).toBe(true);
    fixture.componentRef.setInput('studentId','other');fixture.componentRef.setInput('decision',{...decision,id:'another'});fixture.detectChanges(); await fixture.whenStable();expect(pending.observed).toBe(false);expect(component.studyId).toBeNull();expect(component.dirty).toBe(false);
  });
  it('displays calendar impact and requires the visible confirmation checkbox',async()=>{
    const{component,fixture,api}=await setup();await component.chooseMode('study');component.date='2026-10-22';
    api.previewWork.mockImplementation((_a,_b,proposal)=>of({proposal,fingerprint:'hash',title:'Tekrar',timeZoneId:'Europe/Istanbul',previousDate:null,previousStartMinute:null,otherWorkCount:2,dayMinutes:90,untimedWorkCount:1} satisfies DecisionStudyPreview));
    component.prepare();fixture.detectChanges(); await fixture.whenStable();
    expect(fixture.nativeElement.textContent).toContain('saat çakışması kontrol edilemedi');
    const apply=[...fixture.nativeElement.querySelectorAll('button')].find((button:any)=>button.textContent.includes('Onayla ve plana uygula')) as HTMLButtonElement;expect(apply.disabled).toBe(true);
    const check=fixture.nativeElement.querySelector('input[type=checkbox]') as HTMLInputElement;check.click();fixture.detectChanges(); await fixture.whenStable();expect(apply.disabled).toBe(false);
  });
});

beforeEach(() => { TestBed.overrideProvider(ToasterService, {useValue:{confirm:vi.fn().mockResolvedValue(true),success:vi.fn(),error:vi.fn(),info:vi.fn(),warning:vi.fn()}}); });
