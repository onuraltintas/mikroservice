import { calendarDays, mondayOf, slotError, clock, type WeeklySlot } from './weekly-calendar';

describe('weekly calendar rules',()=>{
  const a:WeeklySlot={id:'a',lessonId:'math',lessonName:'Matematik',examCode:'LGS',day:'Monday',startMinute:1080,durationMinutes:60};
  it('allows adjacent half-hours but rejects overlaps and 45 minute cards',()=>{
    expect(slotError({...a,id:'b',startMinute:1110,durationMinutes:30},[a])).toContain('çakış');
    expect(slotError({...a,id:'b',startMinute:1110,durationMinutes:30},[{...a,durationMinutes:30}])).toBe('');
    expect(slotError({...a,durationMinutes:45},[])).not.toBe('');
    expect(slotError({...a,startMinute:1410,durationMinutes:60},[])).not.toBe('');
    expect(slotError({...a,startMinute:1410,durationMinutes:30},[])).toBe('');
  });
  it('projects the same weekly template across month and year boundaries without UTC shifts',()=>{
    expect(mondayOf(new Date(2027,0,3)).getFullYear()).toBe(2026);
    expect(calendarDays(new Date(2027,0,3),'week').length).toBe(7);
    const month=calendarDays(new Date(2027,1,1),'month');expect(month.length%7).toBe(0);expect(month.filter(d=>d.getMonth()===1)).toHaveLength(28);
    expect(calendarDays(new Date(2027,0,3),'day')).toHaveLength(1);expect(clock(1440)).toBe('24:00');
  });
});
