import { ToasterService } from '../../../core/services/toaster.service';
import {TestBed} from '@angular/core/testing';
import {provideRouter} from '@angular/router';
import {of,Subject,throwError} from 'rxjs';
import {CoachingWeeklyDraftService,WeeklyDraft} from '../../../core/services/coaching-weekly-draft.service';
import {CoachingStudyPlanningService,StudyAvailability} from '../../../core/services/coaching-study-planning.service';
import {StudentWeeklyCalendarComponent} from './student-weekly-calendar.component';

describe('weekly draft calendar',()=>{
  const lesson={id:'math',name:'Matematik',examCode:'LGS',gradeNumber:8};
  const slot={id:'one',lessonId:'math',lessonName:'Matematik',examCode:'LGS',day:'Monday' as const,startMinute:1080,durationMinutes:60};
  async function setup(draft:WeeklyDraft|null=null,maxDayMinutes?:number,availability:StudyAvailability|null=null){
    const service={get:vi.fn<CoachingWeeklyDraftService['get']>(()=>of({draft,lessons:[lesson],...(maxDayMinutes?{maxDayMinutes}:{})})),save:vi.fn<CoachingWeeklyDraftService['save']>(()=>of({version:1,timeZoneId:'Europe/Istanbul',slots:[slot]}))};
    const planning={getAvailability:vi.fn(()=>of(availability))};
    TestBed.configureTestingModule({imports:[StudentWeeklyCalendarComponent],providers:[provideRouter([]),{provide:CoachingWeeklyDraftService,useValue:service},{provide:CoachingStudyPlanningService,useValue:planning}]});
    const fixture=TestBed.createComponent(StudentWeeklyCalendarComponent);fixture.detectChanges(); await fixture.whenStable();return{fixture,c:fixture.componentInstance,service,planning};
  }
  // Paket H5 (K4: warn, allow): cards outside the saved study times are marked and named above the calendar; saving still works.
  it('marks the cards outside the study times, names how many and still saves',async ()=>{
    const hours:StudyAvailability={version:2,timeZoneId:'Europe/Istanbul',windows:[{day:'Monday',startMinute:1080,endMinute:1140}]};
    const week={version:3,timeZoneId:'Europe/Istanbul',slots:[slot,{...slot,id:'two',startMinute:1200},{...slot,id:'three',day:'Tuesday' as const},{...slot,id:'four',day:'Wednesday' as const,durationMinutes:30}]};
    const{fixture,c,service,planning}=await setup(week,undefined,hours);
    c.anchor.set(new Date(2026,9,19));c.changeView('week');fixture.detectChanges();await fixture.whenStable();
    const el=fixture.nativeElement as HTMLElement;expect(planning.getAvailability).toHaveBeenCalledTimes(1);
    const notice=el.querySelector('[data-testid="availability-notice"]') as HTMLElement;
    expect(notice.textContent?.replace(/\s+/g,' ').trim()).toBe('3 ders kartın müsait saatlerinin dışında. İstersen müsait saatlerini güncelle ya da kartları taşı.');
    expect(notice.querySelector('a')?.getAttribute('href')).toBe('/coaching-portal/plan/hours');
    const outside=Array.from(el.querySelectorAll('.slot-card.outside'),x=>x.getAttribute('data-slot-id'));expect(outside.sort()).toEqual(['four','three','two']);
    expect(el.querySelector('[data-slot-id="one"]')!.classList.contains('outside')).toBe(false);
    const card=el.querySelector('[data-slot-id="two"] .slot-content') as HTMLElement;
    expect(card.getAttribute('aria-label')).toContain('Müsait saatlerinin dışında');expect(card.textContent).toContain('Müsait saatlerinin dışında');
    expect(el.querySelector('[data-slot-id="one"] .slot-content')!.getAttribute('aria-label')).not.toContain('Müsait saatlerinin dışında');
    // Removing a card outside updates the count; saving is allowed with cards outside.
    c.edit(week.slots[1]);c.remove();fixture.detectChanges();
    expect(c.outsideCount()).toBe(2);c.save();expect(service.save).toHaveBeenCalledTimes(1);expect(c.error()).toBe('');
    c.changeView('month');fixture.detectChanges();await fixture.whenStable();
    expect(el.querySelector('.month-slot.outside')?.getAttribute('aria-label')).toContain('Müsait saatlerinin dışında');
  });
  it('asks for study times instead of marking cards when none are saved',async ()=>{
    const{fixture}=await setup({version:0,timeZoneId:'Europe/Istanbul',slots:[slot]});
    const el=fixture.nativeElement as HTMLElement;const notice=el.querySelector('[data-testid="availability-notice"]') as HTMLElement;
    expect(notice.textContent?.replace(/\s+/g,' ').trim()).toBe('Müsait saatlerini kaydedersen dışarıda kalan kartları gösteririm.');
    expect(notice.querySelector('a')?.getAttribute('href')).toBe('/coaching-portal/plan/hours');expect(el.querySelector('.slot-card.outside')).toBeNull();
  });
  it('shows no availability marks when the study times cannot be loaded and refreshes them on request',async ()=>{
    const{fixture,c,planning}=await setup({version:0,timeZoneId:'Europe/Istanbul',slots:[slot]});
    planning.getAvailability.mockReturnValue(throwError(()=>({status:503})));c.loadAvailability();fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[data-testid="availability-notice"]')).toBeNull();expect(c.error()).toBe('');
    planning.getAvailability.mockReturnValue(of({version:1,timeZoneId:'Europe/Istanbul',windows:[{day:'Sunday',startMinute:0,endMinute:60}]}));c.loadAvailability();fixture.detectChanges();
    expect(c.outsideCount()).toBe(1);expect((fixture.nativeElement.querySelector('[data-testid="availability-notice"]') as HTMLElement).textContent).toContain('1 ders kartın');
  });
  it('opens three views and a separate draft with a link to existing plans',async ()=>{
    const{fixture,c,service}=await setup();expect(service.get).toHaveBeenCalledTimes(1);expect(c.slots()).toEqual([]);
    expect(fixture.nativeElement.querySelectorAll('.view-switch button')).toHaveLength(3);
    expect(fixture.nativeElement.querySelector('a[href="/coaching-portal/plan"]')).not.toBeNull();
    c.changeView('month');fixture.detectChanges(); await fixture.whenStable();expect(fixture.nativeElement.querySelector('.month-grid')).not.toBeNull();expect(c.dirty()).toBe(false);
  });
  // Paket H1 (K5): the daily limit comes from the server (10 hours for a graduate); without it the calendar uses 8 hours.
  it('refuses a card that takes a day over the limit sent by the server and says the limit up front',async ()=>{
    const day=Array.from({length:8},(_,i)=>({...slot,id:'m'+i,startMinute:480+i*60}));
    const school=await setup({version:0,timeZoneId:'Europe/Istanbul',slots:day});
    expect((school.fixture.nativeElement.querySelector('.calendar-help') as HTMLElement).textContent).toContain('Bir güne en çok 8 saat çalışma koyabilirsin.');
    school.c.add(new Date(2027,0,4),1200);school.c.apply();
    expect(school.c.formError).toBe('Pazartesi günü için en çok 8 saat çalışma koyabilirsin.');
    TestBed.resetTestingModule();
    const graduate=await setup({version:0,timeZoneId:'Europe/Istanbul',slots:day},600);
    expect((graduate.fixture.nativeElement.querySelector('.calendar-help') as HTMLElement).textContent).toContain('Bir güne en çok 10 saat çalışma koyabilirsin.');
    graduate.c.add(new Date(2027,0,4),1200);graduate.c.apply();
    expect(graduate.c.formError).toBe('');expect(graduate.c.slots()).toHaveLength(9);
  });
  // Review of PR 129: a day saved above the limit stays editable and can be saved; it may not grow.
  it('keeps a saved day above the limit editable',async ()=>{
    const ten=Array.from({length:10},(_,i)=>({...slot,id:'m'+i,startMinute:420+i*60}));
    const{c,service}=await setup({version:3,timeZoneId:'Europe/Istanbul',slots:ten});
    c.add(new Date(2027,0,5),1080);c.apply();expect(c.formError).toBe('');
    c.edit(ten[9]);c.remove();expect(c.slots().filter(x=>x.day==='Monday')).toHaveLength(9);
    c.add(new Date(2027,0,4),1020);c.apply();expect(c.formError).toBe('');
    c.add(new Date(2027,0,4),1260);c.apply();expect(c.formError).toBe('Pazartesi günü için en çok 8 saat çalışma koyabilirsin.');
    c.escape();c.save();expect(service.save).toHaveBeenCalledTimes(1);expect(c.error()).toBe('');
  });
  it('names the footer button after the yearly preview it opens',async ()=>{
    const{fixture}=await setup({version:0,timeZoneId:'Europe/Istanbul',slots:[slot]});
    const button=fixture.nativeElement.querySelector('[data-testid="weekly-preview-link"]') as HTMLElement;
    expect(button.textContent?.trim()).toBe('Yıllık plan önizlemesine geç →');
  });
  it('opens saved evening cards instead of empty morning hours and preserves scroll while editing',async ()=>{
    const{fixture,c}=await setup({version:0,timeZoneId:'Europe/Istanbul',slots:[slot]});
    const scroller=fixture.nativeElement.querySelector('.time-scroller') as HTMLElement;
    expect(scroller.scrollTop).toBe(17.5*72);
    scroller.scrollTop=1200;c.edit(slot);c.form!.duration=30;c.apply();fixture.detectChanges(); await fixture.whenStable();
    expect(scroller.scrollTop).toBe(1200);
  });
  it('opens the next occupied day on mobile without changing the explicit Today action',async ()=>{
    const width=vi.spyOn(window,'innerWidth','get').mockReturnValue(390);
    try{
      const{fixture,c}=await setup({version:0,timeZoneId:'Europe/Istanbul',slots:[slot]});
      expect(c.view()).toBe('day');expect(c.rows(c.anchor())).toHaveLength(1);
      expect((fixture.nativeElement.querySelector('.time-scroller') as HTMLElement).scrollTop).toBe(17.5*72);
      c.today();const today=new Intl.DateTimeFormat('en-CA',{timeZone:'Europe/Istanbul'}).format(new Date());
      expect(new Intl.DateTimeFormat('en-CA').format(c.anchor())).toBe(today);
    }finally{width.mockRestore();}
  });
  it('allows plan generation only from the saved nonempty weekly draft',async ()=>{
    const{c,fixture}=await setup({version:0,timeZoneId:'Europe/Istanbul',slots:[slot]});
    const button=()=>fixture.nativeElement.querySelector('[data-testid="weekly-preview-link"]') as HTMLButtonElement;
    expect(button()).not.toBeNull();expect(button().disabled).toBe(false);c.edit(slot);c.form!.duration=30;c.apply();fixture.detectChanges(); await fixture.whenStable();expect(button().disabled).toBe(true);c.save();fixture.detectChanges(); await fixture.whenStable();expect(button().disabled).toBe(false);
  });
  it('adds a half hour, keeps the other half free and rejects overlapping edits',async ()=>{
    const{c}=await setup();c.add(new Date(2026,9,19),1080);c.form!.duration=30;c.apply();expect(c.slots()[0].durationMinutes).toBe(30);
    c.add(new Date(2026,9,19),1110);c.form!.duration=30;c.apply();expect(c.slots()).toHaveLength(2);
    c.edit(c.slots()[0]);c.form!.duration=60;c.apply();expect(c.formError).toContain('çakış');expect(c.slots()[0].durationMinutes).toBe(30);
  });
  it('preserves unsaved cards and blocks overwriting after a save conflict',async ()=>{
    const{c,service}=await setup();c.add();c.apply();service.save.mockReturnValue(throwError(()=>({status:409,error:{message:'Taslak değişti'}})));
    c.save();expect(c.error()).toBe('Taslak değişti');expect(c.conflict()).toBe(true);expect(c.dirty()).toBe(true);expect(c.slots()).toHaveLength(1);
    c.save();expect(service.save).toHaveBeenCalledTimes(1);
  });
  it('saves with the expected version and prevents edits or leaving during the request',async ()=>{
    const{c,service}=await setup({version:3,timeZoneId:'Europe/Istanbul',slots:[slot]});c.edit(slot);c.form!.duration=30;c.apply();
    const response=new Subject<WeeklyDraft>();service.save.mockReturnValue(response);c.save();
    expect(service.save.mock.calls[0][0]).toBe(3);expect(await c.canLeavePage()).toBe(false);c.add();expect(c.form).toBeNull();
    response.next({version:4,timeZoneId:'Europe/Istanbul',slots:c.slots()});expect(c.dirty()).toBe(false);expect(c.message()).toContain('kaydedildi');
  });
  it('keeps saved work if loading fails and allows a guarded reload',async ()=>{
    const{c,service}=await setup();c.add();c.apply();const confirm=vi.spyOn(TestBed.inject(ToasterService), 'confirm').mockResolvedValue(false);await c.load(true);expect(service.get).toHaveBeenCalledTimes(1);
    confirm.mockResolvedValue(true);service.get.mockReturnValue(throwError(()=>({status:503})));await c.load(true);expect(c.slots()).toHaveLength(1);expect(c.error()).toContain('yüklenemedi');confirm.mockRestore();
  });
  it('moves cards by half hours, preserves the end on top resizing and cancels a preview',async ()=>{
    const{c,fixture}=await setup({version:0,timeZoneId:'Europe/Istanbul',slots:[slot]});c.anchor.set(new Date(2026,9,19));c.changeView('week');fixture.detectChanges(); await fixture.whenStable();
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
  it('validates lesson and clock input, permits midnight ending and removes unavailable lessons',async ()=>{
    const{c}=await setup();c.add(new Date(2026,9,19),1410);expect(c.form!.duration).toBe(30);
    c.form!.lessonId='removed';c.apply();expect(c.formError).toContain('aktif bir ders');c.form!.lessonId='math';
    for(const time of ['bad','24:00','18:61']){c.form!.start=time;c.apply();expect(c.formError).toContain('başlangıç');}
    c.form!.start='23:30';c.apply();expect(c.slots()[0].startMinute).toBe(1410);c.lessons.set([]);expect(c.available(c.slots()[0])).toBe(false);
    c.edit(c.slots()[0]);expect(c.hasSlot()).toBe(true);c.remove();expect(c.slots()).toHaveLength(0);
  });
  it('preserves the draft after a temporary save error and supports retry',async ()=>{
    const{c,service}=await setup();c.add();c.apply();service.save.mockReturnValueOnce(throwError(()=>({status:503})));c.save();expect(c.saving()).toBe(false);expect(c.conflict()).toBe(false);expect(c.dirty()).toBe(true);c.save();expect(c.dirty()).toBe(false);expect(service.save).toHaveBeenCalledTimes(2);
  });
  it('navigates month boundaries and protects unsaved work when leaving',async ()=>{
    const{c}=await setup();c.changeView('month');c.anchor.set(new Date(2026,0,31));c.navigate(1);expect(c.anchor().getDate()).toBe(28);expect(c.title()).toContain('2026');
    c.changeView('week');c.navigate(1);expect(c.anchor().getMonth()).toBe(2);c.changeView('day');c.navigate(-1);expect(c.title()).toContain('2026');
    c.timeZoneId='Invalid/Zone';c.today();expect(c.error()).toContain('Bugünün tarihi alınamadı');c.timeZoneId='Europe/Istanbul';c.today();
    c.add();c.apply();const confirm=vi.spyOn(TestBed.inject(ToasterService), 'confirm').mockResolvedValue(false);expect(await c.canLeavePage()).toBe(false);const event=new Event('beforeunload',{cancelable:true});window.dispatchEvent(event);expect(event.defaultPrevented).toBe(false);confirm.mockResolvedValue(true);expect(await c.canLeavePage()).toBe(true);confirm.mockRestore();
  });
});

beforeEach(() => { TestBed.overrideProvider(ToasterService, {useValue:{confirm:vi.fn().mockResolvedValue(true),success:vi.fn(),error:vi.fn(),info:vi.fn(),warning:vi.fn()}}); });
