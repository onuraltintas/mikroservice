import { ToasterService } from '../../../core/services/toaster.service';
import {TestBed} from '@angular/core/testing';
import {provideRouter,Router} from '@angular/router';
import {of,Subject,throwError} from 'rxjs';
import {CoachingWeeklyOutcomePlanService} from '../../../core/services/coaching-weekly-outcome-plan.service';
import {StudentWeeklyOutcomePlanComponent} from './student-weekly-outcome-plan.component';

describe('weekly outcome plan preview',()=>{
  const request={startDate:'2027-05-24',endDate:'2027-06-01',expectedWeeklyDraftVersion:2,reuseFreedSlots:false,engineVersion:2,prioritizeWhenShort:false};
  const short={request,timeZoneId:'Europe/Istanbul',fingerprint:'abc',canCreate:false,protectedTaskCount:0,issues:[],schedule:{tasks:[],freeMinutes:0,recommendedEndDate:'2027-06-07',lessons:[{lessonId:'math',lessonName:'Matematik',outcomeCount:3,weeklyMinutes:30,requiredMinutes:80,scheduledMinutes:60,remainingMinutes:20,finishDate:'2027-06-07',extraWeeklyMinutes:30}]}};
  async function setup(studentId?:string){const service={options:vi.fn(()=>of({startDate:request.startDate,endDate:request.endDate,weeklyDraftVersion:2,timeZoneId:'Europe/Istanbul',exams:[]})),preview:vi.fn(()=>of(short)),create:vi.fn(()=>of({id:'new',version:0})),setExamDate:vi.fn((date:string|null)=>of({startDate:request.startDate,endDate:date?'2027-06-19':'2027-06-12',weeklyDraftVersion:2,timeZoneId:'Europe/Istanbul',exams:[],examDate:date??'2027-06-13',endDateSource:date?'Student':'Catalog',catalogExamDate:'2027-06-13'}))};TestBed.configureTestingModule({imports:[StudentWeeklyOutcomePlanComponent],providers:[provideRouter([]),{provide:CoachingWeeklyOutcomePlanService,useValue:service}]});vi.spyOn(TestBed.inject(Router),'navigate').mockResolvedValue(true);const fixture=TestBed.createComponent(StudentWeeklyOutcomePlanComponent);fixture.componentRef.setInput('studentId',studentId);fixture.detectChanges(); await fixture.whenStable();return{fixture,c:fixture.componentInstance,service};}
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
  it('requires a complete current preview and confirmation before creating a separate draft',async ()=>{
    const{c,service}=await setup();c.title='Yıllık plan';c.generatePreview();c.confirmed=true;c.save();expect(service.create).not.toHaveBeenCalled();
    service.preview.mockReturnValue(of({...short,canCreate:true}) as any);c.generatePreview();c.save();expect(service.create).not.toHaveBeenCalled();c.confirmed=true;c.save();expect(service.create).toHaveBeenCalledWith({title:'Yıllık plan',preview:request,fingerprint:'abc'},undefined);
    expect(TestBed.inject(Router).url).toBeDefined();
  });
  it('invalidates the preview when dates change and retains it read-only after conflict',async ()=>{
    const{c,service}=await setup();service.preview.mockReturnValue(of({...short,canCreate:true}) as any);c.generatePreview();c.confirmed=true;service.create.mockReturnValue(throwError(()=>({status:409,error:{message:'Kaynak değişti'}})));c.save();expect(c.stale()).toBe(true);expect(c.error()).toBe('Kaynak değişti');expect(c.preview()).not.toBeNull();c.changed();expect(c.preview()).toBeNull();expect(c.confirmed).toBe(false);
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
  it('keeps the exam date control off the teacher view',async ()=>{
    const{fixture,c,service}=await setup('student-42');
    expect(fixture.nativeElement.querySelector('[data-testid="exam-date-input"]')).toBeNull();
    c.saveExamDate('2027-06-20');expect(service.setExamDate).not.toHaveBeenCalled();
  });
});

beforeEach(() => { TestBed.overrideProvider(ToasterService, {useValue:{confirm:vi.fn().mockResolvedValue(true),success:vi.fn(),error:vi.fn(),info:vi.fn(),warning:vi.fn()}}); });
