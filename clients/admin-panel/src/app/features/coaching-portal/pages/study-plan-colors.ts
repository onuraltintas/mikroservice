import {StudyTask} from '../../../core/services/coaching-study-planning.service';

export function lessonKey(task:StudyTask){return task.weeklyLessonId??task.lessonName??task.title;}
/** Separate lesson colors; calendar and detail cards share the same palette. */
export function lessonColors(tasks:StudyTask[]):Map<string,string>{
  const colors=new Map<string,string>(),used=new Set<number>();
  const palette=[180,30,270,120,330,210];
  const lessons=new Set(tasks.filter(task=>task.weeklyLessonId||task.lessonName).map(lessonKey));
  const keys=[...new Set(tasks.map(lessonKey))].sort((a,b)=>Number(lessons.has(b))-Number(lessons.has(a))||a.localeCompare(b));
  for(const key of keys){
    const hash=[...key].reduce((value,c)=>(value*31+c.charCodeAt(0))%360,0);
    let index=hash%palette.length;
    while(used.has(index))index=(index+1)%(Math.max(palette.length,colors.size+1));
    used.add(index);
    const hue=palette[index%palette.length];
    const lightness=44-Math.floor(index/palette.length)*8;
    colors.set(key,`hsl(${hue} 62% ${Math.max(20,lightness)}%)`);
  }
  return colors;
}
