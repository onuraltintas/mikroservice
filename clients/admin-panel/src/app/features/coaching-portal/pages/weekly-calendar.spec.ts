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
    expect(slotError(extra,[...monday,extra])).toBe('Pazartesi günü için en çok 8 saat çalışma koyabilirsin.');
    expect(slotError(extra,[...monday,extra],{max:480})).toBe('Pazartesi günü için en çok 8 saat çalışma koyabilirsin.');
    expect(slotError(extra,[...monday,extra],{max:480,staff:true})).toBe('Pazartesi günü için en çok 8 saat çalışma konabilir.');
    expect(slotError(extra,[...monday,extra],{max:600})).toBe('');
    expect(slotError(monday[0],monday)).toBe('');
    expect(slotError({...extra,day:'Tuesday'},[...monday,{...extra,day:'Tuesday'}])).toBe('');
    const ten=Array.from({length:10},(_,i)=>({...a,id:'t'+i,startMinute:420+i*60}));
    expect(slotError(extra,[...ten,extra],{max:600})).toBe('Pazartesi günü için en çok 10 saat çalışma koyabilirsin.');
  });
  // Review of PR 129: a day saved above the limit stays editable; it may not rise above its saved total.
  it('keeps a day saved above the limit editable without letting it grow',()=>{
    const ten=Array.from({length:10},(_,i)=>({...a,id:'t'+i,startMinute:420+i*60}));
    const saved=ten;
    expect(slotError(ten[0],ten,{max:480,saved})).toBe('');
    const nine=ten.slice(1);
    expect(slotError(nine[0],nine,{max:480,saved})).toBe('');
    const moved={...ten[0],startMinute:1080,durationMinutes:30};const lower=[moved,...ten.slice(1)];
    expect(slotError(moved,lower,{max:480,saved})).toBe('');
    const extra={...a,id:'x',startMinute:1200,durationMinutes:30};
    expect(slotError(extra,[...ten,extra],{max:480,saved})).toBe('Pazartesi günü için en çok 8 saat çalışma koyabilirsin.');
    expect(slotError({...extra,day:'Tuesday'},[...ten,{...extra,day:'Tuesday'}],{max:480,saved})).toBe('');
  });
  it('projects the same weekly template across month and year boundaries without UTC shifts',()=>{
    expect(mondayOf(new Date(2027,0,3)).getFullYear()).toBe(2026);
    expect(calendarDays(new Date(2027,0,3),'week').length).toBe(7);
    const month=calendarDays(new Date(2027,1,1),'month');expect(month.length%7).toBe(0);expect(month.filter(d=>d.getMonth()===1)).toHaveLength(28);
    expect(calendarDays(new Date(2027,0,3),'day')).toHaveLength(1);expect(clock(1440)).toBe('24:00');
  });
});
