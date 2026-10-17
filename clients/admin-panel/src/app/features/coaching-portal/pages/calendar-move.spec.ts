import {TestBed} from '@angular/core/testing';
import {calendarMoveError,calendarBlocks} from './study-plan-calendar';
import {StudyPlanCalendarComponent} from './study-plan-calendar.component';
import {StudyPlan,StudyTask} from '../../../core/services/coaching-study-planning.service';
const task=(id:string,start:number,minutes=30):StudyTask=>({id,title:'Matematik',plannedDate:'2027-01-04',plannedStartMinute:start,plannedMinutes:minutes,lessonName:'Matematik',weeklyLessonId:'math',topicId:null,isPinned:false,isCompleted:false,actualMinutes:null,completedAt:null});
describe('annual calendar movement',()=>{
  const tasks=[task('a',1080,15),task('b',1095,45),{...task('c',570),plannedDate:'2027-01-05'}];
  it('validates the entire merged block, completed work, overlap, day end and adjacent placement',()=>{
    const block=calendarBlocks(tasks)[0];
    expect(calendarMoveError(block,'2027-01-05',540,tasks)).toContain('çakış');
    expect(calendarMoveError(block,'2027-01-05',600,tasks)).toBe('');
    expect(calendarMoveError(block,'2027-01-05',1430,tasks)).not.toBe('');
    expect(calendarMoveError({...block,tasks:[{...block.tasks[0],isCompleted:true}]},'2027-01-05',600,tasks)).toContain('Tamamlan');
  });
  it('emits a move without mutating tasks and exposes an accessible date/time alternative',async()=>{
    TestBed.configureTestingModule({imports:[StudyPlanCalendarComponent]});const f=TestBed.createComponent(StudyPlanCalendarComponent);
    const plan:StudyPlan={id:'plan',version:2,title:'Yıllık',status:'Active',tasks};
    f.componentRef.setInput('plan',plan);f.componentRef.setInput('editable',true);f.detectChanges();await f.whenStable();
    const c=f.componentInstance;const emit=vi.spyOn(c.tasksMoved,'emit'),before=JSON.stringify(tasks);
    c.requestMove(calendarBlocks(tasks)[0],'2027-01-05',600);
    expect(emit).toHaveBeenCalledWith({taskIds:['a','b'],plannedDate:'2027-01-05',startMinute:600});expect(JSON.stringify(tasks)).toBe(before);
    expect(f.nativeElement.textContent).toContain('Gün ve saat değiştir');
    c.requestMove(calendarBlocks(tasks)[0],'2027-01-05',540);expect(emit).toHaveBeenCalledTimes(1);expect(c.moveError()).toContain('çakış');
    f.componentRef.setInput('editable',false);f.detectChanges();c.requestMove(calendarBlocks(tasks)[0],'2027-01-05',600);expect(emit).toHaveBeenCalledTimes(1);
  });
  it('supports the date/time form, no-op saves and cancelling without writes',async()=>{
    TestBed.configureTestingModule({imports:[StudyPlanCalendarComponent]});const f=TestBed.createComponent(StudyPlanCalendarComponent),c=f.componentInstance;
    f.componentRef.setInput('plan',{id:'p',version:1,title:'Test',status:'Active',tasks});f.componentRef.setInput('editable',true);f.detectChanges();await f.whenStable();
    const emit=vi.spyOn(c.tasksMoved,'emit');c.openMoveForm();expect(c.moveForm?.time).toBe('18:00');
    c.chooseMoveBlock('c');expect(c.moveForm?.date).toBe('2027-01-05');c.moveForm!.time='24:00';c.applyMoveForm();expect(c.moveError()).toContain('Geçerli');
    c.moveForm!.time='09:30';c.applyMoveForm();expect(emit).not.toHaveBeenCalled();expect(c.moveForm).toBeNull();
    c.openMoveForm();c.moveForm!.date='2027-01-06';c.moveForm!.time='10:00';c.applyMoveForm();expect(emit).toHaveBeenCalledWith({taskIds:['a','b'],plannedDate:'2027-01-06',startMinute:600});
    c.openMoveForm();c.escapeMove();expect(c.moveForm).toBeNull();expect(c.preview()).toBeNull();c.applyMoveForm();
  });
  it('handles pointer thresholds, different days, cancellation and leaving the grid',async()=>{
    TestBed.configureTestingModule({imports:[StudyPlanCalendarComponent]});const f=TestBed.createComponent(StudyPlanCalendarComponent),c=f.componentInstance;
    f.componentRef.setInput('plan',{id:'p',version:1,title:'Test',status:'Active',tasks});f.componentRef.setInput('editable',true);f.detectChanges();await f.whenStable();
    c.changeView('week');c.goToDate('2027-01-04');f.detectChanges();await f.whenStable();
    const block=calendarBlocks(tasks)[0],card=f.nativeElement.querySelector('.timed-block'),track=f.nativeElement.querySelector('.day-track[data-date="2027-01-06"]');
    vi.spyOn(card,'getBoundingClientRect').mockReturnValue({top:0} as DOMRect);vi.spyOn(track,'getBoundingClientRect').mockReturnValue({top:0} as DOMRect);
    Object.defineProperty(document,'elementFromPoint',{configurable:true,value:vi.fn(()=>track)});
    const e=(x:number,y:number,id=7)=>({pointerId:id,button:0,isPrimary:true,clientX:x,clientY:y,currentTarget:card,preventDefault:vi.fn()} as unknown as PointerEvent);
    const emit=vi.spyOn(c.tasksMoved,'emit');c.beginMove(e(0,5),block);c.movePointer(e(1,6));expect(c.preview()).toBeNull();c.movePointer(e(10,605,8));expect(c.preview()).toBeNull();
    c.movePointer(e(10,605));expect(c.preview()?.startMinute).toBe(600);c.endMove(e(10,605));expect(emit).toHaveBeenCalledWith({taskIds:['a','b'],plannedDate:'2027-01-06',startMinute:600});
    const select=vi.spyOn(c.taskSelected,'emit');c.select(block);expect(select).not.toHaveBeenCalled();c.select(block);expect(select).toHaveBeenCalled();
    c.beginMove(e(0,5),block);vi.mocked(document.elementFromPoint).mockReturnValue(null);c.movePointer(e(90,500));c.endMove(e(90,500));expect(c.moveError()).toContain('boş bir saate');expect(emit).toHaveBeenCalledTimes(1);
    c.beginMove(e(0,5),block);c.cancelMove();c.endMove(e(0,605));c.movePointer(e(0,605));expect(emit).toHaveBeenCalledTimes(1);
    c.beginMove(e(0,5),block);c.endMove(e(0,5));expect(emit).toHaveBeenCalledTimes(1);
    c.changeView('month');f.detectChanges();const day=f.nativeElement.querySelector('.month-day[data-date="2027-01-06"]');vi.mocked(document.elementFromPoint).mockReturnValue(day);
    c.beginMove(e(0,5),block);c.movePointer(e(50,60));c.endMove(e(50,60));expect(emit).toHaveBeenLastCalledWith({taskIds:['a','b'],plannedDate:'2027-01-06',startMinute:1080});
  });
  it('rejects invalid dates and excessive daily totals without assigning hours to legacy work',()=>{
    const b=calendarBlocks([task('a',1080)])[0];
    for(const [date,start]of [['2027-02-31',600],['bad',0],['2027-01-05',-1],['2027-01-05',12.5]] as const)expect(calendarMoveError(b,date,start,tasks)).not.toBe('');
    expect(calendarMoveError(b,'2027-01-05',600,[{...task('legacy',0,1440),plannedDate:'2027-01-05',plannedStartMinute:null}])).toContain('24 saat');
  });
});
