import {StudyTask} from '../../../core/services/coaching-study-planning.service';
import {calendarBlocks,calendarFocusDate,calendarToday} from './study-plan-calendar';

describe('dated study plan calendar',()=>{
  const task=(id:string,date:string,start:number|null,minutes=30,lesson='math',completed=false):StudyTask=>({id,plannedDate:date,plannedStartMinute:start,plannedMinutes:minutes,weeklyLessonId:lesson,lessonName:lesson,title:id,topicId:null,isPinned:false,isCompleted:completed,actualMinutes:completed?minutes:null,completedAt:null});
  it('merges only contiguous pieces of the same saved lesson without changing tasks',()=>{
    const tasks=[task('b','2027-01-04',1110,15),task('a','2027-01-04',1080),task('c','2027-01-04',1125,15,'turkish'),task('d','2027-01-05',1080),task('e','2027-01-04',1200)];const before=JSON.stringify(tasks);
    const blocks=calendarBlocks(tasks);expect(blocks).toHaveLength(4);expect(blocks[0].tasks.map(t=>t.id)).toEqual(['a','b']);expect(blocks[0].minutes).toBe(45);expect(blocks[1].lesson).toBe('turkish');expect(JSON.stringify(tasks)).toBe(before);
  });
  it('keeps untimed legacy tasks separate and never invents midnight slots',()=>{
    const blocks=calendarBlocks([task('a','2026-10-01',null),task('b','2026-10-01',null),task('c','2026-10-01',0)]);
    expect(blocks.filter(b=>b.startMinute===null)).toHaveLength(2);expect(blocks.find(b=>b.startMinute===0)?.tasks[0].id).toBe('c');
  });
  it('focuses upcoming unfinished work, then upcoming or the latest past occupied date',()=>{
    const blocks=calendarBlocks([task('old','2026-09-01',1080),task('done','2026-10-18',1080,30,'math',true),task('next','2026-10-20',1080)]);
    expect(calendarFocusDate(blocks,'2026-10-17')).toBe('2026-10-20');expect(calendarFocusDate(blocks,'2027-01-01')).toBe('2026-10-20');
    expect(calendarFocusDate(calendarBlocks([task('done','2026-10-18',1080,30,'math',true)]),'2026-10-17')).toBe('2026-10-18');expect(calendarFocusDate([],'2026-10-17')).toBe('2026-10-17');
  });
  it('uses the saved plan timezone for today across midnight and distinguishes exact dates',()=>{
    expect(calendarToday('Europe/Istanbul',new Date('2026-10-17T21:30:00Z'))).toBe('2026-10-18');
    expect(calendarToday('America/Los_Angeles',new Date('2026-10-17T21:30:00Z'))).toBe('2026-10-17');
    expect(calendarBlocks([task('one','2027-01-04',1080),task('two','2027-01-11',1080)])).toHaveLength(2);
  });
  it('preserves overlapping intervals as separate blocks and uses topic names for legacy lessons',()=>{
    const blocks=calendarBlocks([task('one','2027-01-04',1080,60),task('two','2027-01-04',1110,30)]);expect(blocks).toHaveLength(2);
    const legacy={...task('Legacy topic','2027-01-05',null),lessonName:null,weeklyLessonId:null};expect(calendarBlocks([legacy])[0].lesson).toBe('Legacy topic');
  });
});
