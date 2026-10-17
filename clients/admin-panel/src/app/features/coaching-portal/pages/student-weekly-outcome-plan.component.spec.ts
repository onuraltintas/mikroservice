import {TestBed} from '@angular/core/testing';
import {provideRouter,Router} from '@angular/router';
import {of,Subject,throwError} from 'rxjs';
import {CoachingWeeklyOutcomePlanService} from '../../../core/services/coaching-weekly-outcome-plan.service';
import {StudentWeeklyOutcomePlanComponent} from './student-weekly-outcome-plan.component';

describe('weekly outcome plan preview',()=>{
  const request={startDate:'2027-05-24',endDate:'2027-06-01',expectedWeeklyDraftVersion:2,reuseFreedSlots:false};
  const short={request,timeZoneId:'Europe/Istanbul',fingerprint:'abc',canCreate:false,protectedTaskCount:0,issues:[],schedule:{tasks:[],freeMinutes:0,recommendedEndDate:'2027-06-07',lessons:[{lessonId:'math',lessonName:'Matematik',outcomeCount:3,weeklyMinutes:30,requiredMinutes:80,scheduledMinutes:60,remainingMinutes:20,finishDate:'2027-06-07',extraWeeklyMinutes:30}]}};
  function setup(){const service={options:vi.fn(()=>of({startDate:request.startDate,endDate:request.endDate,weeklyDraftVersion:2,timeZoneId:'Europe/Istanbul',exams:[]})),preview:vi.fn(()=>of(short)),create:vi.fn(()=>of({id:'new',version:0}))};TestBed.configureTestingModule({imports:[StudentWeeklyOutcomePlanComponent],providers:[provideRouter([]),{provide:CoachingWeeklyOutcomePlanService,useValue:service}]});vi.spyOn(TestBed.inject(Router),'navigate').mockResolvedValue(true);const fixture=TestBed.createComponent(StudentWeeklyOutcomePlanComponent);fixture.detectChanges();return{fixture,c:fixture.componentInstance,service};}
  it('shows the default June deadline and course finish detail without writing a plan',()=>{
    const{fixture,c,service}=setup();expect(c.endDate).toBe('2027-06-01');c.generatePreview();fixture.detectChanges();expect(fixture.nativeElement.textContent).toContain('Matematik');expect(fixture.nativeElement.textContent).toContain('07.06.2027');expect(service.create).not.toHaveBeenCalled();expect(fixture.nativeElement.textContent).toContain('30 dk/hafta');
  });
  it('extends only after choosing that option and recomputes the preview',()=>{
    const{c,service}=setup();c.generatePreview();expect(c.endDate).toBe('2027-06-01');c.extendDeadline();expect(c.endDate).toBe('2027-06-07');expect(service.preview).toHaveBeenLastCalledWith(expect.objectContaining({endDate:'2027-06-07'}));
    c.useFreedSlots();expect(service.preview).toHaveBeenLastCalledWith(expect.objectContaining({reuseFreedSlots:true}));
  });
  it('requires a complete current preview and confirmation before creating a separate draft',()=>{
    const{c,service}=setup();c.title='Yıllık plan';c.generatePreview();c.confirmed=true;c.save();expect(service.create).not.toHaveBeenCalled();
    service.preview.mockReturnValue(of({...short,canCreate:true}) as any);c.generatePreview();c.save();expect(service.create).not.toHaveBeenCalled();c.confirmed=true;c.save();expect(service.create).toHaveBeenCalledWith({title:'Yıllık plan',preview:request,fingerprint:'abc'});
    expect(TestBed.inject(Router).url).toBeDefined();
  });
  it('invalidates the preview when dates change and retains it read-only after conflict',()=>{
    const{c,service}=setup();service.preview.mockReturnValue(of({...short,canCreate:true}) as any);c.generatePreview();c.confirmed=true;service.create.mockReturnValue(throwError(()=>({status:409,error:{message:'Kaynak değişti'}})));c.save();expect(c.stale()).toBe(true);expect(c.error()).toBe('Kaynak değişti');expect(c.preview()).not.toBeNull();c.changed();expect(c.preview()).toBeNull();expect(c.confirmed).toBe(false);
  });
  it('includes preserved pinned work in preview totals',()=>{
    const{c,service}=setup();service.preview.mockReturnValue(of({...short,protectedTaskCount:1,protectedTasks:[{date:'2027-05-24',startMinute:1080,title:'Sabit görev',minutes:15}]}) as any);c.generatePreview();expect(c.totalMinutes()).toBe(15);
  });
  it('updates the default June deadline but preserves a chosen custom deadline',()=>{
    const{c,service}=setup();c.startDate='2027-06-02';c.startChanged();expect(c.endDate).toBe('2028-06-01');c.endDate='2028-05-01';c.endChanged();c.startDate='2027-07-01';c.startChanged();expect(c.endDate).toBe('2028-05-01');
    c.endDate='2020-01-01';c.generatePreview();expect(service.preview).not.toHaveBeenCalled();expect(c.error()).toContain('tarih');
  });
  it('keeps custom dates across reloads and handles temporary load or preview failures',()=>{
    const{c,service}=setup();c.endDate='2027-06-07';service.options.mockReturnValueOnce(throwError(()=>({status:503})));c.load();expect(c.error()).toContain('yüklenemedi');expect(c.endDate).toBe('2027-06-07');c.load();expect(c.error()).toBe('');
    service.preview.mockReturnValueOnce(throwError(()=>({status:503})));c.generatePreview();expect(c.error()).toContain('hazırlanamadı');expect(c.generating()).toBe(false);
  });
  it('protects an in-flight save from repeated clicks and leaving, then allows retry',()=>{
    const{c,service}=setup();service.preview.mockReturnValue(of({...short,canCreate:true}) as any);c.generatePreview();c.confirmed=true;c.title=' ';c.save();expect(service.create).not.toHaveBeenCalled();expect(c.error()).toContain('başlık');
    c.title='Plan';const response=new Subject<{id:string;version:number}>();service.create.mockReturnValue(response as any);c.save();c.save();expect(service.create).toHaveBeenCalledTimes(1);expect(c.canLeavePage()).toBe(false);const event={preventDefault:vi.fn(),returnValue:''} as unknown as BeforeUnloadEvent;c.beforeUnload(event);expect(event.preventDefault).toHaveBeenCalled();response.error({status:503});expect(c.canLeavePage()).toBe(true);expect(c.preview()).not.toBeNull();expect(c.error()).toContain('kaydedilemedi');
  });
  it('filters and pages dated work without changing its schedule',()=>{
    const{c,service}=setup();const task={date:'2027-05-24',startMinute:1080,lessonId:'math',lessonName:'Matematik',minutes:30,reusedSlot:false,outcome:{topicName:'Konu',code:'K.1',name:'Kazanım'}};service.preview.mockReturnValue(of({...short,schedule:{...short.schedule,tasks:Array.from({length:30},()=>task)}}) as any);c.generatePreview();expect(c.visibleTasks()).toHaveLength(25);c.page.set(2);expect(c.visibleTasks()).toHaveLength(5);c.filter('other');expect(c.page()).toBe(1);expect(c.visibleTasks()).toHaveLength(0);c.filter('math');expect(c.totalMinutes()).toBe(900);
  });
});
