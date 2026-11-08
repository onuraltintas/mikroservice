import { ToasterService } from '../../../core/services/toaster.service';
import {TestBed} from '@angular/core/testing';
import {StudyPlan,StudyTask} from '../../../core/services/coaching-study-planning.service';
import {StudyPlanCalendarComponent} from './study-plan-calendar.component';
import {lessonColors} from './study-plan-colors';

describe('saved study plan calendar',()=>{
  const task=(id:string,date='2027-01-04',start:number|null=1080):StudyTask=>({id,plannedDate:date,plannedStartMinute:start,plannedMinutes:30,weeklyLessonId:'math',lessonName:'Matematik',title:'Çarpanlar',topicId:null,isPinned:false,isCompleted:false,actualMinutes:null,completedAt:null});
  const plan:StudyPlan={id:'annual',version:0,title:'Yıllık plan',status:'Draft',tasks:[task('a'),{...task('b','2027-01-04',1110),title:'Üslü ifadeler'},task('c','2027-03-08',1200)],weeklySource:{draftVersion:1,learningContextVersion:2,startDate:'2027-01-01',endDate:'2027-06-01',reuseFreedSlots:false,fingerprint:'snapshot',activeRevisionId:null,activeRevisionVersion:null,timeZoneId:'Europe/Istanbul'}};
  async function setup(value=plan){TestBed.configureTestingModule({imports:[StudyPlanCalendarComponent]});const f=TestBed.createComponent(StudyPlanCalendarComponent);f.componentRef.setInput('plan',value);f.detectChanges(); await f.whenStable();return{f,c:f.componentInstance};}
  beforeEach(()=>{vi.useFakeTimers({toFake:['Date']});vi.setSystemTime(new Date('2026-10-17T12:00:00Z'));});
  afterEach(()=>vi.useRealTimers());
  it('shows partial effort without marking the calendar task completed',async()=>{
    const {f}=await setup({...plan,status:'Active',tasks:[{...task('partial'),actualMinutes:12,actualQuestions:4}]});
    expect(f.nativeElement.textContent).toContain('Kısmi çalışma · 12 dk');
    expect(f.nativeElement.querySelector('.timed-block.completed')).toBeNull();
  });
  it('opens the next occupied week and its evening hours, merges adjacent outcome pieces and emits their IDs',async()=>{
    const {f,c}=await setup();expect(c.dateKey(c.anchor())).toBe('2027-01-04');expect(f.nativeElement.querySelector('.time-scroller').scrollTop).toBe(2100);
    expect(f.nativeElement.querySelectorAll('.timed-block')).toHaveLength(1);const emit=vi.spyOn(c.taskSelected,'emit');
    (f.nativeElement.querySelector('.timed-block') as HTMLButtonElement).click();expect(emit).toHaveBeenCalledWith(['a','b']);expect(plan.version).toBe(0);expect(plan.tasks[0].isCompleted).toBe(false);
  });
  it('opens a blank time menu at the clicked half hour and emits add without changing the plan',async()=>{
    const {f,c}=await setup();f.componentRef.setInput('editable',true);c.changeView('day');f.detectChanges();await f.whenStable();
    const track=f.nativeElement.querySelector('.day-track') as HTMLElement;
    vi.spyOn(track,'getBoundingClientRect').mockReturnValue({top:100} as DOMRect);
    const emit=vi.spyOn(c.calendarAction,'emit');
    track.dispatchEvent(new MouseEvent('contextmenu',{bubbles:true,cancelable:true,clientX:200,clientY:160}));f.detectChanges();await f.whenStable();
    expect(c.menu?.startMinute).toBe(30);expect(f.nativeElement.querySelector('[role=menu]')).not.toBeNull();
    c.action('add');expect(emit).toHaveBeenCalledWith({kind:'add',taskIds:[],date:'2027-01-04',startMinute:30});expect(plan.tasks).toHaveLength(3);
  });
  it('offers completion only to students, protects completed cards and supports the keyboard menu',async()=>{
    const {f,c}=await setup({...plan,status:'Active',tasks:[{...task('done'),isCompleted:true,actualMinutes:30}]});f.componentRef.setInput('editable',true);f.detectChanges();await f.whenStable();
    const card=f.nativeElement.querySelector('.timed-block') as HTMLElement;
    card.dispatchEvent(new KeyboardEvent('keydown',{key:'F10',shiftKey:true,bubbles:true,cancelable:true}));f.detectChanges();await f.whenStable();
    const emit=vi.spyOn(c.calendarAction,'emit');c.action('delete');c.action('complete');expect(emit).not.toHaveBeenCalled();
    expect(f.nativeElement.textContent).toContain('Tamamlanan çalışmalar korunur');
    f.componentRef.setInput('student',false);f.detectChanges();card.dispatchEvent(new KeyboardEvent('keydown',{key:'ContextMenu',bubbles:true,cancelable:true}));f.detectChanges();expect(f.nativeElement.querySelector('[role=menu]').textContent).not.toContain('Tamamlandı olarak kaydet');
    c.escapeMove();expect(c.menu).toBeNull();
  });
  it('gives distinct lessons distinct colors and keeps an editable empty day available',async()=>{
    const {f,c}=await setup({...plan,tasks:[task('math'),{...task('science','2027-01-05'),weeklyLessonId:'science',lessonName:'Fen Bilimleri'}]});
    const blocks=c.days().flatMap(d=>c.rows(d));expect(new Set(blocks.map(b=>c.color(b))).size).toBe(2);
    f.componentRef.setInput('editable',true);c.changeView('day');c.goToDate('2027-01-06');f.detectChanges();await f.whenStable();
    expect(f.nativeElement.querySelector('.day-track')).not.toBeNull();
  });
  it('separates six lessons into six color families independently of task order',()=>{
    const tasks=Array.from({length:6},(_,i)=>({...task(String(i)),weeklyLessonId:`lesson-${i}`}));
    const colors=lessonColors(tasks);
    expect(new Set([...colors.values()].map(color=>color.split(' ')[0])).size).toBe(6);
    expect(lessonColors([...tasks].reverse())).toEqual(colors);
    const withCustom=lessonColors([{...task('custom'),weeklyLessonId:null,lessonName:null,title:'A custom work'},...tasks]);
    expect(new Set(tasks.map(task=>withCustom.get(task.weeklyLessonId!)!.split(' ')[0])).size).toBe(6);
  });
  it('shows exact month dates rather than repeating a weekly template and opens a selected day',async()=>{
    const {f,c}=await setup();c.changeView('month');f.detectChanges(); await f.whenStable();expect(f.nativeElement.querySelectorAll('.month-block')).toHaveLength(1);
    c.openDay(new Date(2027,2,8));f.detectChanges(); await f.whenStable();expect(c.view()).toBe('day');expect(f.nativeElement.querySelector('.time-scroller').scrollTop).toBe(2340);
    expect(f.nativeElement.textContent).toContain('20:00');expect(f.nativeElement.querySelectorAll('.timed-block')).toHaveLength(1);
  });
  it('focuses the occupied anchor day instead of an earlier completed lesson elsewhere in the week',async()=>{
    const {f,c}=await setup({...plan,tasks:[{...task('done','2027-01-04',480),isCompleted:true,actualMinutes:25,completedAt:'2026-10-16T10:00:00Z'},task('next','2027-01-10',1200)]});
    expect(c.dateKey(c.anchor())).toBe('2027-01-10');expect(f.nativeElement.querySelector('.time-scroller').scrollTop).toBe(2340);
  });
  it('skips empty dates with occupied-day navigation while Today and a chosen empty date stay explicit',async()=>{
    const {f,c}=await setup();c.occupied(1);f.detectChanges(); await f.whenStable();expect(c.dateKey(c.anchor())).toBe('2027-03-08');c.occupied(-1);expect(c.dateKey(c.anchor())).toBe('2027-01-04');
    c.changeView('day');c.today();f.detectChanges(); await f.whenStable();expect(c.dateKey(c.anchor())).toBe('2026-10-17');expect(f.nativeElement.textContent).toContain('Bu aralıkta çalışma yok');
    c.goToDate('2027-02-02');expect(c.dateKey(c.anchor())).toBe('2027-02-02');c.goToDate('');expect(c.dateKey(c.anchor())).toBe('2027-02-02');
    c.focusWork();expect(c.dateKey(c.anchor())).toBe('2027-01-04');
  });
  it('keeps legacy untimed completed work in an untimed area without inventing a time',async()=>{
    const legacy={...plan,id:'old',weeklySource:null,status:'Archived' as const,tasks:[{...task('old','2026-10-01',null),isCompleted:true,actualMinutes:25,actualQuestions:0}]};
    const before=JSON.stringify(legacy);const {f,c}=await setup(legacy);expect(f.nativeElement.querySelector('.time-scroller')).toBeNull();expect(f.nativeElement.textContent).toContain('Saati belirtilmemiş');expect(f.nativeElement.textContent).toContain('Tamamlandı');c.showDay();expect(JSON.stringify(legacy)).toBe(before);
  });
  it('shows completed, not completed, overdue and partial status on day and month calendar cards',async()=>{
    const date='2026-10-16';
    const value={...plan,tasks:[
      {...task('late',date,600),lessonName:'Matematik'},
      {...task('partial-a',date,660),weeklyLessonId:'science',lessonName:'Fen Bilimleri',isCompleted:true,actualMinutes:25},
      // Paket H5: pieces of one lesson are different work (equal neighbours would be the halves of a split, shown apart).
      {...task('partial-b',date,690),weeklyLessonId:'science',lessonName:'Fen Bilimleri',title:'Maddenin halleri'},
      {...task('complete',date,750),weeklyLessonId:'english',lessonName:'İngilizce',isCompleted:true,actualMinutes:30}
    ]};
    const {f,c}=await setup(value);c.changeView('day');c.goToDate(date);f.detectChanges();await f.whenStable();
    expect(Array.from(f.nativeElement.querySelectorAll('.task-status'),(node:HTMLElement)=>node.textContent?.trim())).toEqual(['Gecikti · tamamlanmadı','Kısmen tamamlandı · 1/2 · gecikti','Tamamlandı']);
    expect(f.nativeElement.querySelector('.timed-block')?.getAttribute('aria-label')).toContain('Gecikti · tamamlanmadı');
    c.changeView('month');f.detectChanges();await f.whenStable();
    expect(f.nativeElement.querySelectorAll('.month-block .task-status')).toHaveLength(3);
  });
  it('preserves position after completing work in the same plan but refocuses when switching plans',async()=>{
    const {f,c}=await setup();const scroller=f.nativeElement.querySelector('.time-scroller');scroller.scrollTop=900;c.goToDate('2027-01-05');f.detectChanges(); await f.whenStable();scroller.scrollTop=950;
    f.componentRef.setInput('plan',{...plan,version:1,tasks:plan.tasks.map(t=>({...t,isCompleted:true,actualMinutes:20}))});f.detectChanges(); await f.whenStable();expect(c.dateKey(c.anchor())).toBe('2027-01-05');expect(scroller.scrollTop).toBe(950);
    f.componentRef.setInput('plan',{...plan,id:'second',tasks:[task('new','2027-05-03',600)]});f.detectChanges(); await f.whenStable();expect(c.dateKey(c.anchor())).toBe('2027-05-03');expect(f.nativeElement.querySelector('.time-scroller').scrollTop).toBe(1140);
  });
  it('supports mobile day view, empty plans, disabled controls and bounded month navigation',async()=>{
    const width=vi.spyOn(window,'innerWidth','get').mockReturnValue(390);
    try{const {f,c}=await setup({...plan,tasks:[]});expect(c.view()).toBe('day');expect(f.nativeElement.textContent).toContain('Bu planda çalışma bulunmuyor');
      c.changeView('month');c.goToDate('2027-01-31');c.navigate(1);expect(c.dateKey(c.anchor())).toBe('2027-02-28');
      f.componentRef.setInput('disabled',true);f.detectChanges(); await f.whenStable();expect(Array.from(f.nativeElement.querySelectorAll('button')).every((b:any)=>b.disabled)).toBe(true);
    }finally{width.mockRestore();}
  });
  // Paket H5: "İki parçaya böl" on a card whose work can be split; the page asks first and calls the server.
  it('offers to split timed, untouched work of an hour or more',async()=>{
    const long={...task('long','2027-01-04',1080),plannedMinutes:60,planKind:'Learn',topicId:'topic'};
    const {f,c}=await setup({...plan,status:'Active',tasks:[long,{...task('short','2027-01-04',1200)}]});f.componentRef.setInput('editable',true);f.detectChanges();await f.whenStable();
    const cards=f.nativeElement.querySelectorAll('.timed-event .card-menu-trigger') as NodeListOf<HTMLButtonElement>;
    cards[0].click();f.detectChanges();await f.whenStable();
    const item=Array.from(f.nativeElement.querySelectorAll('[role=menu] button') as NodeListOf<HTMLButtonElement>).find(b=>b.textContent?.trim()==='İki parçaya böl');
    expect(item).toBeDefined();const emit=vi.spyOn(c.calendarAction,'emit');item!.click();
    expect(emit).toHaveBeenCalledWith({kind:'split',taskIds:['long'],date:'2027-01-04',startMinute:1080});
    cards[1].click();f.detectChanges();await f.whenStable();
    expect(f.nativeElement.querySelector('[role=menu]').textContent).not.toContain('İki parçaya böl');
    f.componentRef.setInput('editable',false);f.detectChanges();cards[0].click();f.detectChanges();await f.whenStable();
    expect(f.nativeElement.querySelector('[role=menu]').textContent).not.toContain('İki parçaya böl');
  });
  // Paket H5: where the work came from, as a small badge that is also part of the card's name.
  it('shows the source badge on calendar cards and in their names',async()=>{
    const {f,c}=await setup({...plan,tasks:[{...task('spaced','2027-01-04',1080),planKind:'Spaced',topicId:'topic'},{...task('own','2027-01-04',1200),weeklyLessonId:'science',lessonName:'Fen Bilimleri'}]});
    const cards=Array.from(f.nativeElement.querySelectorAll('.timed-block') as NodeListOf<HTMLElement>);
    expect(cards.map(x=>x.querySelector('cs-status-chip')?.textContent?.trim())).toEqual(['Aralıklı tekrar','Senin eklediğin']);
    expect(cards[0].getAttribute('aria-label')).toContain('Aralıklı tekrar');expect(cards[1].getAttribute('aria-label')).toContain('Senin eklediğin');
    f.componentRef.setInput('student',false);f.detectChanges();
    expect((f.nativeElement.querySelectorAll('.timed-block')[1] as HTMLElement).getAttribute('aria-label')).toContain('Elle eklenen');
    c.changeView('month');f.detectChanges();await f.whenStable();
    expect(f.nativeElement.querySelector('.month-block cs-status-chip')?.textContent?.trim()).toBe('Aralıklı tekrar');
  });
  // Paket H5: the calendar opens on today and its first work; with nothing today, on the first work of the visible days.
  describe('opening on today',()=>{
    let reduce=false;const scrollTo=vi.fn(function(this:HTMLElement,options:ScrollToOptions){this.scrollTop=options.top??0;});const intoView=vi.fn();
    beforeEach(()=>{reduce=false;scrollTo.mockClear();intoView.mockClear();
      Object.defineProperty(HTMLElement.prototype,'scrollTo',{configurable:true,value:scrollTo});Object.defineProperty(HTMLElement.prototype,'scrollIntoView',{configurable:true,value:intoView});
      vi.stubGlobal('matchMedia',(query:string)=>({matches:query.includes('reduce')&&reduce,media:query,addEventListener:()=>{},removeEventListener:()=>{}}));});
    afterEach(()=>{delete (HTMLElement.prototype as {scrollTo?:unknown}).scrollTo;delete (HTMLElement.prototype as {scrollIntoView?:unknown}).scrollIntoView;vi.unstubAllGlobals();});
    const week=(done=false)=>({...plan,status:'Active' as const,tasks:[{...task('today','2027-01-05',480),isCompleted:done,actualMinutes:done?30:null},task('later','2027-01-07',600),task('next-week','2027-01-12',420)]});
    it('opens today even when its work is done and scrolls smoothly to its first work',async()=>{
      vi.setSystemTime(new Date('2027-01-05T06:00:00Z'));
      const {f,c}=await setup(week(true));
      expect(c.dateKey(c.anchor())).toBe('2027-01-05');
      expect(scrollTo).toHaveBeenCalledWith({top:900,behavior:'smooth'});expect((f.nativeElement.querySelector('.time-scroller') as HTMLElement).scrollTop).toBe(900);
      expect(intoView).toHaveBeenCalledWith({block:'start',behavior:'smooth'});
    });
    it('uses the first work of the visible week when today has none and does not animate for reduced motion',async()=>{
      reduce=true;vi.setSystemTime(new Date('2027-01-06T06:00:00Z'));
      const {f,c}=await setup(week());
      expect(c.dateKey(c.anchor())).toBe('2027-01-06');expect(scrollTo).not.toHaveBeenCalled();
      expect((f.nativeElement.querySelector('.time-scroller') as HTMLElement).scrollTop).toBe(900);
      expect(intoView).toHaveBeenCalledWith({block:'start',behavior:'auto'});
    });
    it('does not scroll again on later renders, even when the hours are drawn again',async()=>{
      vi.setSystemTime(new Date('2027-01-05T06:00:00Z'));
      const {f,c}=await setup(week());f.componentRef.setInput('editable',true);c.goToDate('2027-01-20');f.detectChanges();await f.whenStable();
      scrollTo.mockClear();intoView.mockClear();
      const first=f.nativeElement.querySelector('.time-scroller') as HTMLElement;first.scrollTop=300;
      f.componentRef.setInput('disabled',true);f.detectChanges();await f.whenStable();expect(f.nativeElement.querySelector('.time-scroller')).toBeNull();
      f.componentRef.setInput('disabled',false);f.componentRef.setInput('plan',{...week(),version:3});f.detectChanges();await f.whenStable();
      expect((f.nativeElement.querySelector('.time-scroller') as HTMLElement).scrollTop).toBe(300);
      expect(scrollTo).not.toHaveBeenCalled();expect(intoView).not.toHaveBeenCalled();
    });
  });
});

beforeEach(() => { TestBed.overrideProvider(ToasterService, {useValue:{confirm:vi.fn().mockResolvedValue(true),success:vi.fn(),error:vi.fn(),info:vi.fn(),warning:vi.fn()}}); });
