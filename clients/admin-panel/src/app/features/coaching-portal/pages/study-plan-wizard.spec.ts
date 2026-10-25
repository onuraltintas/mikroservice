import { createStudyPlanProposal } from './study-plan-wizard';
import { WeeklySlot } from './weekly-calendar';

describe('createStudyPlanProposal', () => {
  const lessons = [
    { id: 'math', name: 'Matematik', examCode: 'LGS' },
    { id: 'science', name: 'Fen Bilimleri', examCode: 'LGS' }
  ];

  it('splits available study time into 30 or 60 minute lesson cards over chosen days', () => {
    const proposal = createStudyPlanProposal({ days: ['Monday', 'Wednesday'], startMinute: 1080, minutesPerDay: 90 }, lessons, []);

    expect(proposal).toHaveLength(4);
    expect(proposal.map(x => [x.day, x.durationMinutes])).toEqual([
      ['Monday', 60], ['Monday', 30], ['Wednesday', 60], ['Wednesday', 30]
    ]);
    expect(proposal.map(x => x.lessonId)).toEqual(['math', 'science', 'math', 'science']);
  });

  it('skips time occupied by an existing plan without changing saved cards', () => {
    const existing: WeeklySlot[] = [{ id: 'saved', lessonId: 'math', lessonName: 'Matematik', examCode: 'LGS',
      day: 'Monday', startMinute: 1080, durationMinutes: 60 }];

    const proposal = createStudyPlanProposal({ days: ['Monday'], startMinute: 1080, minutesPerDay: 120 }, lessons, existing);

    expect(proposal.map(x => [x.startMinute, x.durationMinutes])).toEqual([[1140, 60]]);
    expect(existing).toEqual([expect.objectContaining({ id: 'saved', startMinute: 1080, durationMinutes: 60 })]);
  });

  it('returns no proposal when the selection is empty or the requested window exceeds the day', () => {
    expect(createStudyPlanProposal({ days: [], startMinute: 1080, minutesPerDay: 60 }, lessons, [])).toEqual([]);
    expect(createStudyPlanProposal({ days: ['Monday'], startMinute: 1410, minutesPerDay: 60 }, lessons, [])).toEqual([]);
  });
});
