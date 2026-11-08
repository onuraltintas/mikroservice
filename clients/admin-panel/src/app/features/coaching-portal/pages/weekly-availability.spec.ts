import { cardsOutsideAvailability } from './weekly-availability';
import type { WeeklySlot } from './weekly-calendar';

describe('weekly cards and study times',()=>{
  const a:WeeklySlot={id:'a',lessonId:'math',lessonName:'Matematik',examCode:'LGS',day:'Monday',startMinute:1080,durationMinutes:60};
  // Paket H5 (K4: warn, allow): a card is inside only when a saved study time holds all of it; touching times count as one.
  it('finds the cards that are not fully inside the saved study times',()=>{
    const windows=[{day:'Monday',startMinute:1020,endMinute:1080},{day:'Monday',startMinute:1080,endMinute:1140},{day:'Tuesday',startMinute:1200,endMinute:1260}];
    const slots:WeeklySlot[]=[{...a,id:'joined',startMinute:1050,durationMinutes:60},{...a,id:'late',startMinute:1110,durationMinutes:60},
      {...a,id:'tuesday',day:'Tuesday',startMinute:1230,durationMinutes:30},{...a,id:'early',day:'Tuesday',startMinute:1170,durationMinutes:60},{...a,id:'wednesday',day:'Wednesday'}];
    expect(cardsOutsideAvailability(slots,windows).map(x=>x.id)).toEqual(['late','early','wednesday']);
    expect(cardsOutsideAvailability(slots,[])).toHaveLength(5);
    expect(cardsOutsideAvailability([{...a,startMinute:1380,durationMinutes:60}],[{day:'Monday',startMinute:1380,endMinute:1440}])).toEqual([]);
  });
});
