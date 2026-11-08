import { ToasterService } from '../../../core/services/toaster.service';
import {TestBed} from '@angular/core/testing';
import {provideRouter,Router} from '@angular/router';
import {of,Subject,throwError} from 'rxjs';
import {CoachingWeeklyOutcomePlanService} from '../../../core/services/coaching-weekly-outcome-plan.service';
import {CoachingWeeklyDraftService} from '../../../core/services/coaching-weekly-draft.service';
import {ToasterService as Toaster} from '../../../core/services/toaster.service';
import {StudentWeeklyOutcomePlanComponent} from './student-weekly-outcome-plan.component';

describe('weekly outcome plan preview',()=>{
  const request={startDate:'2027-05-24',endDate:'2027-06-01',expectedWeeklyDraftVersion:2,reuseFreedSlots:true,engineVersion:2,prioritizeWhenShort:false};
  const short={request,timeZoneId:'Europe/Istanbul',fingerprint:'abc',canCreate:false,protectedTaskCount:0,issues:[],schedule:{tasks:[],freeMinutes:0,recommendedEndDate:'2027-06-07',lessons:[{lessonId:'math',lessonName:'Matematik',outcomeCount:3,weeklyMinutes:30,requiredMinutes:80,scheduledMinutes:60,remainingMinutes:20,finishDate:'2027-06-07',extraWeeklyMinutes:30}]}};
  async function setup(studentId?:string){const service={options:vi.fn(()=>of({startDate:request.startDate,endDate:request.endDate,weeklyDraftVersion:2,timeZoneId:'Europe/Istanbul',exams:[]})),preview:vi.fn(()=>of(short)),create:vi.fn(()=>of({id:'new',version:0})),setExamDate:vi.fn((date:string|null)=>of({startDate:request.startDate,endDate:date?'2027-06-19':'2027-06-12',weeklyDraftVersion:2,timeZoneId:'Europe/Istanbul',exams:[],examDate:date??'2027-06-13',endDateSource:date?'Student':'Catalog',catalogExamDate:'2027-06-13'}))};const slot=(id:string,lessonId:string,lessonName:string)=>({id,lessonId,lessonName,examCode:'AYT',day:'Monday' as const,startMinute:1080,durationMinutes:60});
    const weekly={get:vi.fn((_student?:string)=>of({draft:{version:2,timeZoneId:'Europe/Istanbul',slots:[slot('a','math','Matematik'),slot('b','fiz','Fizik (AYT)'),{...slot('c','kim','Kimya (AYT)'),day:'Tuesday' as const}]},lessons:[]})),
      save:vi.fn((_v:number|null,zone:string,slots:unknown[],_student?:string)=>of({version:3,timeZoneId:zone,slots}))};
    TestBed.configureTestingModule({imports:[StudentWeeklyOutcomePlanComponent],providers:[provideRouter([]),{provide:CoachingWeeklyOutcomePlanService,useValue:service},{provide:CoachingWeeklyDraftService,useValue:weekly}]});vi.spyOn(TestBed.inject(Router),'navigate').mockResolvedValue(true);const fixture=TestBed.createComponent(StudentWeeklyOutcomePlanComponent);fixture.componentRef.setInput('studentId',studentId);fixture.detectChanges(); await fixture.whenStable();return{fixture,c:fixture.componentInstance,service,weekly};}
  it('shows the default June deadline and course finish detail without writing a plan',async ()=>{
    const{fixture,c,service}=await setup();expect(c.endDate).toBe('2027-06-01');c.generatePreview();fixture.detectChanges(); await fixture.whenStable();expect(fixture.nativeElement.textContent).toContain('Matematik');expect(fixture.nativeElement.textContent).toContain('07.06.2027');expect(service.create).not.toHaveBeenCalled();expect(fixture.nativeElement.textContent).toContain('30 dk/hafta');
  });
  it('keeps teacher preview and creation scoped to the selected student without student-page links',async ()=>{
    const{fixture,c,service}=await setup('student-42');const created=vi.spyOn(c.planCreated,'emit');
    expect(service.options).toHaveBeenCalledWith('student-42');
    expect(fixture.nativeElement.querySelectorAll('a[href*="/coaching-portal"]').length).toBe(0);
    service.preview.mockReturnValue(of({...short,canCreate:true}) as any);c.generatePreview();
    expect(service.preview).toHaveBeenLastCalledWith(request,'student-42');c.confirmed=true;c.save();
    expect(service.create).toHaveBeenCalledWith(expect.objectContaining({preview:request}),'student-42');
    expect(created).toHaveBeenCalledWith('new');expect(TestBed.inject(Router).navigate).not.toHaveBeenCalled();
  });
  it('extends only after choosing that option and recomputes the preview',async ()=>{
    const{c,service}=await setup();c.generatePreview();expect(c.endDate).toBe('2027-06-01');c.extendDeadline();expect(c.endDate).toBe('2027-06-07');expect(service.preview).toHaveBeenLastCalledWith(expect.objectContaining({endDate:'2027-06-07'}),undefined);
    c.useFreedSlots();expect(service.preview).toHaveBeenLastCalledWith(expect.objectContaining({reuseFreedSlots:true}),undefined);
  });
  // K4 (31.10.2026): extending the deadline never reaches the exam day.
  it('caps the extended deadline at the day before the exam',async ()=>{
    const{c,service}=await setup();
    c.options.set({...c.options()!,examDate:'2027-06-05'});
    c.generatePreview();c.extendDeadline();
    expect(c.endDate).toBe('2027-06-04');expect(service.preview).toHaveBeenLastCalledWith(expect.objectContaining({endDate:'2027-06-04'}),undefined);
    const calls=service.preview.mock.calls.length;c.extendDeadline();
    expect(service.preview.mock.calls.length).toBe(calls);expect(c.canExtendDeadline()).toBe(false);
  });
  it('shows the K4 date rules on the fields and blocks a past start or an end after the exam (T4)',async ()=>{
    const{c,fixture,service}=await setup();c.options.set({...c.options()!,examDate:'2027-06-01'});fixture.detectChanges();
    const el=fixture.nativeElement as HTMLElement;
    expect(el.querySelector('input[name=start]')?.getAttribute('min')).toBe(c.todayIso);
    expect(el.querySelector('input[name=end]')?.getAttribute('max')).toBe('2027-05-31');
    expect(el.querySelector('#date-rules')?.textContent).toContain('sınavdan bir gün önce');
    const calls=service.preview.mock.calls.length;
    c.startDate='2020-01-01';c.generatePreview();expect(c.error()).toContain('bugünden önce');
    c.startDate=request.startDate;c.endDate='2027-06-01';c.generatePreview();expect(c.error()).toContain('31.05.2027');
    expect(service.preview.mock.calls.length).toBe(calls);fixture.detectChanges();
    expect(el.querySelector('#date-rules')?.getAttribute('role')).toBe('alert');
  });
  it('requires a complete current preview and confirmation before creating a separate draft',async ()=>{
    const{c,service}=await setup();c.title='Yıllık plan';c.generatePreview();c.confirmed=true;c.save();expect(service.create).not.toHaveBeenCalled();
    service.preview.mockReturnValue(of({...short,canCreate:true}) as any);c.generatePreview();c.save();expect(service.create).not.toHaveBeenCalled();c.confirmed=true;c.save();expect(service.create).toHaveBeenCalledWith({title:'Yıllık plan',preview:request,fingerprint:'abc'},undefined);
    expect(TestBed.inject(Router).url).toBeDefined();
  });
  it('invalidates the preview when dates change and retains it read-only after conflict',async ()=>{
    const{c,service}=await setup();service.preview.mockReturnValue(of({...short,canCreate:true}) as any);c.generatePreview();c.confirmed=true;service.create.mockReturnValue(throwError(()=>({status:409,error:{message:'Kaynak değişti'}})));c.save();expect(c.stale()).toBe(true);expect(c.error()).toBe('Kaynak değişti');expect(c.preview()).not.toBeNull();c.changed();expect(c.preview()).toBeNull();expect(c.confirmed).toBe(false);
  });
  // Paket G #41: a chosen course with no weekly lesson gets no time at all; the "sığmayan dersler" part names it too.
  it('names the chosen courses that have no weekly lesson next to the courses that do not fit',async ()=>{
    const{fixture,c,service}=await setup();const fits={...short,canCreate:true,schedule:{...short.schedule,lessons:[{...short.schedule.lessons[0],remainingMinutes:0,scheduledMinutes:80}]}};
    service.preview.mockReturnValue(of({...fits,unscheduledLessons:[{lessonId:'tur',lessonName:'Türkçe (TYT)'},{lessonId:'fel',lessonName:'Felsefe (TYT)'}]}) as any);
    c.generatePreview();fixture.detectChanges();const el=fixture.nativeElement as HTMLElement;
    const part=el.querySelector('[data-testid="unscheduled-lessons"]');
    expect(part?.textContent?.replace(/\s+/g,' ')).toContain('Türkçe (TYT), Felsefe (TYT) için haftalık düzeninde ders saati yok');
    // Review of PR 126: only courses without a weekly lesson: their own eyebrow, heading and action.
    expect(el.textContent).toContain('HAFTALIK DÜZENDE SAATİ OLMAYAN DERSLER');expect(el.textContent).not.toContain('HEDEF TARİHİNE SIĞMAYAN');
    expect(el.querySelector('.decisions h2')?.textContent).toBe('Bu dersler yıllık plana girmiyor');
    expect(el.querySelector('.decisions a[href="/coaching-portal/plan/weekly"]')?.textContent).toContain('Haftalık takvimime saat ekle');
    expect(el.querySelector('[data-testid="prioritize-short"]')).toBeNull();
    service.preview.mockReturnValue(of(fits) as any);c.generatePreview();fixture.detectChanges();
    expect(el.querySelector('[data-testid="unscheduled-lessons"]')).toBeNull();expect(el.textContent).not.toContain('SIĞMAYAN DERSLER');
  });
  it('uses the teacher wording for courses without a weekly lesson',async ()=>{
    const{fixture,c,service}=await setup('student-42');
    service.preview.mockReturnValue(of({...short,unscheduledLessons:[{lessonId:'tur',lessonName:'Türkçe'}]}) as any);c.generatePreview();fixture.detectChanges();
    const el=fixture.nativeElement as HTMLElement;
    expect(el.querySelector('[data-testid="unscheduled-lessons"]')?.textContent).toContain('Türkçe için öğrencinin haftalık düzeninde ders saati yok');
    // Both reasons: the courses that do not fit keep the first eyebrow.
    expect(el.textContent).toContain('HEDEF TARİHİNE SIĞMAYAN DERSLER VAR');
  });
  it('includes preserved pinned work in preview totals',async ()=>{
    const{c,service}=await setup();service.preview.mockReturnValue(of({...short,protectedTaskCount:1,protectedTasks:[{date:'2027-05-24',startMinute:1080,title:'Sabit görev',minutes:15}]}) as any);c.generatePreview();expect(c.totalMinutes()).toBe(15);
  });
  it('updates the default June deadline but preserves a chosen custom deadline',async ()=>{
    const{c,service}=await setup();c.startDate='2027-06-02';c.startChanged();expect(c.endDate).toBe('2028-06-01');c.endDate='2028-05-01';c.endChanged();c.startDate='2027-07-01';c.startChanged();expect(c.endDate).toBe('2028-05-01');
    c.endDate='2020-01-01';c.generatePreview();expect(service.preview).not.toHaveBeenCalled();expect(c.error()).toContain('tarih');
  });
  it('keeps custom dates across reloads and handles temporary load or preview failures',async ()=>{
    const{c,service}=await setup();c.endDate='2027-06-07';service.options.mockReturnValueOnce(throwError(()=>({status:503})));await c.load();expect(c.error()).toContain('yüklenemedi');expect(c.endDate).toBe('2027-06-07');await c.load();expect(c.error()).toBe('');
    service.preview.mockReturnValueOnce(throwError(()=>({status:503})));c.generatePreview();expect(c.error()).toContain('hazırlanamadı');expect(c.generating()).toBe(false);
  });
  it('protects an in-flight save from repeated clicks and leaving, then allows retry',async ()=>{
    const{c,service}=await setup();service.preview.mockReturnValue(of({...short,canCreate:true}) as any);c.generatePreview();c.confirmed=true;c.title=' ';c.save();expect(service.create).not.toHaveBeenCalled();expect(c.error()).toContain('başlık');
    c.title='Plan';const response=new Subject<{id:string;version:number}>();service.create.mockReturnValue(response as any);c.save();c.save();expect(service.create).toHaveBeenCalledTimes(1);expect(await c.canLeavePage()).toBe(false);const event=new Event('beforeunload',{cancelable:true});window.dispatchEvent(event);expect(event.defaultPrevented).toBe(false);response.error({status:503});expect(await c.canLeavePage()).toBe(true);expect(c.preview()).not.toBeNull();expect(c.error()).toContain('kaydedilemedi');
  });
  it('filters and pages dated work without changing its schedule',async ()=>{
    const{c,service}=await setup();const task={date:'2027-05-24',startMinute:1080,lessonId:'math',lessonName:'Matematik',minutes:30,reusedSlot:false,outcome:{topicName:'Konu',code:'K.1',name:'Kazanım'}};service.preview.mockReturnValue(of({...short,schedule:{...short.schedule,tasks:Array.from({length:30},()=>task)}}) as any);c.generatePreview();expect(c.visibleTasks()).toHaveLength(25);c.page.set(2);expect(c.visibleTasks()).toHaveLength(5);c.filter('other');expect(c.page()).toBe(1);expect(c.visibleTasks()).toHaveLength(0);c.filter('math');expect(c.totalMinutes()).toBe(900);
  });
  it('uses engine version 2 and shows each task kind, reason and question target (roadmap O7)',async ()=>{
    const{fixture,c,service}=await setup();const task={date:'2027-05-24',startMinute:1080,lessonId:'math',lessonName:'Matematik',minutes:30,reusedSlot:false,kind:'Spaced',reason:'Aralıklı tekrar: konuyu bitirdikten 7 gün sonra',plannedQuestions:14,outcome:{topicName:'Konu',code:'K.1',name:'Kazanım'}};
    service.preview.mockReturnValue(of({...short,canCreate:true,schedule:{...short.schedule,tasks:[task]},leftOutTopics:[{lessonId:'math',lessonName:'Matematik',topicId:'t2',topicName:'Olasılık',minutes:45}]}) as any);
    c.generatePreview();fixture.detectChanges();
    expect(service.preview).toHaveBeenLastCalledWith(expect.objectContaining({engineVersion:2}),undefined);
    const text=fixture.nativeElement.textContent as string;
    expect(text).toContain('Aralıklı tekrar: konuyu bitirdikten 7 gün sonra');expect(text).toContain('~14 soru');
    expect(fixture.nativeElement.querySelector('[data-testid="left-out"]').textContent).toContain('Olasılık');
  });
  it('offers to plan the important topics first when time is short',async ()=>{
    const{fixture,c,service}=await setup();c.generatePreview();fixture.detectChanges();
    (fixture.nativeElement.querySelector('[data-testid="prioritize-short"]') as HTMLButtonElement).click();
    expect(c.prioritizeWhenShort).toBe(true);
    expect(service.preview).toHaveBeenLastCalledWith(expect.objectContaining({prioritizeWhenShort:true}),undefined);
  });
  it('lets the student set her own exam date, which moves the default end, and return to the official date',async ()=>{
    const{fixture,c,service}=await setup();
    c.saveExamDate('2027-06-20');fixture.detectChanges();
    expect(service.setExamDate).toHaveBeenCalledWith('2027-06-20');expect(c.endDate).toBe('2027-06-19');
    expect(fixture.nativeElement.querySelector('[data-testid="exam-date-note"]').textContent).toContain('senin seçtiğin');
    c.saveExamDate(null);fixture.detectChanges();
    expect(c.endDate).toBe('2027-06-12');expect(fixture.nativeElement.textContent).toContain('resmî tarih');
    c.saveExamDate('bad');expect(service.setExamDate).toHaveBeenCalledTimes(2);expect(c.error()).toContain('tarih');
  });
  // G2 (#40): a year-only target with no catalog date yet: the plan ends before an estimated date, said as such.
  it('says when the exam date is an estimate',async ()=>{
    const{fixture,c,service}=await setup();
    service.setExamDate.mockImplementation((date:string|null)=>of({startDate:request.startDate,endDate:date?'2028-06-19':'2028-06-16',weeklyDraftVersion:2,timeZoneId:'Europe/Istanbul',exams:[],
      examDate:date??'2028-06-17',endDateSource:date?'Student':'Estimated',catalogExamDate:'2028-06-17',catalogExamDateEstimated:true}) as never);
    c.saveExamDate(null);fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[data-testid="exam-date-note"]').textContent).toContain('(tahmini; resmî tarih henüz açıklanmadı)');
    // G2 review (M2): the estimate sets the default end only; a later end is no date issue and the field has no maximum.
    expect(c.lastDay()).toBeNull();c.endDate='2028-07-01';expect(c.dateIssue()).toBeNull();
    expect(fixture.nativeElement.querySelector('#date-rules').textContent).toContain('tahmini olduğundan bitişi sınırlamaz');
    c.saveExamDate('2028-06-20');fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Tahmini tarihe dön (17.06.2028)');
  });
  // Paket H1 (08.11.2026): the freed hours go to the remaining lessons unless the student turns it off.
  it('uses the freed hours by default and keeps the choice to turn it off',async ()=>{
    const{fixture,c,service}=await setup();fixture.detectChanges();
    const box=fixture.nativeElement.querySelector('input[name="reuse"]') as HTMLInputElement;await fixture.whenStable();
    expect(c.reuseFreedSlots).toBe(true);expect(box.checked).toBe(true);
    c.generatePreview();expect(service.preview).toHaveBeenLastCalledWith(expect.objectContaining({reuseFreedSlots:true}),undefined);
    fixture.detectChanges();expect(fixture.nativeElement.textContent).not.toContain('Erken biten derslerin saatlerini kullan');
    box.click();fixture.detectChanges();await fixture.whenStable();expect(c.reuseFreedSlots).toBe(false);
    c.generatePreview();expect(service.preview).toHaveBeenLastCalledWith(expect.objectContaining({reuseFreedSlots:false}),undefined);
  });
  // Paket H1 (P10): the server names the lessons no longer chosen; the student gets the way to her weekly routine.
  // Review of PR 129: matched by the code (not the text), and it is not a stale preview.
  const notChosen={status:409,error:{code:'StudyPlanning.LessonsNotChosen',message:'Haftalık düzeninde artık seçili olmayan dersler var: Fizik (AYT), Kimya (AYT). Bu dersleri haftalık düzeninden kaldırıp yeniden önizle.',
    lessons:[{lessonId:'fiz',lessonName:'Fizik (AYT)'},{lessonId:'kim',lessonName:'Kimya (AYT)'}]}};
  it('offers the weekly routine and one-click removal when lessons no longer chosen stop the preview',async ()=>{
    const{fixture,c,service}=await setup();service.preview.mockReturnValue(throwError(()=>notChosen) as any);
    c.generatePreview();fixture.detectChanges();
    const el=fixture.nativeElement as HTMLElement;const alert=el.querySelector('[role="alert"]') as HTMLElement;
    expect(alert.textContent).toContain(notChosen.error.message);
    expect((alert.querySelector('a[href="/coaching-portal/plan/weekly"]') as HTMLAnchorElement).textContent?.trim()).toBe('Haftalık düzenini aç');
    expect(Array.from(alert.querySelectorAll('button')).map(b=>b.textContent?.trim())).toContain('Bu dersleri haftalık düzenimden kaldır');
    expect(c.stale()).toBe(false);expect(el.textContent).not.toContain('Kaynak bilgiler değişti');
    // The same text without the code is any other conflict: no link, no button.
    service.preview.mockReturnValue(throwError(()=>({status:409,error:{message:notChosen.error.message}})) as any);
    c.generatePreview();fixture.detectChanges();
    expect(el.querySelector('[role="alert"] a')).toBeNull();expect(el.querySelector('[role="alert"] button')).toBeNull();
  });
  it('removes the lessons no longer chosen from the weekly routine after confirming and previews again',async ()=>{
    const{fixture,c,service,weekly}=await setup();service.preview.mockReturnValueOnce(throwError(()=>notChosen) as any);
    c.generatePreview();fixture.detectChanges();
    const confirm=TestBed.inject(Toaster).confirm as ReturnType<typeof vi.fn>;
    (Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('[role="alert"] button')) as HTMLButtonElement[]).find(b=>b.textContent?.trim()==='Bu dersleri haftalık düzenimden kaldır')!.click();
    await fixture.whenStable();await new Promise(r=>setTimeout(r));fixture.detectChanges();
    expect(confirm).toHaveBeenCalledWith('Fizik (AYT) ve Kimya (AYT) dersleri haftalık düzeninden kaldırılacak. Devam edelim mi?',expect.anything());
    expect(weekly.get).toHaveBeenCalledWith();
    expect(weekly.save).toHaveBeenCalledWith(2,'Europe/Istanbul',[expect.objectContaining({id:'a',lessonId:'math'})]);
    expect(service.preview).toHaveBeenLastCalledWith(expect.objectContaining({expectedWeeklyDraftVersion:3}),undefined);
    expect(c.error()).toBe('');expect(c.preview()).not.toBeNull();
  });
  it('keeps the routine when the removal is not confirmed, and shows the reload notice on a save conflict',async ()=>{
    const{fixture,c,service,weekly}=await setup();service.preview.mockReturnValue(throwError(()=>notChosen) as any);
    const confirm=TestBed.inject(Toaster).confirm as ReturnType<typeof vi.fn>;
    c.generatePreview();confirm.mockResolvedValueOnce(false);await c.removeNotChosenLessons();
    expect(weekly.get).not.toHaveBeenCalled();
    weekly.save.mockReturnValueOnce(throwError(()=>({status:409,error:{message:'Haftalık taslak başka bir işlemde değişti.'}})) as any);
    await c.removeNotChosenLessons();fixture.detectChanges();
    expect(c.stale()).toBe(true);expect(c.error()).toBe('Haftalık taslak başka bir işlemde değişti.');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Kaynak bilgiler değişti');
  });
  it('shows a teacher only the message about lessons no longer chosen',async ()=>{
    const message='Öğrencinin haftalık düzeninde artık seçili olmayan dersler var: Fizik (AYT). Bu derslerin haftalık düzenden kaldırılması gerekiyor.';
    const{fixture,c,service}=await setup('student-42');service.preview.mockReturnValue(throwError(()=>({...notChosen,error:{...notChosen.error,message}})) as any);
    c.generatePreview();fixture.detectChanges();
    const alert=fixture.nativeElement.querySelector('[role="alert"]') as HTMLElement;
    expect(alert.textContent).toContain(message);expect(alert.querySelector('a')).toBeNull();
    expect(Array.from(alert.querySelectorAll('button')).map(b=>b.textContent?.trim())).toEqual(['Bu dersleri öğrencinin haftalık düzeninden kaldır']);
    expect(c.stale()).toBe(false);
  });
  // Final review of PR 130 (6): the teacher removes the lessons from the student's weekly routine through the teacher endpoints.
  it('lets a teacher remove the lessons no longer chosen from the student\'s weekly routine',async ()=>{
    const{fixture,c,service,weekly}=await setup('student-42');service.preview.mockReturnValueOnce(throwError(()=>notChosen) as any);
    c.generatePreview();fixture.detectChanges();
    const confirm=TestBed.inject(Toaster).confirm as ReturnType<typeof vi.fn>;
    (Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('[role="alert"] button')) as HTMLButtonElement[]).find(b=>b.textContent?.trim()==='Bu dersleri öğrencinin haftalık düzeninden kaldır')!.click();
    await fixture.whenStable();await new Promise(r=>setTimeout(r));fixture.detectChanges();
    expect(confirm).toHaveBeenCalledWith('Fizik (AYT) ve Kimya (AYT) dersleri öğrencinin haftalık düzeninden kaldırılacak. Devam edilsin mi?',expect.anything());
    expect(weekly.get).toHaveBeenCalledWith('student-42');
    expect(weekly.save).toHaveBeenCalledWith(2,'Europe/Istanbul',[expect.objectContaining({id:'a',lessonId:'math'})],'student-42');
    expect(service.preview).toHaveBeenLastCalledWith(expect.objectContaining({expectedWeeklyDraftVersion:3}),'student-42');
    expect(c.error()).toBe('');
  });
  it('keeps the exam date control off the teacher view',async ()=>{
    const{fixture,c,service}=await setup('student-42');
    expect(fixture.nativeElement.querySelector('[data-testid="exam-date-input"]')).toBeNull();
    c.saveExamDate('2027-06-20');expect(service.setExamDate).not.toHaveBeenCalled();
  });
});

beforeEach(() => { TestBed.overrideProvider(ToasterService, {useValue:{confirm:vi.fn().mockResolvedValue(true),success:vi.fn(),error:vi.fn(),info:vi.fn(),warning:vi.fn()}}); });
