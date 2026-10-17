import {TestBed} from '@angular/core/testing';
import {StudyPlan,StudyTask} from '../../../core/services/coaching-study-planning.service';
import {StudyPlanCalendarComponent} from './study-plan-calendar.component';

describe('saved study plan calendar',()=>{
  const task=(id:string,date='2027-01-04',start:number|null=1080):StudyTask=>({id,plannedDate:date,plannedStartMinute:start,plannedMinutes:30,weeklyLessonId:'math',lessonName:'Matematik',title:'Çarpanlar',topicId:null,isPinned:false,isCompleted:false,actualMinutes:null,completedAt:null});
  const plan:StudyPlan={id:'annual',version:0,title:'Yıllık plan',status:'Draft',tasks:[task('a'),task('b','2027-01-04',1110),task('c','2027-03-08',1200)],weeklySource:{draftVersion:1,learningContextVersion:2,startDate:'2027-01-01',endDate:'2027-06-01',reuseFreedSlots:false,fingerprint:'snapshot',activeRevisionId:null,activeRevisionVersion:null,timeZoneId:'Europe/Istanbul'}};
  async function setup(value=plan){TestBed.configureTestingModule({imports:[StudyPlanCalendarComponent]});const f=TestBed.createComponent(StudyPlanCalendarComponent);f.componentRef.setInput('plan',value);f.detectChanges();await f.whenStable();return{f,c:f.componentInstance};}
  beforeEach(()=>{vi.useFakeTimers({toFake:['Date']});vi.setSystemTime(new Date('2026-10-17T12:00:00Z'));});
  afterEach(()=>vi.useRealTimers());
  it('opens the next occupied week and its evening hours, merges adjacent outcome pieces and emits their IDs',async()=>{
    const {f,c}=await setup();expect(c.dateKey(c.anchor())).toBe('2027-01-04');expect(f.nativeElement.querySelector('.time-scroller').scrollTop).toBe(1050);
    expect(f.nativeElement.querySelectorAll('.timed-block')).toHaveLength(1);const emit=vi.spyOn(c.taskSelected,'emit');
    (f.nativeElement.querySelector('.timed-block') as HTMLButtonElement).click();expect(emit).toHaveBeenCalledWith(['a','b']);expect(plan.version).toBe(0);expect(plan.tasks[0].isCompleted).toBe(false);
  });
  it('shows exact month dates rather than repeating a weekly template and opens a selected day',async()=>{
    const {f,c}=await setup();c.changeView('month');f.detectChanges();await f.whenStable();expect(f.nativeElement.querySelectorAll('.month-block')).toHaveLength(1);
    c.openDay(new Date(2027,2,8));f.detectChanges();await f.whenStable();expect(c.view()).toBe('day');expect(f.nativeElement.querySelector('.time-scroller').scrollTop).toBe(1170);
    expect(f.nativeElement.textContent).toContain('20:00');expect(f.nativeElement.querySelectorAll('.timed-block')).toHaveLength(1);
  });
  it('focuses the occupied anchor day instead of an earlier completed lesson elsewhere in the week',async()=>{
    const {f,c}=await setup({...plan,tasks:[{...task('done','2027-01-04',480),isCompleted:true,actualMinutes:25,completedAt:'2026-10-16T10:00:00Z'},task('next','2027-01-10',1200)]});
    expect(c.dateKey(c.anchor())).toBe('2027-01-10');expect(f.nativeElement.querySelector('.time-scroller').scrollTop).toBe(1170);
  });
  it('skips empty dates with occupied-day navigation while Today and a chosen empty date stay explicit',async()=>{
    const {f,c}=await setup();c.occupied(1);f.detectChanges();await f.whenStable();expect(c.dateKey(c.anchor())).toBe('2027-03-08');c.occupied(-1);expect(c.dateKey(c.anchor())).toBe('2027-01-04');
    c.changeView('day');c.today();f.detectChanges();await f.whenStable();expect(c.dateKey(c.anchor())).toBe('2026-10-17');expect(f.nativeElement.textContent).toContain('Bu aralıkta çalışma yok');
    c.goToDate('2027-02-02');expect(c.dateKey(c.anchor())).toBe('2027-02-02');c.goToDate('');expect(c.dateKey(c.anchor())).toBe('2027-02-02');
    c.focusWork();expect(c.dateKey(c.anchor())).toBe('2027-01-04');
  });
  it('keeps legacy untimed completed work in an untimed area without inventing a time',async()=>{
    const legacy={...plan,id:'old',weeklySource:null,status:'Archived' as const,tasks:[{...task('old','2026-10-01',null),isCompleted:true,actualMinutes:25,actualQuestions:0}]};
    const before=JSON.stringify(legacy);const {f,c}=await setup(legacy);expect(f.nativeElement.querySelector('.time-scroller')).toBeNull();expect(f.nativeElement.textContent).toContain('Saati belirtilmemiş');expect(f.nativeElement.textContent).toContain('Tamamlandı');c.showDay();expect(JSON.stringify(legacy)).toBe(before);
  });
  it('preserves position after completing work in the same plan but refocuses when switching plans',async()=>{
    const {f,c}=await setup();const scroller=f.nativeElement.querySelector('.time-scroller');scroller.scrollTop=900;c.goToDate('2027-01-05');f.detectChanges();await f.whenStable();scroller.scrollTop=950;
    f.componentRef.setInput('plan',{...plan,version:1,tasks:plan.tasks.map(t=>({...t,isCompleted:true,actualMinutes:20}))});f.detectChanges();await f.whenStable();expect(c.dateKey(c.anchor())).toBe('2027-01-05');expect(scroller.scrollTop).toBe(950);
    f.componentRef.setInput('plan',{...plan,id:'second',tasks:[task('new','2027-05-03',600)]});f.detectChanges();await f.whenStable();expect(c.dateKey(c.anchor())).toBe('2027-05-03');expect(f.nativeElement.querySelector('.time-scroller').scrollTop).toBe(570);
  });
  it('supports mobile day view, empty plans, disabled controls and bounded month navigation',async()=>{
    const width=vi.spyOn(window,'innerWidth','get').mockReturnValue(390);
    try{const {f,c}=await setup({...plan,tasks:[]});expect(c.view()).toBe('day');expect(f.nativeElement.textContent).toContain('Bu planda çalışma bulunmuyor');
      c.changeView('month');c.goToDate('2027-01-31');c.navigate(1);expect(c.dateKey(c.anchor())).toBe('2027-02-28');
      f.componentRef.setInput('disabled',true);f.detectChanges();expect(Array.from(f.nativeElement.querySelectorAll('button')).every((b:any)=>b.disabled)).toBe(true);
    }finally{width.mockRestore();}
  });
});
