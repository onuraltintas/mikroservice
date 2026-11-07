import { OnboardingView } from '../../../core/services/coaching-onboarding.service';
import {
  Answers, FlowContext, change, currentStep, withoutUnfitPurpose, examRequest, nextQuestion, personalRequest, prefill, questions, scoreTargetTypes, signature,
  stepComplete, summary, targetRequest, inviteAnswer, parentAnswer, emailAnswer, parentCodeNote, staleParentCode,
  cleanSchoolName, typedSchoolAnswer, typedSchoolError
} from './onboarding-model';

describe('first-login wizard flow (Paket C #3)', () => {
  const context: FlowContext = { academicYearStart: 2026, sessions: [
    { id: 'lgs-27', examCode: 'LGS', examName: 'LGS', date: '2027-06-13', label: '2027', applicationStart: null, applicationEnd: null }] };
  const a = (value: string | number | null, label = String(value), data?: Record<string, unknown>) => ({ value, label, data });
  const personal: Answers = { birthDate: a('2012-05-01', '01.05.2012'), city: a('06', 'Ankara'), district: a('0601', 'Çankaya'), stage: a('middle', 'Ortaokul öğrencisiyim'), education: a('8', '8. sınıf') };
  const lise = { stage: a('high', 'Lise öğrencisiyim') };
  const ids = (answers: Answers) => questions(answers, context).map(x => x.id);

  it('offers only the exams the server allows for the grade and does not prefill one that no longer fits (Paket D #8, review M1)', () => {
    const allowed: FlowContext = { ...context, allowedPurposes: ['Yks', 'Yds', 'Yokdil'] };
    expect(questions({ ...personal, ...lise, education: a('10') }, allowed).find(x => x.id === 'purpose')!.choices!.map(x => x.value)).toEqual(['Yks', 'Yds', 'Yokdil']);
    // An older API sends no list: every exam is offered.
    expect(questions(personal, context).find(x => x.id === 'purpose')!.choices!.length).toBe(5);
    const saved = { status: 'InProgress', nextStep: 2, savedSteps: [1, 2], completedAt: null, academicYearStart: 2026, examSessions: [], allowedPurposes: ['Yks', 'Yds', 'Yokdil'],
      answers: { birthDate: '2009-05-01', homeProvinceId: '06', homeCity: 'Ankara', homeDistrictId: '0601', homeDistrict: 'Çankaya', educationStatus: 'Student', gradeLevel: 11,
        graduationYear: null, schoolName: null, purpose: 'Lgs', primaryScoreType: null, foreignLanguage: null, targetExamSessionId: 'lgs-27', targetExamYear: 2027,
        diplomaGrade: null, diplomaGradeEstimated: false, target: null, targetLocked: false } } as unknown as OnboardingView;
    const answers = prefill(saved);
    expect(answers.purpose).toBeUndefined(); expect(answers.session).toBeUndefined();
    expect(answers.education!.value).toBe('11'); expect(answers.stage!.value).toBe('high');
    expect(withoutUnfitPurpose({ ...personal, purpose: a('Lgs'), session: a('session:lgs-27:2027') }, ['Yks']).purpose).toBeUndefined();
    expect(withoutUnfitPurpose({ ...personal, purpose: a('Lgs') }, ['Lgs']).purpose!.value).toBe('Lgs');
    expect(withoutUnfitPurpose({ ...personal, purpose: a('Lgs') }, undefined).purpose!.value).toBe('Lgs');
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
    const yks = { ...personal, ...lise, education: a('12'), ownSchool: a(null), purpose: a('Yks'), yksField: a('SAY'), session: a('year:2027'), diploma: a(null), targetMode: a('program') };
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
    expect(ids({ ...personal, ...lise, education: a('11'), purpose: a('Yks'), yksField: a('SAY') })).toContain('diploma');
    expect(ids({ ...personal, stage: a('HighSchoolGraduate'), education: undefined, purpose: a('Yks'), yksField: a('SAY') })).toContain('diploma');
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
    const yks = { ...personal, ...lise, education: a('11'), ownSchool: a(null), purpose: a('Yks'), yksField: a('EA'), session: a('year:2027'), diploma: a(null), targetMode: a('score') };
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
    expect(Object.keys(exam).sort()).toEqual(['birthDate', 'city', 'district', 'education', 'invite', 'parent', 'stage']);
    const city = change(full, 'schoolCity');
    expect(city.targetMode).toBeDefined(); expect(city.schoolType).toBeUndefined(); expect(city.targetSchool).toBeUndefined();
    const home = change(full, 'city');
    expect(home.district).toBeUndefined(); expect(home.purpose).toBeDefined(); expect(home.targetSchool).toBeDefined();
    expect(change(full, 'birthDate').city).toBeDefined();
  });

  // G4 (#43): "Okulum listede yok".
  it('sends a typed school as its name (trimmed), only for a student and only without a picked school', () => {
    const typed = typedSchoolAnswer('  Özel   Deneme  Koleji ');
    expect(typed).toEqual({ value: null, label: 'Özel Deneme Koleji', data: { typed: 'Özel Deneme Koleji' } });
    expect(personalRequest({ ...personal, ...lise, education: a('10'), ownSchool: typed })).toMatchObject({ schoolId: null, schoolName: 'Özel Deneme Koleji' });
    expect(personalRequest({ ...personal, ...lise, education: a('10'), ownSchool: a('school-1', 'Ankara Lisesi') })).not.toHaveProperty('schoolName');
    expect(personalRequest({ ...personal, ...lise, education: a('10'), ownSchool: a(null, 'Ankara Lisesi') })).not.toHaveProperty('schoolName');
    expect(personalRequest({ ...personal, stage: a('HighSchoolGraduate'), education: undefined, ownSchool: typed })).not.toHaveProperty('schoolName');
    expect(cleanSchoolName(' a \n b ')).toBe('a b');
    expect(typedSchoolError('   ')).toBe('Okulunun adını yaz.');
    expect(typedSchoolError('x'.repeat(151))).toBe('Okulunun adı en fazla 150 karakter olabilir.');
    expect(typedSchoolError(` ${'x'.repeat(150)} `)).toBeNull();
    // Review L2: zero-width and other format characters are dropped; a name of only those is refused.
    expect(typedSchoolError('\u200B\u200C \uFEFF')).toBe('Okulunun adını yaz.');
    expect(cleanSchoolName('Deneme\u200B Lisesi')).toBe('Deneme Lisesi');
    // The typed name changes the step's signature, so the step is sent again.
    expect(signature({ ...personal, ...lise, education: a('10'), ownSchool: typed }, context, 1))
      .not.toBe(signature({ ...personal, ...lise, education: a('10'), ownSchool: a(null, 'Özel Deneme Koleji') }, context, 1));
  });

  it('builds each step request', () => {
    expect(personalRequest({ ...personal, stage: a('HighSchoolGraduate'), education: undefined })).toEqual({ birthDate: '2012-05-01', provinceId: '06', districtId: '0601',
      educationStatus: 'HighSchoolGraduate', gradeLevel: null, graduationYear: null, schoolId: null });
    // A picked school sends its catalog id; a resumed (saved) school sends nothing and stays.
    expect(personalRequest({ ...personal, ...lise, education: a('10'), ownSchool: a('school-1', 'Ankara Lisesi') })).toMatchObject({ educationStatus: 'Student', gradeLevel: 10, schoolId: 'school-1' });
    expect(personalRequest({ ...personal, ...lise, education: a('10'), ownSchool: a(null, 'Ankara Lisesi') }).schoolId).toBeNull();
    expect(examRequest({ ...personal, ...lise, education: a('12'), purpose: a('Yks'), yksField: a('DIL'), language: a('English'), session: a('session:s1:2027'),
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
  // E3 #14 (user decision 06.11.2026): "Şu an durumun ne?" first; the grade only for ortaokul (5–8) and lise (9–12) students.
  it('asks the education status, then the grade that fits it', () => {
    const start: Answers = { birthDate: personal.birthDate, city: personal.city, district: personal.district };
    const stage = questions(start, context).find(x => x.id === 'stage')!;
    expect(stage.prompt).toBe('Şu an durumun ne?');
    expect(stage.choices!.map(x => x.label)).toEqual(['Ortaokul öğrencisiyim', 'Lise öğrencisiyim', 'Liseden mezunum', 'Üniversite öğrencisiyim', 'Üniversiteden mezunum']);
    expect(questions(start, context).some(x => x.prompt === 'Kaçıncı sınıftasın?')).toBe(false);
    const grade = (value: string) => questions({ ...start, stage: a(value) }, context).find(x => x.id === 'education');
    expect(grade('middle')!.choices!.map(x => x.value)).toEqual(['5', '6', '7', '8']);
    expect(grade('high')!.choices!.map(x => x.value)).toEqual(['9', '10', '11', '12']);
    for (const other of ['HighSchoolGraduate', 'UniversityStudent', 'UniversityGraduate']) {
      expect(grade(other)).toBeUndefined();
      expect(ids({ ...start, stage: a(other) })).not.toContain('ownSchool');
      expect(personalRequest({ ...start, stage: a(other) })).toMatchObject({ educationStatus: other, gradeLevel: null, schoolId: null });
    }
    // The own school list is the high-school catalog: asked of lise students only.
    expect(ids({ ...start, ...lise, education: a('9') })).toContain('ownSchool');
    expect(ids({ ...start, stage: a('middle'), education: a('7') })).not.toContain('ownSchool');
    expect(personalRequest({ ...start, stage: a('middle'), education: a('7') })).toMatchObject({ educationStatus: 'Student', gradeLevel: 7 });
    // A changed status asks the grade, the school and the diploma grade again.
    const changed = change({ ...start, ...lise, education: a('11'), ownSchool: a(null), diploma: a(80) }, 'stage');
    expect([changed.education, changed.ownSchool, changed.diploma]).toEqual([undefined, undefined, undefined]);
  });

  it('asks the YKS diploma grade in 11th and 12th grade and of everyone out of high school', () => {
    const yks = { ...personal, purpose: a('Yks'), yksField: a('SAY'), education: undefined };
    for (const stage of ['HighSchoolGraduate', 'UniversityStudent', 'UniversityGraduate'])
      expect(ids({ ...yks, stage: a(stage) })).toContain('diploma');
    expect(ids({ ...yks, ...lise, education: a('12') })).toContain('diploma');
    expect(ids({ ...yks, ...lise, education: a('10') })).not.toContain('diploma');
    expect(ids({ ...yks, stage: a('UniversityGraduate'), purpose: a('Yds') })).not.toContain('diploma');
  });

  it('resumes every status from the server', () => {
    const view = (educationStatus: string, gradeLevel: number | null) => ({ status: 'InProgress', nextStep: 2, savedSteps: [1], completedAt: null, academicYearStart: 2026,
      examSessions: [], answers: { birthDate: '2000-05-01', homeProvinceId: '06', homeCity: 'Ankara', homeDistrictId: '0601', homeDistrict: 'Çankaya', educationStatus,
        gradeLevel, graduationYear: null, schoolName: null, purpose: null, primaryScoreType: null, foreignLanguage: null, targetExamSessionId: null, targetExamYear: null,
        diplomaGrade: null, diplomaGradeEstimated: false, target: null } }) as unknown as OnboardingView;
    expect([prefill(view('Student', 7)).stage!.value, prefill(view('Student', 7)).education!.value]).toEqual(['middle', '7']);
    expect([prefill(view('Student', 10)).stage!.value, prefill(view('Student', 10)).education!.value]).toEqual(['high', '10']);
    for (const status of ['HighSchoolGraduate', 'UniversityStudent', 'UniversityGraduate']) {
      const answers = prefill(view(status, null));
      expect([answers.stage!.value, answers.education]).toEqual([status, undefined]);
      expect(stepComplete(answers, context, 1)).toBe(true);
    }
    expect(prefill(view('Unknown', null)).stage).toBeUndefined();
    expect(summary(prefill(view('UniversityStudent', null)), context).find(x => x.step === 1)!.lines).toContain('Eğitim durumu: Üniversite öğrencisiyim');
  });

  // E4 #18 (live test 06.11.2026): after a reload the answers of steps 4–6 are rebuilt from the server, not "Tamam".
  it('rebuilds the invite, parent and e-mail answers of a resumed wizard from what the server has', () => {
    const view: OnboardingView = { status: 'InProgress', nextStep: 7, savedSteps: [1, 2, 3, 4, 5, 6], completedAt: null, academicYearStart: 2026, examSessions: context.sessions,
      answers: { birthDate: '2012-05-01', homeProvinceId: '06', homeCity: 'Ankara', homeDistrictId: '0601', homeDistrict: 'Çankaya', educationStatus: 'Student',
        gradeLevel: 8, graduationYear: null, schoolName: null, purpose: 'Lgs', primaryScoreType: null, foreignLanguage: null, targetExamSessionId: 'lgs-27',
        targetExamYear: 2027, diplomaGrade: null, diplomaGradeEstimated: false, target: null },
      acceptedInvitations: [{ inviterEmail: 'ogretmen@example.test', type: 'StudentToTeacher', acceptedAt: '2026-11-06T09:00:00Z' }] };
    const answers = prefill(view);
    expect(answers.invite).toEqual({ value: 'passed', label: 'Daveti kabul ettim: ogretmen@example.test' });
    expect(prefill({ ...view, acceptedInvitations: [] }).invite!.label).toBe('Davetim yok');
    // Steps 5 and 6 come from Identity and Notification; until they answer the bubble does not pretend to know.
    expect(answers.parent!.label).not.toBe('Tamam'); expect(answers.notifications!.label).not.toBe('Tercihlerim kaydedildi');

    expect(inviteAnswer([{ inviterEmail: 'kurum@example.test', type: 'StudentToInstitution', acceptedAt: '2026-11-06T09:00:00Z' },
      { inviterEmail: null, type: 'StudentToTeacher', acceptedAt: '2026-11-06T09:00:00Z' }])).toBe('Daveti kabul ettim: kurum@example.test (kurum), bir öğretmen');
    expect(parentAnswer({ expiresAt: '2026-11-13T09:00:00Z' }, [])).toBe('Veli kodu oluşturdum (13.11.2026 tarihine kadar geçerli)');
    expect(parentAnswer(null, [{ parentName: 'Ayşe Veli', status: 'Verified' }, { parentName: 'Can Veli', status: 'Pending' }]))
      .toBe('Velimle bağlantım var: Ayşe Veli, Can Veli (onayımı bekliyor)');
    expect(parentAnswer(null, [])).toBe('Şimdilik geç');
    // F2 (E4 c): an expired code is named for a week; a code whose expiry is long past is not.
    vi.useFakeTimers({ toFake: ['Date'] }); vi.setSystemTime(new Date('2026-11-03T12:00:00Z'));
    expect(parentAnswer({ expiresAt: '2026-11-01T09:00:00Z', expired: true }, [])).toBe('Veli kodumun süresi doldu (01.11.2026)');
    vi.setSystemTime(new Date('2026-11-20T12:00:00Z'));
    expect(parentAnswer({ expiresAt: '2026-11-01T09:00:00Z', expired: true }, [])).toBe('Şimdilik geç');
    vi.useRealTimers();
    expect(staleParentCode({ expiresAt: '2026-11-01T09:00:00Z' }, Date.parse('2026-11-03T12:00:00Z'))).toBe(false);
    expect(staleParentCode({ expiresAt: '2026-11-01T09:00:00Z' }, Date.parse('2026-11-20T12:00:00Z'))).toBe(true);
    expect(staleParentCode(null)).toBe(false);
    expect(inviteAnswer([], 1)).toBe('Sonra bakarım');
    expect(emailAnswer([{ label: 'Çalışma planı hatırlatmaları', email: true }, { label: 'Seanslar', email: false }])).toBe('Çalışma planı hatırlatmaları');
    expect(emailAnswer([{ label: 'Seanslar', email: false }])).toBe('E-posta istemiyorum');
    // As on Profilim: the code's expiry and that it works once.
    expect(parentCodeNote('2026-11-13T09:00:00Z')).toBe('13.11.2026 tarihine kadar, bir kez kullanılabilir.');
  });
});
