import {StudyTask} from '../../../core/services/coaching-study-planning.service';

export interface PlanCalendarBlock {id:string;date:string;startMinute:number|null;minutes:number;lesson:string;lessonKey:string;tasks:StudyTask[]}
export const dateKey=(date:Date)=>`${date.getFullYear()}-${String(date.getMonth()+1).padStart(2,'0')}-${String(date.getDate()).padStart(2,'0')}`;
export const calendarDate=(value:string)=>{const [y,m,d]=value.split('-').map(Number);return new Date(y,m-1,d);};
export function calendarToday(timeZone:string,now=new Date()):string{
  const parts=new Intl.DateTimeFormat('en-CA',{timeZone,year:'numeric',month:'2-digit',day:'2-digit'}).formatToParts(now);
  const get=(type:string)=>parts.find(p=>p.type===type)!.value;
  return `${get('year')}-${get('month')}-${get('day')}`;
}
export function calendarBlocks(tasks:StudyTask[]):PlanCalendarBlock[]{
  const blocks:PlanCalendarBlock[]=[];
  for(const task of [...tasks].sort((a,b)=>a.plannedDate.localeCompare(b.plannedDate)||(a.plannedStartMinute??-1)-(b.plannedStartMinute??-1)||a.id.localeCompare(b.id))){
    const start=task.plannedStartMinute??null,lesson=task.lessonName??task.title,key=task.weeklyLessonId??lesson,previous=blocks.at(-1);
    if(start!==null&&previous?.date===task.plannedDate&&previous.startMinute!==null&&previous.lessonKey===key&&previous.startMinute+previous.minutes===start){previous.tasks.push(task);previous.minutes+=task.plannedMinutes;}
    else blocks.push({id:task.id,date:task.plannedDate,startMinute:start,minutes:task.plannedMinutes,lesson,lessonKey:key,tasks:[task]});
  }
  return blocks;
}
export function calendarFocusDate(blocks:PlanCalendarBlock[],today:string):string{
  return blocks.find(b=>b.date>=today&&b.tasks.some(t=>!t.isCompleted))?.date??blocks.find(b=>b.date>=today)?.date??blocks.at(-1)?.date??today;
}
