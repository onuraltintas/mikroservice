import {TestBed} from '@angular/core/testing';
import {provideRouter} from '@angular/router';
import {of,Subject,throwError} from 'rxjs';
import {CoachingWeeklyDraftService,WeeklyDraft} from '../../../core/services/coaching-weekly-draft.service';
import {StudentWeeklyCalendarComponent} from './student-weekly-calendar.component';

describe('weekly draft calendar',()=>{
  const lesson={id:'math',name:'Matematik',examCode:'LGS',gradeNumber:8};
  const slot={id:'one',lessonId:'math',lessonName:'Matematik',examCode:'LGS',day:'Monday' as const,startMinute:1080,durationMinutes:60};
  function setup(draft:WeeklyDraft|null=null){
    const service={get:vi.fn<CoachingWeeklyDraftService['get']>(()=>of({draft,lessons:[lesson]})),save:vi.fn<CoachingWeeklyDraftService['save']>(()=>of({version:1,timeZoneId:'Europe/Istanbul',slots:[slot]}))};
    TestBed.configureTestingModule({imports:[StudentWeeklyCalendarComponent],providers:[provideRouter([]),{provide:CoachingWeeklyDraftService,useValue:service}]});
    const fixture=TestBed.createComponent(StudentWeeklyCalendarComponent);fixture.detectChanges();return{fixture,c:fixture.componentInstance,service};
  }
  it('opens three views and a separate draft with a link to existing plans',()=>{
    const{fixture,c,service}=setup();expect(service.get).toHaveBeenCalledTimes(1);expect(c.slots()).toEqual([]);
    expect(fixture.nativeElement.querySelectorAll('.view-switch button')).toHaveLength(3);
    expect(fixture.nativeElement.querySelector('a[href="/coaching-portal/study-plans"]')).not.toBeNull();
    c.changeView('month');fixture.detectChanges();expect(fixture.nativeElement.querySelector('.month-grid')).not.toBeNull();expect(c.dirty()).toBe(false);
  });
  it('adds a half hour, keeps the other half free and rejects overlapping edits',()=>{
    const{c}=setup();c.add(new Date(2026,9,19),1080);c.form!.duration=30;c.apply();expect(c.slots()[0].durationMinutes).toBe(30);
    c.add(new Date(2026,9,19),1110);c.form!.duration=30;c.apply();expect(c.slots()).toHaveLength(2);
    c.edit(c.slots()[0]);c.form!.duration=60;c.apply();expect(c.formError).toContain('çakış');expect(c.slots()[0].durationMinutes).toBe(30);
  });
  it('preserves unsaved cards and blocks overwriting after a save conflict',()=>{
    const{c,service}=setup();c.add();c.apply();service.save.mockReturnValue(throwError(()=>({status:409,error:{message:'Taslak değişti'}})));
    c.save();expect(c.error()).toBe('Taslak değişti');expect(c.conflict()).toBe(true);expect(c.dirty()).toBe(true);expect(c.slots()).toHaveLength(1);
    c.save();expect(service.save).toHaveBeenCalledTimes(1);
  });
  it('saves with the expected version and prevents edits or leaving during the request',()=>{
    const{c,service}=setup({version:3,timeZoneId:'Europe/Istanbul',slots:[slot]});c.edit(slot);c.form!.duration=30;c.apply();
    const response=new Subject<WeeklyDraft>();service.save.mockReturnValue(response);c.save();
    expect(service.save.mock.calls[0][0]).toBe(3);expect(c.canLeavePage()).toBe(false);c.add();expect(c.form).toBeNull();
    response.next({version:4,timeZoneId:'Europe/Istanbul',slots:c.slots()});expect(c.dirty()).toBe(false);expect(c.message()).toContain('kaydedildi');
  });
  it('keeps saved work if loading fails and allows a guarded reload',()=>{
    const{c,service}=setup();c.add();c.apply();const confirm=vi.spyOn(window,'confirm').mockReturnValue(false);c.load(true);expect(service.get).toHaveBeenCalledTimes(1);
    confirm.mockReturnValue(true);service.get.mockReturnValue(throwError(()=>({status:503})));c.load(true);expect(c.slots()).toHaveLength(1);expect(c.error()).toContain('yüklenemedi');confirm.mockRestore();
  });
});
