import { createStudyPlanProposal, createProposalFromWindows, lessonsWithoutTime, rankLessons, rankedWizardLessons } from './study-plan-wizard';
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
    // Paket G #41: with the same priority and no exam weight the name decides (Fen Bilimleri before Matematik).
    expect(proposal.map(x => x.lessonId)).toEqual(['science', 'math', 'science', 'math']);
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
        ['Monday', 1020, 60, 'science'], ['Monday', 1080, 60, 'math']]);
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

    it('never double-books when windows on the same day overlap', () => {
      const proposal = createProposalFromWindows([{ day: 'Monday', startMinute: 1020, endMinute: 1110 }, { day: 'Monday', startMinute: 1050, endMinute: 1140 }], lessons, []);
      const minutes = proposal.flatMap(x => Array.from({ length: x.durationMinutes / 30 }, (_, i) => x.startMinute + i * 30));
      expect(new Set(minutes).size).toBe(minutes.length);
      expect(minutes.sort((a, b) => a - b)).toEqual([1020, 1050, 1080, 1110]);
    });

    // Paket G #41 (live 07.11): 13 YKS Sayısal courses and Pazartesi–Cuma 17:00–18:00 gave the five hours to the first five
    // names (Biyoloji AYT, Biyoloji TYT, Coğrafya, Din Kültürü, Felsefe); Matematik and Türkçe got none.
    describe('order of the courses (#41)', () => {
      const week = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday'].map(day => ({ day, startMinute: 1020, endMinute: 1080 }));
      const yks = [
        { id: 'bio-ayt', name: 'Biyoloji (AYT)', examCode: 'AYT', examQuestions: 13 },
        { id: 'bio-tyt', name: 'Biyoloji (TYT)', examCode: 'TYT', examQuestions: 6 },
        { id: 'cog', name: 'Coğrafya (TYT)', examCode: 'TYT', examQuestions: 5 },
        { id: 'din', name: 'Din Kültürü ve Ahlak Bilgisi (TYT)', examCode: 'TYT', examQuestions: 5 },
        { id: 'fel', name: 'Felsefe (TYT)', examCode: 'TYT', examQuestions: 5 },
        { id: 'fiz-ayt', name: 'Fizik (AYT)', examCode: 'AYT', examQuestions: 14 },
        { id: 'kim-ayt', name: 'Kimya (AYT)', examCode: 'AYT', examQuestions: 13 },
        { id: 'mat-ayt', name: 'Matematik (AYT)', examCode: 'AYT', examQuestions: 40 },
        { id: 'mat-tyt', name: 'Matematik (TYT)', examCode: 'TYT', examQuestions: 40 },
        { id: 'tar', name: 'Tarih (TYT)', examCode: 'TYT', examQuestions: 5 },
        { id: 'tur', name: 'Türkçe (TYT)', examCode: 'TYT', examQuestions: 40 },
        { id: 'fiz-tyt', name: 'Fizik (TYT)', examCode: 'TYT', examQuestions: 7 },
        { id: 'kim-tyt', name: 'Kimya (TYT)', examCode: 'TYT', examQuestions: 7 }
      ];

      it('gives the hours to the student\'s priority first, not to the first names', () => {
        const lessons = yks.map(x => ({ ...x, priority: ['fel', 'din'].includes(x.id) ? 1 : 3 }));
        const proposal = createProposalFromWindows(week, lessons, []);
        expect(proposal.map(x => x.lessonId).slice(0, 2)).toEqual(['din', 'fel']);
        expect(proposal).toHaveLength(5);
        expect(proposal.every(x => x.durationMinutes === 60)).toBe(true);
      });

      it('breaks a tie of priority by the questions the course has in the exam, then by name', () => {
        const proposal = createProposalFromWindows(week, yks.map(x => ({ ...x, priority: 3 })), []);
        expect(proposal.map(x => [x.day, x.lessonId])).toEqual([
          ['Monday', 'mat-ayt'], ['Tuesday', 'mat-tyt'], ['Wednesday', 'tur'], ['Thursday', 'fiz-ayt'], ['Friday', 'bio-ayt']]);
      });

      // Review of PR 126: one course without a count must not switch the whole ranking to topic counts.
      it('ranks courses with a known count first, the others after them by topic count', () => {
        const lessons = [
          { id: 'mat', name: 'Matematik (TYT)', examCode: 'TYT', examQuestions: 40, topicCount: 60 },
          { id: 'fel', name: 'Felsefe (TYT)', examCode: 'TYT', examQuestions: null, topicCount: 50 },
          { id: 'din', name: 'Din Kültürü (TYT)', examCode: 'TYT', examQuestions: 5, topicCount: 40 },
          { id: 'tur', name: 'Türkçe (TYT)', examCode: 'TYT', examQuestions: 40, topicCount: 36 },
          { id: 'sec', name: 'Seçmeli', examCode: 'TYT', topicCount: 10 },
          { id: 'abc', name: 'Adsız', examCode: 'TYT' }];
        expect(rankLessons(lessons, []).map(x => x.id)).toEqual(['mat', 'tur', 'din', 'fel', 'sec', 'abc']);
        const proposal = createProposalFromWindows(week.slice(0, 2), lessons, []);
        expect(proposal.map(x => x.lessonId)).toEqual(['mat', 'tur']);
      });

      it('lists the courses without time in the order the proposal used', () => {
        const lessons = [
          { id: 'a', name: 'A', examCode: 'TYT', examQuestions: 40, topicCount: 1 },
          { id: 'b', name: 'B', examCode: 'TYT', examQuestions: null, topicCount: 50 },
          { id: 'c', name: 'C', examCode: 'TYT', examQuestions: 5, topicCount: 30 },
          { id: 'd', name: 'D', examCode: 'TYT', examQuestions: 4, topicCount: 1 }];
        const proposal = createProposalFromWindows(week.slice(0, 1), lessons, []);
        expect(proposal.map(x => x.lessonId)).toEqual(['a']);
        expect(lessonsWithoutTime(lessons, [], proposal).map(x => x.id)).toEqual(['c', 'd', 'b']);
      });

      it('keeps the least planned course first among equals and never moves saved cards', () => {
        const existing: WeeklySlot[] = [{ id: 'saved', lessonId: 'math', lessonName: 'Matematik', examCode: 'LGS', day: 'Monday', startMinute: 1020, durationMinutes: 60 }];
        const proposal = createProposalFromWindows([{ day: 'Monday', startMinute: 1020, endMinute: 1140 }], lessons, existing);
        expect(proposal.map(x => [x.startMinute, x.lessonId])).toEqual([[1080, 'science']]);
        // Hours added later go to the course still without time, even when the other one weighs more in the exam.
        expect(rankLessons([{ ...lessons[0], examQuestions: 40 }, { ...lessons[1], examQuestions: 20 }], existing).map(x => x.id)).toEqual(['science', 'math']);
        // A higher priority still comes first, even with saved time.
        const ranked = rankLessons([{ ...lessons[0], priority: 1 }, { ...lessons[1], priority: 2 }], existing);
        expect(ranked.map(x => x.id)).toEqual(['math', 'science']);
      });

      it('names the courses that got no time, the most important first', () => {
        const lessons = yks.map(x => ({ ...x, priority: 3 }));
        const proposal = createProposalFromWindows(week, lessons, []);
        const unplaced = lessonsWithoutTime(lessons, [], proposal);
        expect(unplaced).toHaveLength(8);
        expect(unplaced.map(x => x.id)).toEqual(['kim-ayt', 'fiz-tyt', 'kim-tyt', 'bio-tyt', 'cog', 'din', 'fel', 'tar']);
        expect(lessonsWithoutTime(lessons.slice(0, 2), [{ ...proposal[0], lessonId: 'BIO-AYT' }], []).map(x => x.id)).toEqual(['bio-tyt']);
      });

      it('warns in development about a chosen course the server could not weight', () => {
        const warn = vi.spyOn(console, 'warn').mockImplementation(() => undefined);
        rankedWizardLessons([{ id: 'x', name: 'Seçmeli Okuma', examCode: 'LGS', gradeNumber: 8 }, { id: 'y', name: 'Türkçe', examCode: 'LGS', gradeNumber: 8 }],
          [{ lessonId: 'x', priority: 3, examQuestions: null }, { lessonId: 'y', priority: 3, examQuestions: 20 }]);
        expect(warn).toHaveBeenCalledOnce();
        expect(String(warn.mock.calls[0][0])).toContain('Seçmeli Okuma');
        warn.mockRestore();
      });

      it('takes the priority and the exam weight from the weekly draft page', () => {
        const merged = rankedWizardLessons([{ id: 'A', name: 'A', examCode: 'TYT', gradeNumber: null, topicCount: 3 }, { id: 'b', name: 'B', examCode: 'TYT', gradeNumber: null }],
          [{ lessonId: 'a', priority: 2, examQuestions: 40 }]);
        expect(merged).toEqual([
          { id: 'A', name: 'A', examCode: 'TYT', priority: 2, examQuestions: 40, topicCount: 3 },
          { id: 'b', name: 'B', examCode: 'TYT', priority: 3, examQuestions: null, topicCount: undefined }]);
      });
    });

    it('returns nothing without windows or lessons', () => {
      expect(createProposalFromWindows([], lessons, [])).toEqual([]);
      expect(createProposalFromWindows([{ day: 'Monday', startMinute: 1020, endMinute: 1140 }], [], [])).toEqual([]);
      expect(createProposalFromWindows([{ day: 'Monday', startMinute: 1030, endMinute: 1050 }], lessons, [])).toEqual([]);
    });
  });
});
