import {StudyTask} from '../../../core/services/coaching-study-planning.service';

export function lessonKey(task:StudyTask){return task.weeklyLessonId??task.lessonName??task.title;}
/** Separate lesson colors; calendar and detail cards share the theme's lesson palette (light and dark, see coaching-student-tokens). */
export function lessonColors(tasks:StudyTask[]):Map<string,string>{
  const colors=new Map<string,string>(),used=new Set<number>();
  const palette=[0,1,2,3,4,5,6,7];
  const lessons=new Set(tasks.filter(task=>task.weeklyLessonId||task.lessonName).map(lessonKey));
  const keys=[...new Set(tasks.map(lessonKey))].sort((a,b)=>Number(lessons.has(b))-Number(lessons.has(a))||a.localeCompare(b));
  for(const key of keys){
    const hash=[...key].reduce((value,c)=>(value*31+c.charCodeAt(0))%360,0);
    let index=hash%palette.length;
    while(used.has(index))index=(index+1)%(Math.max(palette.length,colors.size+1));
    used.add(index);
    colors.set(key,`var(--coaching-lesson-text-${palette[index%palette.length]})`);
  }
  return colors;
}
