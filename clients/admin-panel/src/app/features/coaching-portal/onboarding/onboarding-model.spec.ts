import { OnboardingView } from '../../../core/services/coaching-onboarding.service';
import {
  Answers, FlowContext, change, currentStep, purposeChoices, withAnswer, examRequest, nextQuestion, personalRequest, prefill, questions, scoreTargetTypes, signature,
  stepComplete, summary, targetRequest
} from './onboarding-model';

describe('first-login wizard flow (Paket C #3)', () => {
  const context: FlowContext = { academicYearStart: 2026, sessions: [
    { id: 'lgs-27', examCode: 'LGS', examName: 'LGS', date: '2027-06-13', label: '2027', applicationStart: null, applicationEnd: null }] };
  const a = (value: string | number | null, label = String(value), data?: Record<string, unknown>) => ({ value, label, data });
  const personal: Answers = { birthDate: a('2012-05-01', '01.05.2012'), city: a('06', 'Ankara'), district: a('0601', 'Çankaya'), education: a('8', '8. sınıf') };
  const ids = (answers: Answers) => questions(answers, context).map(x => x.id);

  it('offers only the exams that fit the grade and asks the exam again when a new grade no longer fits it (Paket D #8)', () => {
    expect(purposeChoices(8).map(x => x.value)).toEqual(['Lgs']);
    expect(purposeChoices(9).map(x => x.value)).toEqual(['Yks', 'Ags', 'Yds', 'Yokdil']);
    expect(purposeChoices('graduate').map(x => x.value)).toEqual(['Yks', 'Ags', 'Yds', 'Yokdil']);
    expect(purposeChoices(null).length).toBe(5);
    expect(questions({ ...personal, education: a('10') }, context).find(x => x.id === 'purpose')!.choices!.map(x => x.value)).not.toContain('Lgs');
    const lgs: Answers = { ...personal, purpose: a('Lgs'), session: a('session:lgs-27:2027'), targetMode: a('school') };
    const older = withAnswer(lgs, 'education', a('9', '9. sınıf'));
    expect(older.education!.value).toBe('9');
    expect(older.purpose).toBeUndefined(); expect(older.session).toBeUndefined(); expect(older.targetMode).toBeUndefined();
    expect(withAnswer(lgs, 'education', a('7', '7. sınıf')).purpose!.value).toBe('Lgs');
  });

  it('asks one question at a time, each with a reason, and every answer comes from a component', () => {
    expect(nextQuestion({}, context)!.id).toBe('birthDate');
    expect(nextQuestion({ birthDate: personal.birthDate }, context)!.id).toBe('city');
    for (const question of questions({ ...personal, purpose: a('Yks'), yksField: a('SAY'), targetMode: a('program'), level: a('Bachelor') }, context)) {
      expect(question.reason.length).toBeGreaterThan(10);
      expect(['date', 'buttons', 'combobox', 'number', 'diploma', 'invite', 'parent', 'notifications']).toContain(question.kind);
    }
  });

  it('narrows the long lists question by question: LGS city → type → school; YKS level → score type → city → university → programme', () => {
    const lgs = { ...personal, purpose: a('Lgs'), session: a('session:lgs-27:2027'), targetMode: a('school') };
    expect(ids(lgs).filter(x => ['schoolCity', 'schoolType', 'targetSchool'].includes(x))).toEqual(['schoolCity']);
    expect(ids({ ...lgs, schoolCity: a('Ankara') })).toContain('schoolType');
    expect(ids({ ...lgs, schoolCity: a('Ankara'), schoolType: a('Fen Lisesi') })).toContain('targetSchool');
    const yks = { ...personal, education: a('12'), ownSchool: a(null), purpose: a('Yks'), yksField: a('SAY'), session: a('year:2027'), diploma: a(null), targetMode: a('program') };
    expect(nextQuestion(yks, context)!.id).toBe('level');
    expect(nextQuestion({ ...yks, level: a('Bachelor') }, context)!.choices).toEqual([{ value: 'SAY', label: 'Sayısal' }]);
    expect(nextQuestion({ ...yks, level: a('Associate') }, context)!.id).toBe('programCity');
    const chosen = { ...yks, level: a('Bachelor'), programScoreType: a('SAY'), programCity: a('Ankara'), university: a('Ankara Üniversitesi') };
    expect(nextQuestion(chosen, context)!.id).toBe('program');
    // A programme without a taban asks for the student's own target score.
    expect(nextQuestion({ ...chosen, program: a('p1', 'Fizik', { minimumScore: null }) }, context)!.id).toBe('ownScore');
    expect(nextQuestion({ ...chosen, program: a('p2', 'Bilgisayar', { minimumScore: 470 }) }, context)!.id).toBe('invite');
    // Önlisans only for a TYT-only student.
    expect(nextQuestion({ ...yks, yksField: a('TYT') }, context)!.choices).toEqual([{ value: 'Associate', label: 'Önlisans' }]);
  });

  it('asks the exam details each exam needs, the diploma grade only from 11th grade on YKS, and the session or the year', () => {
    expect(ids({ ...personal, purpose: a('Yks'), yksField: a('DIL') })).toContain('language');
    expect(ids({ ...personal, purpose: a('Yks'), yksField: a('SAY') })).not.toContain('language');
    expect(ids({ ...personal, purpose: a('Ags') })).toContain('oabtField');
    expect(ids({ ...personal, purpose: a('Yokdil') })).toContain('yokdilField');
    expect(ids({ ...personal, purpose: a('Yks'), yksField: a('SAY') })).not.toContain('diploma');
    expect(ids({ ...personal, education: a('11'), purpose: a('Yks'), yksField: a('SAY') })).toContain('diploma');
    expect(ids({ ...personal, education: a('graduate'), purpose: a('Yks'), yksField: a('SAY') })).toContain('diploma');
    const lgsSession = questions({ ...personal, purpose: a('Lgs') }, context).find(x => x.id === 'session')!;
    expect(lgsSession.choices).toEqual([{ value: 'session:lgs-27:2027', label: '2027 · 13.06.2027' }]);
    const ydsYear = questions({ ...personal, purpose: a('Yds') }, context).find(x => x.id === 'session')!;
    expect(ydsYear.prompt).toBe('Hangi yıl sınava gireceksin?'); expect(ydsYear.choices!.map(x => x.value)).toEqual(['year:2027', 'year:2028', 'year:2029']);
  });

  it('offers the right score types for a score-only target', () => {
    expect(scoreTargetTypes({ purpose: a('Yks'), yksField: a('EA') }).map(x => x.value)).toEqual(['TYT', 'EA']);
    expect(scoreTargetTypes({ purpose: a('Yks'), yksField: a('TYT') }).map(x => x.value)).toEqual(['TYT']);
    expect(scoreTargetTypes({ purpose: a('Ags'), oabtField: a('OABT_IMAT') }).map(x => x.value)).toEqual(['P2_IMAT']);
    expect(scoreTargetTypes({ purpose: a('Ags'), oabtField: a('OABT_DIN') }).map(x => x.value)).toEqual(['P2_16', 'P2_17']);
    expect(scoreTargetTypes({ purpose: a('Ags'), oabtField: a('none') }).map(x => x.value)).toEqual(['P1']);
    expect(scoreTargetTypes({ purpose: a('Yokdil'), yokdilField: a('YOKDIL_SAG') }).map(x => x.value)).toEqual(['YOKDIL_SAG']);
    // YDS: the level shortcut comes before the score.
    const yds = { ...personal, purpose: a('Yds'), session: a('year:2027'), targetMode: a('score'), targetScoreType: a('YDS_EN') };
    expect(nextQuestion(yds, context)!.id).toBe('ydsLevel');
    expect(nextQuestion(yds, context)!.choices!.map(x => x.label)).toEqual(['A (90–100)', 'B (80–89)', 'C (70–79)', 'D (60–69)', 'E (50–59)']);
    expect(nextQuestion({ ...yds, ydsLevel: a('80') }, context)!).toMatchObject({ id: 'targetScore', max: 100 });
  });

  it('a YKS score target asks TYT and the field score; one of them may be skipped, not both', () => {
    const yks = { ...personal, education: a('11'), ownSchool: a(null), purpose: a('Yks'), yksField: a('EA'), session: a('year:2027'), diploma: a(null), targetMode: a('score') };
    const tyt = nextQuestion(yks, context)!;
    expect(tyt).toMatchObject({ id: 'tytScore', skipLabel: 'TYT hedefim yok', max: 560 });
    expect(tyt.reason).toContain("0'dan büyük");
    const field = nextQuestion({ ...yks, tytScore: a(null) }, context)!;
    expect(field).toMatchObject({ id: 'fieldScore', prompt: 'Eşit Ağırlık (EA) hedef puanın kaç?' });
    expect(field.skipLabel).toBeUndefined();
    expect(nextQuestion({ ...yks, tytScore: a(380) }, context)!.skipLabel).toBe('Bu puanı şimdi girmeyeceğim');
    expect(nextQuestion({ ...yks, yksField: a('TYT'), tytScore: undefined }, context)!.skipLabel).toBeUndefined();
    expect(change({ ...yks, tytScore: a(380), fieldScore: a(400) }, 'tytScore').fieldScore).toBeUndefined();
  });

  it('an older score target without a score type is asked again instead of sending an invalid request', () => {
    const view: OnboardingView = { status: 'InProgress', nextStep: 4, savedSteps: [1, 2, 3], completedAt: null, academicYearStart: 2026, examSessions: [],
      answers: { birthDate: '2012-05-01', homeProvinceId: '06', homeCity: 'Ankara', homeDistrictId: '0601', homeDistrict: 'Çankaya', educationStatus: 'Student',
        gradeLevel: 8, graduationYear: null, schoolName: null, purpose: 'Lgs', primaryScoreType: null, foreignLanguage: null, targetExamSessionId: null,
        targetExamYear: 2027, diplomaGrade: null, diplomaGradeEstimated: false,
        target: { kind: 'score', targetId: null, name: 'Eski hedef', detail: null, minimumScore: null, scoreYear: null, scoreType: null, scores: [{ scoreType: '', score: 400 }], canEdit: true } } };
    const answers = prefill(view);
    expect(answers.targetSaved).toBeUndefined();
    expect(nextQuestion(answers, context)!.id).toBe('targetMode');
  });

  it('a teacher\'s goal is shown as set and is not sent', () => {
    const view: OnboardingView = { status: 'InProgress', nextStep: 3, savedSteps: [1, 2], completedAt: null, academicYearStart: 2026, examSessions: [],
      answers: { birthDate: '2012-05-01', homeProvinceId: '06', homeCity: 'Ankara', homeDistrictId: '0601', homeDistrict: 'Çankaya', educationStatus: 'Student',
        gradeLevel: 8, graduationYear: null, schoolName: null, purpose: 'Lgs', primaryScoreType: null, foreignLanguage: null, targetExamSessionId: null,
        targetExamYear: 2027, diplomaGrade: null, diplomaGradeEstimated: false, targetLocked: true,
        target: { kind: 'school', targetId: 's1', name: 'Ankara Fen Lisesi', detail: null, minimumScore: 480, scoreYear: 2025, scoreType: 'LGS', scores: [], canEdit: false } } };
    const answers = prefill(view);
    expect(answers.targetSaved!.label).toBe('Hedefini öğretmenin belirledi: Ankara Fen Lisesi · taban 480');
    expect(targetRequest(answers)).toEqual({ kind: 'none', targetId: null, scores: null });
  });

  it('"Değiştir" clears only the dependent answers', () => {
    const full: Answers = { ...personal, purpose: a('Lgs'), session: a('session:lgs-27:2027'), targetMode: a('school'), schoolCity: a('Ankara'),
      schoolType: a('Fen Lisesi'), targetSchool: a('s1', 'Ankara Fen Lisesi'), invite: a('passed'), parent: a('passed') };
    const exam = change(full, 'purpose');
    expect(Object.keys(exam).sort()).toEqual(['birthDate', 'city', 'district', 'education', 'invite', 'parent']);
    const city = change(full, 'schoolCity');
    expect(city.targetMode).toBeDefined(); expect(city.schoolType).toBeUndefined(); expect(city.targetSchool).toBeUndefined();
    const home = change(full, 'city');
    expect(home.district).toBeUndefined(); expect(home.purpose).toBeDefined(); expect(home.targetSchool).toBeDefined();
    expect(change(full, 'birthDate').city).toBeDefined();
  });

  it('builds each step request', () => {
    expect(personalRequest({ ...personal, education: a('graduate') })).toEqual({ birthDate: '2012-05-01', provinceId: '06', districtId: '0601',
      educationStatus: 'Graduate', gradeLevel: null, graduationYear: null, schoolId: null });
    // A picked school sends its catalog id; a resumed (saved) school sends nothing and stays.
    expect(personalRequest({ ...personal, education: a('10'), ownSchool: a('school-1', 'Ankara Lisesi') })).toMatchObject({ educationStatus: 'Student', gradeLevel: 10, schoolId: 'school-1' });
    expect(personalRequest({ ...personal, education: a('10'), ownSchool: a(null, 'Ankara Lisesi') }).schoolId).toBeNull();
    expect(examRequest({ ...personal, education: a('12'), purpose: a('Yks'), yksField: a('DIL'), language: a('English'), session: a('session:s1:2027'),
      diploma: a(82.5, '82,5 (tahmini)', { estimated: true }) })).toEqual({ purpose: 'Yks', primaryScoreType: 'DIL', foreignLanguage: 'English',
      targetExamSessionId: 's1', targetExamYear: 2027, diplomaGrade: 82.5, diplomaGradeEstimated: true });
    expect(examRequest({ ...personal, purpose: a('Ags'), oabtField: a('none'), session: a('year:2028') })).toMatchObject({ primaryScoreType: null,
      targetExamSessionId: null, targetExamYear: 2028, diplomaGrade: null });
    expect(targetRequest({ targetMode: a('none') })).toEqual({ kind: 'none', targetId: null, scores: null });
    expect(targetRequest({ targetMode: a('school'), targetSchool: a('s1') })).toEqual({ kind: 'school', targetId: 's1', scores: null });
    expect(targetRequest({ targetMode: a('program'), program: a('p1'), ownScore: a(410) })).toEqual({ kind: 'program', targetId: 'p1', scores: [{ scoreType: '', score: 410 }] });
    expect(targetRequest({ purpose: a('Ags'), targetMode: a('score'), targetScoreType: a('P2_16'), targetScore: a(72.5) })).toEqual({ kind: 'score', targetId: null, scores: [{ scoreType: 'P2_16', score: 72.5 }] });
    // YKS: TYT and the field's score; either may be left out.
    const yks = { purpose: a('Yks'), yksField: a('SAY'), targetMode: a('score') };
    expect(targetRequest({ ...yks, tytScore: a(380), fieldScore: a(450) }).scores).toEqual([{ scoreType: 'TYT', score: 380 }, { scoreType: 'SAY', score: 450 }]);
    expect(targetRequest({ ...yks, tytScore: a(null), fieldScore: a(450) }).scores).toEqual([{ scoreType: 'SAY', score: 450 }]);
    expect(targetRequest({ ...yks, tytScore: a(380), fieldScore: a(null) }).scores).toEqual([{ scoreType: 'TYT', score: 380 }]);
  });

  it('resumes from the server: saved steps count as answered, the progress shows the step', () => {
    const view: OnboardingView = { status: 'InProgress', nextStep: 3, savedSteps: [1, 2], completedAt: null, academicYearStart: 2026, examSessions: context.sessions,
      answers: { birthDate: '2012-05-01', homeProvinceId: '06', homeCity: 'Ankara', homeDistrictId: '0601', homeDistrict: 'Çankaya', educationStatus: 'Student',
        gradeLevel: 8, graduationYear: null, schoolName: null, purpose: 'Lgs', primaryScoreType: null, foreignLanguage: null, targetExamSessionId: 'lgs-27',
        targetExamYear: 2027, diplomaGrade: null, diplomaGradeEstimated: false, target: null } };
    const answers = prefill(view);
    expect(stepComplete(answers, context, 1)).toBe(true); expect(stepComplete(answers, context, 2)).toBe(true);
    expect(nextQuestion(answers, context)!.id).toBe('targetMode');
    expect(currentStep(answers, context)).toBe(3);
    const done = prefill({ ...view, savedSteps: [1, 2, 3, 4, 5, 6], answers: { ...view.answers,
      target: { kind: 'school', targetId: 's1', name: 'Ankara Fen Lisesi', detail: 'Ankara / Çankaya', minimumScore: 480, scoreYear: 2025, scoreType: 'LGS', scores: [], canEdit: true } } });
    expect(currentStep(done, context)).toBe(7);
    expect(targetRequest(done)).toEqual({ kind: 'school', targetId: 's1', scores: null });
    expect(summary(done, context).find(x => x.step === 3)!.lines).toEqual(['Hedef: Ankara Fen Lisesi · taban 480']);
    // A step whose answers changed has a new signature, so it is sent again.
    expect(signature(change(done, 'targetSaved'), context, 3)).not.toBe(signature(done, context, 3));
  });
});
