import {TestBed} from '@angular/core/testing';
import {provideRouter,Router} from '@angular/router';
import {of,throwError} from 'rxjs';
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
});
