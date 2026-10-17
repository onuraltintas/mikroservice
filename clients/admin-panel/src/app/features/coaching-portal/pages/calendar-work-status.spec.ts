import {calendarTaskOverdue} from './study-plan-calendar';
import {StudyTask} from '../../../core/services/coaching-study-planning.service';
describe('calendar overdue status',()=>{
  const now=new Date('2027-01-05T12:00:00Z');
  const task:StudyTask={id:'a',title:'Test',plannedDate:'2027-01-05',plannedStartMinute:840,plannedMinutes:30,topicId:null,isPinned:false,isCompleted:false,actualMinutes:null,completedAt:null};
  it('uses the plan timezone and exact end time; untimed work expires on the next day',()=>{
    expect(calendarTaskOverdue(task,'Europe/Istanbul',now)).toBe(true);
    expect(calendarTaskOverdue({...task,plannedStartMinute:900},'Europe/Istanbul',now)).toBe(false);
    expect(calendarTaskOverdue(task,'Europe/London',now)).toBe(false);
    expect(calendarTaskOverdue({...task,plannedStartMinute:null},'Europe/Istanbul',now)).toBe(false);
    expect(calendarTaskOverdue({...task,plannedDate:'2027-01-04',plannedStartMinute:null},'Europe/Istanbul',now)).toBe(true);
    expect(calendarTaskOverdue({...task,isCompleted:true},'Europe/Istanbul',now)).toBe(false);
  });
});
