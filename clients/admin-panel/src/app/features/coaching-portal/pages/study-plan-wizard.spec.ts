import { createStudyPlanProposal, createProposalFromWindows } from './study-plan-wizard';
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

  // E6 #29: the wizard plans from the saved availability (one source), with the same rules as the day/start/duration form.
  describe('from the saved availability', () => {
    it('fills Pazartesi 17:00–19:00 with the same 60/30 minute cards as the form', () => {
      const proposal = createProposalFromWindows([{ day: 'Monday', startMinute: 1020, endMinute: 1140 }], lessons, []);
      expect(proposal.map(x => [x.day, x.startMinute, x.durationMinutes, x.lessonId])).toEqual([
        ['Monday', 1020, 60, 'math'], ['Monday', 1080, 60, 'science']]);
      expect(createProposalFromWindows([{ day: 'Monday', startMinute: 1080, endMinute: 1170 }, { day: 'Wednesday', startMinute: 1080, endMinute: 1170 }], lessons, []))
        .toEqual(createStudyPlanProposal({ days: ['Monday', 'Wednesday'], startMinute: 1080, minutesPerDay: 90 }, lessons, [])
          .map(x => ({ ...x, id: expect.any(String) })));
    });

    it('goes through the week in order, keeps cards on the half hour and skips saved cards', () => {
      const existing: WeeklySlot[] = [{ id: 'saved', lessonId: 'math', lessonName: 'Matematik', examCode: 'LGS', day: 'Sunday', startMinute: 600, durationMinutes: 60 }];
      const proposal = createProposalFromWindows([
        { day: 'Sunday', startMinute: 600, endMinute: 720 }, { day: 'Tuesday', startMinute: 1035, endMinute: 1110 },
        { day: 'Tuesday', startMinute: 900, endMinute: 930 }], lessons, existing);
      expect(proposal.map(x => [x.day, x.startMinute, x.durationMinutes])).toEqual([
        ['Tuesday', 900, 30], ['Tuesday', 1050, 60], ['Sunday', 660, 60]]);
      expect(proposal[0].lessonId).toBe('science');
    });

    it('returns nothing without windows or lessons', () => {
      expect(createProposalFromWindows([], lessons, [])).toEqual([]);
      expect(createProposalFromWindows([{ day: 'Monday', startMinute: 1020, endMinute: 1140 }], [], [])).toEqual([]);
      expect(createProposalFromWindows([{ day: 'Monday', startMinute: 1030, endMinute: 1050 }], lessons, [])).toEqual([]);
    });
  });
});
