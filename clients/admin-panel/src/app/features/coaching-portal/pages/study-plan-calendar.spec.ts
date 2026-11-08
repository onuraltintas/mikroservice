import {StudyTask} from '../../../core/services/coaching-study-planning.service';
import {calendarBlocks,calendarFocusDate,calendarToday,canSplitTask,splitRanges,taskSourceBadge} from './study-plan-calendar';

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
  // Paket H5: a split task's halves stay two cards (the plan engine would have joined equal neighbours), so either can be moved.
  it('keeps the halves of a split task apart but still joins different outcome pieces',()=>{
    const piece=(id:string,start:number,outcome:string,extra:Partial<StudyTask>={})=>({...task(id,'2027-01-04',start),title:'Çarpanlar',topicId:'topic',planKind:'Learn',
      outcomes:[{outcomeId:outcome,code:outcome,name:outcome,estimateId:'e',estimateNumber:1,allocatedMinutes:30,origin:'Catalog'}],...extra});
    expect(calendarBlocks([piece('a',1080,'o1'),piece('b',1110,'o2')])).toHaveLength(1);
    expect(calendarBlocks([piece('a',1080,'o1'),piece('b',1110,'o1')]).map(b=>b.tasks.map(t=>t.id))).toEqual([['a'],['b']]);
    const own=(id:string,start:number)=>({...task(id,'2027-01-04',start),title:'Soru çözümü'});
    expect(calendarBlocks([own('x',1080),own('y',1110)])).toHaveLength(2);
    // A version 1 plan task (topic, no engine decision) keeps its old joined card.
    const legacy=(id:string,start:number)=>({...task(id,'2027-01-04',start),title:'Çarpanlar',topicId:'topic'});
    expect(calendarBlocks([legacy('l1',1080),legacy('l2',1110)])).toHaveLength(1);
  });
  it('splits only timed, untouched work of at least an hour into a rounded first half and the rest',()=>{
    const work={...task('w','2027-01-04',1080,60),planKind:'Learn',topicId:'topic'};
    expect(canSplitTask(work)).toBe(true);expect(splitRanges(work)).toEqual([[1080,1110],[1110,1140]]);
    expect(splitRanges({...work,plannedMinutes:90})).toEqual([[1080,1140],[1140,1170]]);
    expect(splitRanges({...work,plannedMinutes:120})).toEqual([[1080,1140],[1140,1200]]);
    expect(canSplitTask({...task('own','2027-01-04',1080,60)})).toBe(true);
    // Paket H5 review: each half must be at least 30 minutes and a multiple of 30.
    expect(canSplitTask({...work,plannedMinutes:90})).toBe(true);expect(canSplitTask({...work,plannedMinutes:150})).toBe(true);
    for(const blocked of [{plannedMinutes:45},{plannedMinutes:61},{plannedMinutes:75},{plannedMinutes:89},{plannedStartMinute:null},{isCompleted:true},{actualMinutes:10},{predecessorTaskId:'p'},{skippedAt:'2027-01-05T00:00:00Z'},
      {realizationHistory:[{at:'x'} as never]},{planKind:null}])expect(canSplitTask({...work,...blocked})).toBe(false);
  });
  it('names where each task came from',()=>{
    const base=task('k','2027-01-04',1080);
    expect(['Learn','Review','Refresh','Spaced'].map(planKind=>taskSourceBadge({...base,planKind,topicId:'t'}))).toEqual(['Yeni konu','Tekrar','Kısa tekrar','Aralıklı tekrar']);
    // Paket H5 review: who added a task is not stored, so the label is neutral for everyone.
    expect(taskSourceBadge(base)).toBe('Elle eklenen');
    expect(taskSourceBadge({...base,topicId:'t'})).toBeNull();expect(taskSourceBadge({...base,sourceName:'DenemeTekrar:x'})).toBeNull();
    expect(taskSourceBadge({...base,weeklyLessonId:null})).toBeNull();expect(taskSourceBadge({...base,planKind:'Other'})).toBeNull();
    expect(taskSourceBadge({...base,predecessorTaskId:'p'})).toBeNull();
  });
});
