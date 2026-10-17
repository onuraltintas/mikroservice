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
});
