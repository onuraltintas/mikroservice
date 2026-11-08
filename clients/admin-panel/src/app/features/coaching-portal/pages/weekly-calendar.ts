export type Weekday='Monday'|'Tuesday'|'Wednesday'|'Thursday'|'Friday'|'Saturday'|'Sunday';
export type CalendarView='day'|'week'|'month';
export interface WeeklySlot {id:string;lessonId:string;lessonName:string;examCode:string|null;day:Weekday;startMinute:number;durationMinutes:number}
export const weekDays: {value:Weekday;label:string}[]=[{value:'Monday',label:'Pazartesi'},{value:'Tuesday',label:'Salı'},{value:'Wednesday',label:'Çarşamba'},{value:'Thursday',label:'Perşembe'},{value:'Friday',label:'Cuma'},{value:'Saturday',label:'Cumartesi'},{value:'Sunday',label:'Pazar'}];
export const weekday=(d:Date)=>weekDays[(d.getDay()+6)%7].value;
export const clock=(m:number)=>`${String(Math.floor(m/60)).padStart(2,'0')}:${String(m%60).padStart(2,'0')}`;
export function mondayOf(d:Date){const date=new Date(d.getFullYear(),d.getMonth(),d.getDate());date.setDate(date.getDate()-(date.getDay()+6)%7);return date;}
export function calendarDays(anchor:Date,view:CalendarView):Date[]{
  if(view==='day')return [new Date(anchor)];
  const start=view==='week'?mondayOf(anchor):mondayOf(new Date(anchor.getFullYear(),anchor.getMonth(),1));
  const last=new Date(anchor.getFullYear(),anchor.getMonth()+1,0);
  const distance=(Date.UTC(last.getFullYear(),last.getMonth(),last.getDate())-Date.UTC(start.getFullYear(),start.getMonth(),start.getDate()))/86400000;
  const count=view==='week'?7:Math.ceil((distance+1)/7)*7;
  return Array.from({length:count},(_,i)=>new Date(start.getFullYear(),start.getMonth(),start.getDate()+i));
}
/** Paket H1 (K5): the daily limit when the server does not send one (8 hours). */
export const DEFAULT_MAX_DAY_MINUTES=480;
/**
 * Paket H1 (K5): `max` is the student's daily limit from the server (the weekly draft page). Review of PR 129: `saved` is the saved
 * draft; a day saved above the limit stays editable and may not rise above its saved total. `staff` gives the teacher's wording.
 */
export interface DayLimit {max:number;saved?:WeeklySlot[];staff?:boolean}
const dayMinutes=(slots:WeeklySlot[],day:Weekday)=>slots.filter(x=>x.day===day).reduce((sum,x)=>sum+x.durationMinutes,0);
export function dayLimitError(day:Weekday,limit:DayLimit){
  const name=weekDays.find(d=>d.value===day)?.label??day;const hours=Math.floor(limit.max/60);
  return limit.staff?`${name} günü için en çok ${hours} saat çalışma konabilir.`:`${name} günü için en çok ${hours} saat çalışma koyabilirsin.`;
}
export function slotError(s:WeeklySlot,slots:WeeklySlot[],limit:DayLimit={max:DEFAULT_MAX_DAY_MINUTES}):string{
  if(!s.lessonId || !weekDays.some(d=>d.value===s.day))return 'Ders ve gün seç.';
  if(!Number.isInteger(s.startMinute)||s.startMinute<0||s.startMinute%30||![30,60].includes(s.durationMinutes)||s.startMinute+s.durationMinutes>1440)return 'Başlangıç :00 veya :30; süre 30 veya 60 dakika olmalı. Kart günün dışına çıkamaz.';
  if(slots.some(x=>x.id!==s.id&&x.day===s.day&&s.startMinute<x.startMinute+x.durationMinutes&&s.startMinute+s.durationMinutes>x.startMinute))return 'Bu saat başka bir dersle çakışıyor.';
  const day=dayMinutes(slots.filter(x=>x.id!==s.id),s.day)+s.durationMinutes;
  return day>Math.max(limit.max,dayMinutes(limit.saved??[],s.day))?dayLimitError(s.day,limit):'';
}
