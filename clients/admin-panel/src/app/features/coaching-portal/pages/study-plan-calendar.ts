import {StudyTask} from '../../../core/services/coaching-study-planning.service';

export interface PlanCalendarBlock {id:string;date:string;startMinute:number|null;minutes:number;lesson:string;lessonKey:string;tasks:StudyTask[]}
export interface CalendarMove {taskIds:string[];plannedDate:string;startMinute:number}
export function calendarTaskOverdue(task:StudyTask,zone='Europe/Istanbul',now=new Date()):boolean{
  if(task.isCompleted)return false;
  const today=calendarToday(zone,now);
  if(task.plannedDate!==today)return task.plannedDate<today;
  if(task.plannedStartMinute==null)return false;
  const parts=new Intl.DateTimeFormat('en-GB',{timeZone:zone,hour:'2-digit',minute:'2-digit',hourCycle:'h23'}).formatToParts(now);
  const value=(type:string)=>Number(parts.find(p=>p.type===type)?.value);
  return task.plannedStartMinute+task.plannedMinutes<=value('hour')*60+value('minute');
}
export function calendarMoveError(block:PlanCalendarBlock,date:string,start:number,tasks:StudyTask[]):string{
  if(block.tasks.some(t=>t.isCompleted))return 'Tamamlanmış çalışma içeren ders taşınamaz.';
  if(!/^\d{4}-\d{2}-\d{2}$/.test(date)||dateKey(calendarDate(date))!==date||!Number.isInteger(start)||start<0||start+block.minutes>1440)return 'Dersin tamamı geçerli bir günün içinde kalmalıdır.';
  const ids=new Set(block.tasks.map(t=>t.id)),others=tasks.filter(t=>!ids.has(t.id)&&t.plannedDate===date);
  if(others.some(t=>t.plannedStartMinute!=null&&t.plannedStartMinute<start+block.minutes&&t.plannedStartMinute+t.plannedMinutes>start))return 'Bu saat başka bir çalışmayla çakışıyor. Boş bir saat seç.';
  if(others.reduce((s,t)=>s+t.plannedMinutes,0)+block.minutes>1440)return 'Bir güne 24 saatten fazla çalışma eklenemez.';
  return '';
}
export const dateKey=(date:Date)=>`${date.getFullYear()}-${String(date.getMonth()+1).padStart(2,'0')}-${String(date.getDate()).padStart(2,'0')}`;
export const calendarDate=(value:string)=>{const [y,m,d]=value.split('-').map(Number);return new Date(y,m-1,d);};
export function calendarToday(timeZone:string,now=new Date()):string{
  const parts=new Intl.DateTimeFormat('en-CA',{timeZone,year:'numeric',month:'2-digit',day:'2-digit'}).formatToParts(now);
  const get=(type:string)=>parts.find(p=>p.type===type)!.value;
  return `${get('year')}-${get('month')}-${get('day')}`;
}
/**
 * Paket H5: two neighbouring tasks that are the same work — same title, topic, engine decision and a shared outcome (or none) — are
 * the halves of a split: the plan engine joins such neighbours itself. They stay two cards so either half can be moved. A version 1
 * plan task (a topic but no engine decision) was never joined by the engine, so its pieces keep their joined card.
 */
function sameWork(a:StudyTask,b:StudyTask){
  if(a.title!==b.title||(a.topicId??null)!==(b.topicId??null)||(a.planKind??null)!==(b.planKind??null))return false;
  if(!b.planKind&&b.topicId)return false;
  const outcomes=new Set((a.outcomes??[]).map(o=>o.outcomeId)),others=(b.outcomes??[]).map(o=>o.outcomeId);
  return !outcomes.size&&!others.length||others.some(o=>outcomes.has(o));
}
export function calendarBlocks(tasks:StudyTask[]):PlanCalendarBlock[]{
  const blocks:PlanCalendarBlock[]=[];
  for(const task of [...tasks].sort((a,b)=>a.plannedDate.localeCompare(b.plannedDate)||(a.plannedStartMinute??-1)-(b.plannedStartMinute??-1)||a.id.localeCompare(b.id))){
    const start=task.plannedStartMinute??null,lesson=task.lessonName??task.title,key=task.weeklyLessonId??lesson,previous=blocks.at(-1);
    if(start!==null&&previous?.date===task.plannedDate&&previous.startMinute!==null&&previous.lessonKey===key&&previous.startMinute+previous.minutes===start&&!sameWork(previous.tasks.at(-1)!,task)){previous.tasks.push(task);previous.minutes+=task.plannedMinutes;}
    else blocks.push({id:task.id,date:task.plannedDate,startMinute:start,minutes:task.plannedMinutes,lesson,lessonKey:key,tasks:[task]});
  }
  return blocks;
}
export function calendarFocusDate(blocks:PlanCalendarBlock[],today:string):string{
  return blocks.find(b=>b.date>=today&&b.tasks.some(t=>!t.isCompleted))?.date??blocks.find(b=>b.date>=today)?.date??blocks.at(-1)?.date??today;
}

/**
 * Paket H5: work that "İki parçaya böl" can split: timed, at least an hour, nothing recorded on it, not a continuation or skipped,
 * and either chosen by the plan engine or added by hand (a version 1 plan task would show its halves as one card again).
 * The server also refuses work bound to a weekly decision.
 */
export function canSplitTask(task:StudyTask):boolean{
  return !task.isCompleted&&task.actualMinutes==null&&!task.realizationHistory?.length&&!task.predecessorTaskId&&!task.skippedAt
    &&task.plannedStartMinute!=null&&task.plannedMinutes>=60&&(!!task.planKind||!task.topicId);
}
/** Paket H5: the two halves of a split, as the server makes them: half the minutes rounded up to a half hour, then the rest. */
export function splitRanges(task:StudyTask):[[number,number],[number,number]]{
  const start=task.plannedStartMinute??0,first=Math.ceil(Math.ceil(task.plannedMinutes/2)/30)*30;
  return [[start,start+first],[start+first,start+task.plannedMinutes]];
}
const sourceLabels:Record<string,string>={Learn:'Yeni konu',Review:'Tekrar',Refresh:'Kısa tekrar',Spaced:'Aralıklı tekrar'};
/** Paket H5: where a task came from, from the plan engine's decision; a calendar task added by hand is the student's own. */
export function taskSourceBadge(task:StudyTask,student=true):string|null{
  if(task.planKind)return sourceLabels[task.planKind]??null;
  const own=!!task.weeklyLessonId&&!task.topicId&&!task.outcomes?.length&&!task.sourceName&&!task.predecessorTaskId;
  return own?(student?'Senin eklediğin':'Elle eklenen'):null;
}
/** Paket H5: the badge colour, in the coaching status chip tones. */
export function taskSourceTone(task:StudyTask):'brand'|'accent'|'info'|'neutral'{
  return task.planKind==='Learn'?'brand':task.planKind==='Review'||task.planKind==='Refresh'?'accent':task.planKind==='Spaced'?'info':'neutral';
}
