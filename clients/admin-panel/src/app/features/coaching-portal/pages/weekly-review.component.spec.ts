import { ToasterService } from '../../../core/services/toaster.service';
import { TestBed } from '@angular/core/testing';
import { of, Subject, throwError } from 'rxjs';
import { provideRouter } from '@angular/router';
import { CoachingWeeklyReviewService, WeeklyDecision, WeeklyReview, WeeklyResultSnapshot } from '../../../core/services/coaching-weekly-review.service';
import { WeeklyReviewComponent, mondayOf, shiftWeek } from './weekly-review.component';

const sample: WeeklyReview = {
  weekStart: '2026-10-12', today: '2026-10-20', reflection: null,
  summary: { scheduledTasks: 4, completedTasks: 2, plannedMinutes: 120, actualMinutes: 55 },
  decisions: [], decisionPage: 1, totalDecisions: 0, canReflect: true, canDecide: false
};
const decision: WeeklyDecision = { id: 'decision', version: 2, reviewWeekStart: '2026-10-05', title: 'Eski karar', note: 'Takip', dueOn: '2026-10-13', status: 'Planned', completedAt: null, isOverdue: true, canEdit: true, history: [] };
async function setup(staff = false, view: Partial<WeeklyReview> = {}) {
  const api = { reviewReflection:vi.fn(()=>of({})), get: vi.fn(() => of({ ...structuredClone(sample), ...view, canReflect: !staff, canDecide: staff })), reflect: vi.fn(() => of({ version: 1 })), decide: vi.fn(() => of(decision)), postpone: vi.fn(() => of(decision)), changeDeadline:vi.fn(()=>of(decision)) };
  TestBed.configureTestingModule({ imports: [WeeklyReviewComponent], providers: [provideRouter([]), { provide: CoachingWeeklyReviewService, useValue: api }] });
  await TestBed.compileComponents(); const fixture = TestBed.createComponent(WeeklyReviewComponent);
  if (staff) fixture.componentRef.setInput('staffStudentId', 'student');
  fixture.detectChanges(); await fixture.whenStable(); return { fixture, component: fixture.componentInstance, api };
}
const results:WeeklyResultSnapshot={fingerprint:'current-results',capturedAt:'2026-10-20T12:00:00Z',summary:sample.summary,studies:[],decisions:[{
  decisionId:'previous',version:3,sourceWeekStart:'2026-10-05',title:'Önceki haftanın kapanmış kararı',status:'Completed',dueOn:'2026-10-15',completedAt:'2026-10-16T12:00:00Z',
  linkedStudyRootId:null,relatedStudy:[],effect:null,effectNote:null,work:{study:null,assignment:{id:'assignment',title:'Kontrol ödevi',status:'Graded',isDone:true,isOverdue:false,canOpen:true,score:95,feedback:'Güncel geri bildirim'}}
}]};
describe('WeeklyReviewComponent', () => {
  afterEach(() => vi.restoreAllMocks());
  it('creates a follow-up decision with the displayed source version and previous decision and preserves them on conflict',async()=>{
    const reflection={version:4,successes:'Değerlendirme',obstacles:'',nextFocus:'Yeni yaklaşım',rating:3,savedAt:'2026-10-19T12:00:00Z',results};
    const{component,api}=await setup(true,{results,reflection});await component.newDecision('previous');
    expect(component.editor!.body).toEqual(expect.objectContaining({sourceReflectionVersion:4,previousDecisionId:'previous',expectedResultsFingerprint:'current-results'}));
    expect(component.previousDecisionOptions().map(x=>x.decisionId)).toEqual(['previous']);
    component.editor!.body.title='Yeni yaklaşım';api.decide.mockReturnValueOnce(throwError(()=>({status:409,error:{message:'Kaynak değişti'}})));component.saveDecision();
    expect(component.stale()).toBe(true);expect(component.editor?.body.previousDecisionId).toBe('previous');expect(component.editor?.body.sourceReflectionVersion).toBe(4);
    expect(api.decide).toHaveBeenCalledWith('student','2026-10-12',expect.any(String),expect.objectContaining({previousDecisionId:'previous',sourceReflectionVersion:4,expectedResultsFingerprint:'current-results'}));
  });
  it('reopens changed-result reviews, shows the old evidence and acknowledges only the displayed current results',async()=>{
    const old={...results,fingerprint:'old-results',summary:{...results.summary,actualMinutes:20}};
    const reflection={version:3,successes:'Öğrenci metni',obstacles:'',nextFocus:'Tekrar',rating:4,savedAt:'2026-10-19T10:00:00Z'};
    const{component,api,fixture}=await setup(true,{reflection,results,needsTeacherReview:true,reviewReason:'ResultsChanged',reviews:[{teacherId:'teacher',reflectionVersion:3,note:'İlk inceleme notu',reviewedAt:'2026-10-20T10:00:00Z',results:old,evidenceFingerprint:'old-evidence'}]});
    expect(fixture.nativeElement.textContent).toContain('İnceleme sonrasında sonuçlar değişti');expect(fixture.nativeElement.textContent).toContain('İlk inceleme notu');
    expect(fixture.nativeElement.textContent).toContain('Bu incelemede görülen sonuçlar');component.reviewNote='Güncel sonuçları inceledim';component.dirty=true;
    api.reviewReflection.mockReturnValueOnce(throwError(()=>({status:409,error:{message:'Sonuç tekrar değişti'}})));component.acknowledgeReflection();
    expect(api.reviewReflection).toHaveBeenCalledWith('student','2026-10-12',3,'Güncel sonuçları inceledim','current-results');expect(component.reviewNote).toBe('Güncel sonuçları inceledim');expect(component.stale()).toBe(true);
  });
  it('displays a decisions frozen origin separately from newer reflection text and current results',async()=>{
    const row={...decision,sourceReflection:{reflectionId:'reflection',reflectionVersion:2,weekStart:'2026-10-05',successes:'Karara kaynak eski başarı',obstacles:'Eski engel',nextFocus:'Korunan kaynak odağı',rating:3,results,currentResults:results},previousDecisionId:'previous',previousDecision:results.decisions[0]};
    const{fixture}=await setup(true,{decisions:[row],reflection:{version:7,successes:'Yeni değerlendirme metni',obstacles:'',nextFocus:'Yeni odak',rating:4,savedAt:'2026-10-20T10:00:00Z'}});
    expect(fixture.nativeElement.textContent).toContain('Karara kaynak eski başarı');expect(fixture.nativeElement.textContent).toContain('Yeni değerlendirme metni');
    expect(fixture.nativeElement.textContent).toContain('Karar alınırken görülen güncel sonuçlar');expect(fixture.nativeElement.textContent).toContain('Önceki karar:');
  });
  it('saves decision effects against the displayed results and keeps the inputs when evidence changes',async()=>{
    const {component,api}=await setup(false,{results});
    component.reflection={expectedVersion:null,successes:'İyi geçti',obstacles:'',nextFocus:'Tekrar',rating:4};
    component.decisionEffects['previous']={effect:'PartlyHelped',note:'az'};component.saveReflection();expect(api.reflect).not.toHaveBeenCalled();
    component.decisionEffects['previous'].note='Hız arttı, hata devam ediyor';component.dirty=true;
    api.reflect.mockReturnValueOnce(throwError(()=>({status:409,error:{message:'Sonuçlar değişti'}})));
    component.saveReflection();expect(api.reflect).toHaveBeenCalledWith('2026-10-12',expect.objectContaining({expectedResultsFingerprint:'current-results',decisionEffects:[{decisionId:'previous',effect:'PartlyHelped',note:'Hız arttı, hata devam ediyor'}]}));
    expect(component.stale()).toBe(true);expect(component.decisionEffects['previous'].note).toBe('Hız arttı, hata devam ediyor');expect(component.dirty).toBe(true);
  });
  it('shows current results separately from the saved evidence and lets the teacher use the student effect',async()=>{
    const frozen={...results,fingerprint:'saved-results',decisions:results.decisions.map(row=>({...row,effect:'PartlyHelped',effectNote:'Eski sonuç notu',work:{study:null,assignment:{...row.work!.assignment!,score:80,feedback:'Önceki geri bildirim'}}}))};
    const {fixture,component,api}=await setup(true,{results,reflection:{version:1,successes:'Başarı',obstacles:'',nextFocus:'Tekrar',rating:4,savedAt:'2026-10-19T12:00:00Z',results:frozen}});
    fixture.detectChanges();const text=fixture.nativeElement.textContent;
    expect(text).toContain('Önceki haftanın kapanmış kararı');expect(text).toContain('Değerlendirmenin kaydedildiği andaki sonuçlar');
    expect(text).toContain('Önceki geri bildirim');expect(text).toContain('Güncel geri bildirim');expect(text).toContain('Eski sonuç notu');
    component.saveReflection();expect(api.reflect).not.toHaveBeenCalled();
  });
  it('renders older saved result versions without inventing evidence for legacy reflections',async()=>{
    const {fixture}=await setup(true,{results,reflection:{version:2,successes:'Yeni metin',obstacles:'',nextFocus:'Yeni odak',rating:4,savedAt:'2026-10-20T12:00:00Z',results:null,
      history:[{version:1,savedAt:'2026-10-19T12:00:00Z',successes:'Korunan eski başarı',obstacles:'',nextFocus:'Eski odak',rating:3,results}]}});
    fixture.detectChanges();expect(fixture.nativeElement.textContent).toContain('Bu eski değerlendirmeye kayıt anında sonuç görüntüsü bağlanmamış');
    expect(fixture.nativeElement.textContent).toContain('Korunan eski başarı');expect(fixture.nativeElement.textContent).toContain('Önceki değerlendirme sürümleri');
  });
  it('acknowledges only the displayed reflection version and preserves the note on conflict',async()=>{
    const reflection={version:3,successes:'İyi',obstacles:'',nextFocus:'Tekrar',rating:4,savedAt:'2026-10-19T10:00:00Z'};
    const {component,api,fixture}=await setup(true,{reflection,needsTeacherReview:true});fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('İnceledim, kuyruğu kapat');
    expect(api.reviewReflection).not.toHaveBeenCalled();component.reviewNote='Konu tekrarını takip edeceğim';component.dirty=true;
    component.editor={id:'draft',week:'2026-10-12',body:{expectedVersion:null,title:'Taslak karar',note:'',dueOn:'2026-10-19',status:'Planned'}};
    component.acknowledgeReflection();expect(api.reviewReflection).not.toHaveBeenCalled();component.editor=null;
    api.reviewReflection.mockReturnValueOnce(throwError(()=>({status:409,error:{message:'Değerlendirme değişti'}})));
    component.acknowledgeReflection();expect(component.reviewNote).toBe('Konu tekrarını takip edeceğim');expect(component.stale()).toBe(true);
    expect(api.reviewReflection).toHaveBeenCalledWith('student','2026-10-12',3,'Konu tekrarını takip edeceğim');
  });
  it('does not acknowledge without a reflection or from a student view',async()=>{
    const {component,api}=await setup();component.reviewNote='İnceledim';component.acknowledgeReflection();expect(api.reviewReflection).not.toHaveBeenCalled();
  });
  it('creates a decision from an old review with today as default and no old-week upper bound',async()=>{
    const {component,fixture,api}=await setup(true,{weekStart:'2026-10-05',today:'2026-10-25'});
    await component.newDecision();fixture.changeDetectorRef.markForCheck();fixture.detectChanges();await fixture.whenStable();
    expect(component.editor?.week).toBe('2026-10-05');expect(component.editor?.body.dueOn).toBe('2026-10-25');
    const date=fixture.nativeElement.querySelector('input[name="dueOn"]') as HTMLInputElement;
    expect(date.min).toBe('2026-10-25');expect(date.max).toBe('');expect(date.readOnly).toBe(false);
    component.editor!.body.title='Gelecekte kontrol';component.editor!.body.dueOn='2026-10-28';component.saveDecision();
    expect(api.decide).toHaveBeenCalledWith('student','2026-10-05',expect.any(String),expect.objectContaining({expectedVersion:null,dueOn:'2026-10-28',status:'Planned'}));
    expect(api.postpone).not.toHaveBeenCalled();
  });
  it('defaults current-week decisions to next Monday and keeps future-dated existing decisions locked',async()=>{
    const {component,fixture}=await setup(true,{weekStart:'2026-10-19',today:'2026-10-25'});
    await component.newDecision();expect(component.editor?.body.dueOn).toBe('2026-10-26');component.dirty=false;
    const row={...decision,reviewWeekStart:'2026-10-05',dueOn:'2026-10-28',isOverdue:false};
    await component.editDecision(row);fixture.changeDetectorRef.markForCheck();fixture.detectChanges();await fixture.whenStable();
    expect(component.editor?.dateLocked).toBe(true);
    expect((fixture.nativeElement.querySelector('input[name="dueOn"]') as HTMLInputElement).readOnly).toBe(true);
    expect(component.editor?.body.dueOn).toBe('2026-10-28');
  });
  it('keeps title edits available while requiring the separate reasoned operation for every existing deadline',async()=>{
    const row={...decision,reviewWeekStart:'2026-10-12',dueOn:'2026-10-22',isOverdue:false};const {component,api}=await setup(true,{decisions:[row]});
    await component.editDecision(row);expect(component.editor?.dateLocked).toBe(true);
    component.editor!.body.title='Güncel başlık';component.saveDecision();expect(api.decide).toHaveBeenCalledWith('student','2026-10-12','decision',expect.objectContaining({dueOn:'2026-10-22',title:'Güncel başlık'}));
  });
  it('postpones an overdue decision with its original week, version and trimmed reason',async()=>{
    const {component,api,fixture}=await setup(true,{decisions:[decision]});
    await component.startPostponement(decision);expect(component.postponement?.dueOn).toBe('2026-10-21');
    component.postponement!.dueOn='2026-10-28';component.postponement!.reason='  Hastalık nedeniyle ek süre  ';component.dirty=true;
    component.savePostponement();fixture.detectChanges();await fixture.whenStable();
    expect(api.postpone).toHaveBeenCalledWith('student','2026-10-05','decision',{expectedVersion:2,dueOn:'2026-10-28',reason:'Hastalık nedeniyle ek süre'});
    expect(component.postponement).toBeNull();expect(component.dirty).toBe(false);expect(decision.dueOn).toBe('2026-10-13');
  });
  it('changes a future deadline earlier with a reason and the original week and keeps inputs on conflict',async()=>{
    const row={...decision,isOverdue:false,dueOn:'2026-10-30'};
    const{component,api,fixture}=await setup(true,{decisions:[row]});fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Gerekçeyle değiştir');
    await component.startPostponement(row);expect(component.postponement?.dueOn).toBe('2026-10-30');
    component.postponement!.dueOn='2026-10-24';component.postponement!.reason='  Takvim erkene alındı  ';
    api.changeDeadline.mockReturnValueOnce(throwError(()=>({status:409,error:{message:'Karar değişti'}})));
    component.savePostponement();expect(component.postponement?.reason).toBe('  Takvim erkene alındı  ');expect(component.stale()).toBe(true);
    expect(api.changeDeadline).toHaveBeenCalledWith('student','2026-10-05','decision',{expectedVersion:2,dueOn:'2026-10-24',reason:'Takvim erkene alındı'});expect(api.postpone).not.toHaveBeenCalled();
  });
  it('requires a reason and a different future date before sending deadline changes',async()=>{
    const row={...decision,isOverdue:false,dueOn:'2026-10-30'};const{component,api}=await setup(true,{decisions:[row]});await component.startPostponement(row);
    component.savePostponement();component.postponement!.reason='Geçerli gerekçe';component.savePostponement();
    component.postponement!.dueOn='2026-10-19';component.savePostponement();expect(api.changeDeadline).not.toHaveBeenCalled();
  });
  it('preserves postponement inputs after a conflict and clears them if responsibility is lost',async()=>{
    const {component,api}=await setup(true,{decisions:[decision]});await component.startPostponement(decision);
    component.postponement!.reason='Gerekçeli erteleme';component.dirty=true;
    api.postpone.mockReturnValueOnce(throwError(()=>({status:409,error:{message:'Karar değişti'}})));
    component.savePostponement();expect(component.stale()).toBe(true);expect(component.postponement?.reason).toBe('Gerekçeli erteleme');
    component.stale.set(false);api.postpone.mockReturnValueOnce(throwError(()=>({status:403,error:{message:'Sorumlu değişti'}})));
    component.savePostponement();expect(component.postponement).toBeNull();expect(component.data()).toBeNull();
  });
  it('shows first deadline, transfer and postponement history and prevents student or read-only writes',async()=>{
    const row={...decision,initialDueOn:'2026-10-12',history:[{at:'2026-10-20T12:00:00Z',title:decision.title,note:'',dueOn:'2026-10-13',status:'Planned',action:'Postponement',previousDueOn:'2026-10-12',reason:'Hastalık nedeniyle ek süre'},{at:'2026-10-20T12:00:00Z',title:decision.title,note:'',dueOn:'2026-10-13',status:'Planned',action:'ResponsibilityTransfer'}]};
    const {component,fixture,api}=await setup(false,{decisions:[row]});
    expect(fixture.nativeElement.textContent).toContain('İlk son tarih');expect(fixture.nativeElement.textContent).toContain('Hastalık nedeniyle ek süre');expect(fixture.nativeElement.textContent).toContain('yeni öğretmenin sorumluluğuna');
    await component.startPostponement(row);expect(component.postponement).toBeNull();expect(api.postpone).not.toHaveBeenCalled();
  });
  it('shows actual work status independently and gives students exact task and assignment links',async()=>{
    const row={...decision,canEdit:false,work:{study:{id:'task',title:'Tekrar çalışması',status:'Completed',isDone:true,isOverdue:false,canOpen:true,planId:'plan',actualMinutes:25},assignment:{id:'assignment',title:'Teslimim',status:'Graded',isDone:true,isOverdue:false,canOpen:true,score:90}}};
    const {fixture}=await setup(false,{decisions:[row]});fixture.detectChanges(); await fixture.whenStable();
    const html=fixture.nativeElement as HTMLElement;expect(html.textContent).toContain('Değerlendirildi');expect(html.textContent).toContain('25 dk kaydedildi');
    expect(html.querySelector('a[href*="study-plans"]')?.getAttribute('href')).toBe('/coaching-portal/study-plans?plan=plan&task=task');
    expect(html.querySelector('a[href*="assignments"]')?.getAttribute('href')).toBe('/coaching-portal/assignments/assignment');
    expect(html.textContent).not.toContain('Çalışma ve ödevleri bağla / takip et');
  });
  it('loads the exact linked week and decision page, then allows ordinary pagination',async()=>{
    const{fixture,component,api}=await setup(true,{decisions:[decision],totalDecisions:24,decisionPage:2});
    fixture.componentRef.setInput('initialWeek','2026-10-05');fixture.componentRef.setInput('decisionId','decision');fixture.detectChanges(); await fixture.whenStable();
    expect(api.get).toHaveBeenLastCalledWith('student','2026-10-05',1,'decision');expect(component.focusedDecision()).toBe('decision');
    await component.page(1);expect(api.get).toHaveBeenLastCalledWith('student','2026-10-12',1);
  });
  it('normalizes selected calendar dates to Monday and shifts over year boundaries', () => {
    expect(mondayOf('2027-01-03')).toBe('2026-12-28'); expect(shiftWeek('2026-12-28', 7)).toBe('2027-01-04');
  });
  it('drops an unresolved linked decision when the user selects a different week', async () => {
    const { fixture, component, api } = await setup(true);
    const pending = new Subject<WeeklyReview>(); api.get.mockReturnValueOnce(pending);
    fixture.componentRef.setInput('initialWeek', '2026-10-05'); fixture.componentRef.setInput('decisionId', 'decision'); fixture.detectChanges(); await fixture.whenStable();
    expect(pending.observed).toBe(true);
    await component.changeWeek('2026-09-28');
    expect(pending.observed).toBe(false); expect(api.get).toHaveBeenLastCalledWith('student', '2026-09-28', 1);
    expect(component.focusedDecision()).toBeNull();
  });
  it('saves a student reflection and reloads the week without offering teacher writes', async () => {
    const { fixture, component, api } = await setup();
    component.reflection = { expectedVersion: null, successes: 'Düzenli çalıştım', obstacles: '', nextFocus: 'Tekrar', rating: 4 }; component.dirty = true;
    component.saveReflection(); fixture.detectChanges(); await fixture.whenStable();
    expect(api.reflect).toHaveBeenCalledWith('2026-10-12', { expectedVersion: null, successes: 'Düzenli çalıştım', obstacles: '', nextFocus: 'Tekrar', rating: 4 });
    expect(api.get).toHaveBeenCalledTimes(2); expect(component.dirty).toBe(false);
    await component.newDecision(); expect(component.editor).toBeNull(); expect(fixture.nativeElement.textContent).toContain('120 dk');
  });
  it('shows teacher the reflection, creates decisions with a current deadline and edits carried decisions in their original week', async () => {
    const { fixture, component, api } = await setup(true, { reflection: { version: 1, successes: 'Başardım', obstacles: '', nextFocus: 'Tekrar', rating: 4, savedAt: '2026-10-19T10:00:00Z' }, decisions: [decision], totalDecisions: 1 });
    expect(fixture.nativeElement.querySelector('textarea[name="successes"]')).toBeNull(); expect(fixture.nativeElement.textContent).toContain('Başardım');
    await component.newDecision(); expect(component.editor!.body.dueOn).toBe('2026-10-20'); const id = component.editor!.id;
    component.editor!.body.title = 'Yeni karar'; component.saveDecision();
    expect(api.decide).toHaveBeenCalledWith('student', '2026-10-12', id, expect.objectContaining({ status: 'Planned', expectedVersion: null }));
    await component.editDecision(decision); component.editor!.body.status = 'Completed'; component.saveDecision();
    expect(api.decide).toHaveBeenLastCalledWith('student', '2026-10-05', 'decision', expect.objectContaining({ expectedVersion: 2, status: 'Completed' }));
    component.saveReflection(); expect(api.reflect).not.toHaveBeenCalled();
  });
  it('preserves a draft after conflict and blocks blind retry until refresh is explicitly confirmed', async () => {
    const { component, api } = await setup(); component.reflection.successes = 'Kaybolmasın'; component.dirty = true;
    api.reflect.mockReturnValueOnce(throwError(() => ({ status: 409 })));
    component.saveReflection(); expect(component.stale()).toBe(true); expect(component.reflection.successes).toBe('Kaybolmasın');
    component.saveReflection(); expect(api.reflect).toHaveBeenCalledTimes(1);
    const confirm = vi.spyOn(TestBed.inject(ToasterService), 'confirm').mockResolvedValue(false); await component.refresh(); expect(api.get).toHaveBeenCalledTimes(1);
    confirm.mockResolvedValue(true); await component.refresh(); expect(api.get).toHaveBeenCalledTimes(2); expect(component.stale()).toBe(false);
  });
  it('protects drafts on week/page changes, prevents leaving during writes and supports pagination', async () => {
    const { component, api } = await setup(true, { totalDecisions: 21 }); await component.newDecision();
    const confirm = vi.spyOn(TestBed.inject(ToasterService), 'confirm').mockResolvedValue(false); await component.page(2); await component.changeWeek('2026-10-05');
    expect(api.get).toHaveBeenCalledTimes(1); expect(component.week).toBe('2026-10-12');
    confirm.mockResolvedValue(true); await component.page(2); expect(api.get).toHaveBeenLastCalledWith('student', '2026-10-12', 2);
    await component.newDecision(); const pending = new Subject<any>(); api.decide.mockReturnValueOnce(pending); component.saveDecision(); component.saveDecision();
    expect(await component.canLeavePage()).toBe(false); expect(api.decide).toHaveBeenCalledTimes(1); pending.next(decision); pending.complete(); expect(component.canRefresh()).toBe(true);
  });
  it('keeps the displayed week aligned with saved data when a dirty date change is cancelled', async () => {
    const { fixture, component, api } = await setup(); component.dirty = true;
    vi.spyOn(TestBed.inject(ToasterService), 'confirm').mockResolvedValue(false);
    const input = fixture.nativeElement.querySelector('.week-picker input') as HTMLInputElement;
    input.value = '2026-10-05'; input.dispatchEvent(new Event('change', { bubbles: true })); fixture.detectChanges(); await fixture.whenStable();
    expect(input.value).toBe('2026-10-12'); expect(api.get).toHaveBeenCalledTimes(1);
  });
  it('cancels old reads and pending writes when student scope changes and respects read-only mode', async () => {
    const { component, fixture, api } = await setup(true); const pending = new Subject<WeeklyReview>(); api.get.mockReturnValueOnce(pending);
    await component.load(); fixture.componentRef.setInput('staffStudentId', 'other'); fixture.detectChanges(); await fixture.whenStable(); expect(pending.observed).toBe(false);
    expect(api.get).toHaveBeenLastCalledWith('other', '2026-10-12', 1);
    const write = new Subject<any>(); api.decide.mockReturnValueOnce(write); await component.newDecision(); component.saveDecision();
    fixture.componentRef.setInput('staffStudentId', 'third'); fixture.detectChanges(); await fixture.whenStable(); expect(write.observed).toBe(false); expect(component.editor).toBeNull();
    fixture.componentRef.setInput('readOnly', true); fixture.detectChanges(); await fixture.whenStable(); await component.newDecision(); expect(component.editor).toBeNull();
  });
  it('keeps the editor on transient errors and clears protected data after revoked authorization', async () => {
    const { component, api } = await setup(true); await component.newDecision(); const id = component.editor!.id;
    api.decide.mockReturnValueOnce(throwError(() => ({ status: 500 }))); component.saveDecision(); expect(component.editor!.id).toBe(id);
    api.decide.mockReturnValueOnce(throwError(() => ({ status: 403 }))); component.saveDecision(); expect(component.data()).toBeNull(); expect(component.editor).toBeNull();
  });
});

beforeEach(() => { TestBed.overrideProvider(ToasterService, {useValue:{confirm:vi.fn().mockResolvedValue(true),success:vi.fn(),error:vi.fn(),info:vi.fn(),warning:vi.fn()}}); });
