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
  // Paket H1 (K5, 08.11.2026): a day of the weekly routine holds at most 8 hours (10 for graduates; the server sends the limit).
  it('limits the study of one day',()=>{
    const monday=Array.from({length:8},(_,i)=>({...a,id:'m'+i,startMinute:480+i*60}));
    const extra={...a,id:'x',startMinute:1200,durationMinutes:30};
    expect(slotError(extra,[...monday,extra])).toBe('Pazartesi günü haftalık düzende 8 saatten fazla çalışma olamaz.');
    expect(slotError(extra,[...monday,extra],480)).toBe('Pazartesi günü haftalık düzende 8 saatten fazla çalışma olamaz.');
    expect(slotError(extra,[...monday,extra],600)).toBe('');
    expect(slotError(monday[0],monday)).toBe('');
    expect(slotError({...extra,day:'Tuesday'},[...monday,{...extra,day:'Tuesday'}])).toBe('');
    const ten=Array.from({length:10},(_,i)=>({...a,id:'t'+i,startMinute:420+i*60}));
    expect(slotError(extra,[...ten,extra],600)).toBe('Pazartesi günü haftalık düzende 10 saatten fazla çalışma olamaz.');
  });
  it('projects the same weekly template across month and year boundaries without UTC shifts',()=>{
    expect(mondayOf(new Date(2027,0,3)).getFullYear()).toBe(2026);
    expect(calendarDays(new Date(2027,0,3),'week').length).toBe(7);
    const month=calendarDays(new Date(2027,1,1),'month');expect(month.length%7).toBe(0);expect(month.filter(d=>d.getMonth()===1)).toHaveLength(28);
    expect(calendarDays(new Date(2027,0,3),'day')).toHaveLength(1);expect(clock(1440)).toBe('24:00');
  });
});
