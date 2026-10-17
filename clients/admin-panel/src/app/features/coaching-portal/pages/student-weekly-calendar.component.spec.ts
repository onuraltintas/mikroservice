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
  it('allows plan generation only from the saved nonempty weekly draft',()=>{
    const{c,fixture}=setup({version:0,timeZoneId:'Europe/Istanbul',slots:[slot]});
    const button=()=>fixture.nativeElement.querySelector('[data-testid="weekly-preview-link"]') as HTMLButtonElement;
    expect(button()).not.toBeNull();expect(button().disabled).toBe(false);c.edit(slot);c.form!.duration=30;c.apply();fixture.detectChanges();expect(button().disabled).toBe(true);c.save();fixture.detectChanges();expect(button().disabled).toBe(false);
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
  it('moves cards by half hours, preserves the end on top resizing and cancels a preview',()=>{
    const{c,fixture}=setup({version:0,timeZoneId:'Europe/Istanbul',slots:[slot]});c.anchor.set(new Date(2026,9,19));c.changeView('week');fixture.detectChanges();
    const card=fixture.nativeElement.querySelector('.slot-card') as HTMLElement;
    vi.spyOn(card,'getBoundingClientRect').mockReturnValue({top:0} as DOMRect);
    const track=fixture.nativeElement.querySelector('.day-track[data-day="Tuesday"]') as HTMLElement;
    vi.spyOn(track,'getBoundingClientRect').mockReturnValue({top:0} as DOMRect);
    Object.defineProperty(document,'elementFromPoint',{configurable:true,value:vi.fn(()=>track)});
    const event=(x:number,y:number)=>({button:0,isPrimary:true,pointerId:7,clientX:x,clientY:y,target:card,stopPropagation:vi.fn(),preventDefault:vi.fn()} as unknown as PointerEvent);
    c.begin(event(0,10),slot,'move');c.move(event(1,12));expect(c.preview()).toBeNull();c.move(event(150,1342));c.end(event(150,1342));
    expect(c.slots()[0]).toMatchObject({day:'Tuesday',startMinute:1110,durationMinutes:60});
    c.begin(event(0,10),c.slots()[0],'top');c.move(event(0,46));c.end(event(0,46));expect(c.slots()[0]).toMatchObject({startMinute:1140,durationMinutes:30});
    c.begin(event(0,10),c.slots()[0],'bottom');c.move(event(0,46));expect(c.preview()?.durationMinutes).toBe(60);c.escape();c.end(event(0,46));expect(c.slots()[0].durationMinutes).toBe(30);
    c.begin(event(0,10),c.slots()[0],'move');vi.mocked(document.elementFromPoint).mockReturnValue(null);c.move(event(60,60));c.end(event(60,60));expect(c.error()).toContain('uygun bir saate');expect(c.slots()[0].startMinute).toBe(1140);
  });
  it('validates lesson and clock input, permits midnight ending and removes unavailable lessons',()=>{
    const{c}=setup();c.add(new Date(2026,9,19),1410);expect(c.form!.duration).toBe(30);
    c.form!.lessonId='removed';c.apply();expect(c.formError).toContain('aktif bir ders');c.form!.lessonId='math';
    for(const time of ['bad','24:00','18:61']){c.form!.start=time;c.apply();expect(c.formError).toContain('başlangıç');}
    c.form!.start='23:30';c.apply();expect(c.slots()[0].startMinute).toBe(1410);c.lessons.set([]);expect(c.available(c.slots()[0])).toBe(false);
    c.edit(c.slots()[0]);expect(c.hasSlot()).toBe(true);c.remove();expect(c.slots()).toHaveLength(0);
  });
  it('preserves the draft after a temporary save error and supports retry',()=>{
    const{c,service}=setup();c.add();c.apply();service.save.mockReturnValueOnce(throwError(()=>({status:503})));c.save();expect(c.saving()).toBe(false);expect(c.conflict()).toBe(false);expect(c.dirty()).toBe(true);c.save();expect(c.dirty()).toBe(false);expect(service.save).toHaveBeenCalledTimes(2);
  });
  it('navigates month boundaries and protects unsaved work when leaving',()=>{
    const{c}=setup();c.changeView('month');c.anchor.set(new Date(2026,0,31));c.navigate(1);expect(c.anchor().getDate()).toBe(28);expect(c.title()).toContain('2026');
    c.changeView('week');c.navigate(1);expect(c.anchor().getMonth()).toBe(2);c.changeView('day');c.navigate(-1);expect(c.title()).toContain('2026');
    c.timeZoneId='Invalid/Zone';c.today();expect(c.error()).toContain('saat dilimi');c.timeZoneId='Europe/Istanbul';c.today();
    c.add();c.apply();const confirm=vi.spyOn(window,'confirm').mockReturnValue(false);expect(c.canLeavePage()).toBe(false);const event={preventDefault:vi.fn(),returnValue:undefined} as unknown as BeforeUnloadEvent;c.beforeUnload(event);expect(event.preventDefault).toHaveBeenCalled();confirm.mockReturnValue(true);expect(c.canLeavePage()).toBe(true);confirm.mockRestore();
  });
});
