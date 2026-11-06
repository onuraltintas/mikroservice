import { ExamLearningPurpose, LearningExamSession } from '../../../core/services/coaching-learning-setup.service';
import { OnboardingExamStep, OnboardingInvitation, OnboardingPersonalStep, OnboardingTargetStep, OnboardingView } from '../../../core/services/coaching-onboarding.service';
import { coachingDate } from '../coaching-labels';
import { OABT_FIELDS, YOKDIL_FIELDS } from '../../../core/models/practice-exam';
import { EDUCATION_STAGES, EducationStage, educationRequest, gradesFor, isStudentStage, stageOf } from '../coaching-education';

/**
 * Paket C (#3, user decisions 05.11.2026): the first-login wizard as a chat. Every question is answered with a component
 * (buttons, date, number or a searchable list); there is no free text and no AI. This file holds the flow without Angular:
 * which questions apply to the answers so far, what a changed answer clears, and the request of each step.
 */
export type QuestionId =
  | 'birthDate' | 'city' | 'district' | 'stage' | 'education' | 'ownSchool'
  | 'purpose' | 'yksField' | 'language' | 'oabtField' | 'yokdilField' | 'session' | 'diploma'
  | 'targetSaved' | 'targetMode' | 'schoolCity' | 'schoolType' | 'targetSchool'
  | 'level' | 'programScoreType' | 'programCity' | 'university' | 'program' | 'ownScore'
  | 'targetScoreType' | 'ydsLevel' | 'targetScore' | 'tytScore' | 'fieldScore'
  | 'invite' | 'parent' | 'notifications';
export type InputKind = 'date' | 'buttons' | 'combobox' | 'number' | 'diploma' | 'invite' | 'parent' | 'notifications';
export type StepNumber = 1 | 2 | 3 | 4 | 5 | 6 | 7;
export interface Answer { value: string | number | null; label: string; data?: Record<string, unknown> }
export type Answers = Partial<Record<QuestionId, Answer>>;
export interface Choice { value: string; label: string }
export interface Question {
  id: QuestionId; step: StepNumber; prompt: string; reason: string; kind: InputKind; choices?: Choice[];
  /** A skip button with this label answers the question with no value. */
  skipLabel?: string; min?: number; max?: number;
}
export interface FlowContext { sessions: LearningExamSession[]; academicYearStart: number;
  /** Paket D: the exams the server allows for the saved grade (all when missing). */
  allowedPurposes?: string[] }

export const STEP_COUNT = 7;
export const STEP_TITLES: Record<StepNumber, string> = {
  1: 'Kişisel bilgilerin', 2: 'Sınavın', 3: 'Hedefin', 4: 'Davet', 5: 'Veli bağlantısı', 6: 'Bildirimler', 7: 'Özet'
};
export const PURPOSES: Choice[] = [
  { value: 'Lgs', label: 'LGS' }, { value: 'Yks', label: 'YKS' }, { value: 'Ags', label: 'AGS / ÖABT' },
  { value: 'Yds', label: 'YDS' }, { value: 'Yokdil', label: 'YÖKDİL' }];
export const YKS_FIELDS: Choice[] = [
  { value: 'SAY', label: 'Sayısal' }, { value: 'EA', label: 'Eşit Ağırlık' }, { value: 'SOZ', label: 'Sözel' },
  { value: 'DIL', label: 'Dil' }, { value: 'TYT', label: 'Yalnız TYT (önlisans)' }];
/** E3 #14: "Şu an durumun ne?" (the grade is asked next, for ortaokul and lise students only). */
export const STAGES: Choice[] = EDUCATION_STAGES.map(x => ({ value: x.value, label: x.label }));
const gradeChoices = (stage: EducationStage | '') => gradesFor(stage).map(grade => ({ value: String(grade), label: `${grade}. sınıf` }));
/** ÖSYM YDS levels (2026 YDS Kılavuzu 3.8): the shortcut fills the level's lowest score. */
export const YDS_LEVELS: Choice[] = [
  { value: '90', label: 'A (90–100)' }, { value: '80', label: 'B (80–89)' }, { value: '70', label: 'C (70–79)' },
  { value: '60', label: 'D (60–69)' }, { value: '50', label: 'E (50–59)' }];
const EXAM_CODES: Record<ExamLearningPurpose, string> = { Lgs: 'LGS', Yks: 'TYT', Ags: 'AGS', Yds: 'YDS', Yokdil: 'YOKDIL' };
const EXAM_TYPES: Record<ExamLearningPurpose, string> = { Lgs: 'LGS', Yks: 'YKS', Ags: 'AGS', Yds: 'YDS', Yokdil: 'YÖKDİL' };

const TARGET: QuestionId[] = ['targetSaved', 'targetMode', 'schoolCity', 'schoolType', 'targetSchool', 'level', 'programScoreType', 'programCity',
  'university', 'program', 'ownScore', 'targetScoreType', 'ydsLevel', 'targetScore', 'tytScore', 'fieldScore'];
/** A changed answer clears only the answers that depend on it; the flow then asks them again. */
export const DEPENDENTS: Partial<Record<QuestionId, QuestionId[]>> = {
  city: ['district', 'ownSchool'],
  stage: ['education', 'ownSchool', 'diploma'],
  education: ['ownSchool', 'diploma'],
  purpose: ['yksField', 'language', 'oabtField', 'yokdilField', 'session', 'diploma', ...TARGET],
  yksField: ['language', 'diploma', ...TARGET],
  oabtField: TARGET, yokdilField: TARGET,
  targetMode: TARGET.filter(x => x !== 'targetMode'),
  schoolCity: ['schoolType', 'targetSchool'], schoolType: ['targetSchool'],
  level: ['programScoreType', 'programCity', 'university', 'program', 'ownScore'],
  programScoreType: ['programCity', 'university', 'program', 'ownScore'],
  programCity: ['university', 'program', 'ownScore'], university: ['program', 'ownScore'], program: ['ownScore'],
  targetScoreType: ['ydsLevel', 'targetScore'], ydsLevel: ['targetScore'], tytScore: ['fieldScore']
};

/** Paket D (#8, review M1/L3): the exams the server allows for the saved grade; the rule itself lives only on the server. */
export function purposeChoices(context: FlowContext): Choice[] {
  const allowed = context.allowedPurposes;
  return allowed ? PURPOSES.filter(x => allowed.includes(x.value)) : PURPOSES;
}

/** An exam the saved grade no longer allows is asked again (with its dependents), never resent to the server. */
export function withoutUnfitPurpose(answers: Answers, allowed: string[] | undefined): Answers {
  const purpose = purposeOf(answers);
  return allowed && purpose && !allowed.includes(purpose) ? change(answers, 'purpose') : answers;
}

/** Removes an answer and everything that depends on it ("Değiştir"). */
export function change(answers: Answers, id: QuestionId): Answers {
  const next = { ...answers };
  const clear = (key: QuestionId) => { delete next[key]; for (const child of DEPENDENTS[key] ?? []) if (child in next) clear(child); };
  clear(id);
  return next;
}

const value = (answers: Answers, id: QuestionId) => answers[id]?.value ?? null;
export function purposeOf(answers: Answers): ExamLearningPurpose | null { return value(answers, 'purpose') as ExamLearningPurpose | null; }
export function stageOfAnswers(answers: Answers): EducationStage | '' { return (value(answers, 'stage') as EducationStage | null) ?? ''; }
/** The grade of an ortaokul or lise student (null for everyone else and before it is answered). */
export function gradeOf(answers: Answers): number | null {
  const education = value(answers, 'education');
  return isStudentStage(stageOfAnswers(answers)) && education !== null ? Number(education) : null;
}
/** OBP counts from 11th grade, and for everyone out of high school (E3 #14). */
export function asksDiploma(answers: Answers): boolean {
  const stage = stageOfAnswers(answers); const grade = gradeOf(answers);
  return purposeOf(answers) === 'Yks' && (stage !== '' && !isStudentStage(stage) || grade === 11 || grade === 12);
}

/** The score types a score-only target may use (the server's ScoreTargetTypes). */
export function scoreTargetTypes(answers: Answers): Choice[] {
  switch (purposeOf(answers)) {
    case 'Lgs': return [{ value: 'LGS', label: 'LGS puanı' }];
    case 'Yks': {
      const field = value(answers, 'yksField') as string | null;
      const own = YKS_FIELDS.find(x => x.value === field && x.value !== 'TYT');
      return [{ value: 'TYT', label: 'TYT puanı' }, ...(own ? [{ value: own.value, label: `${own.label} (${own.value === 'SOZ' ? 'SÖZ' : own.value === 'DIL' ? 'DİL' : own.value}) puanı` }] : [])];
    }
    case 'Ags': {
      const field = value(answers, 'oabtField') as string | null;
      if (field === 'OABT_DIN') return [{ value: 'P2_16', label: 'P2-16 (Din Kültürü ve Ahlak Bilgisi)' }, { value: 'P2_17', label: 'P2-17 (İHL Meslek Dersleri)' }];
      return field && field !== 'none' ? [{ value: `P2_${field.slice(5)}`, label: 'P2 puanı (AGS + ÖABT)' }] : [{ value: 'P1', label: 'P1 puanı (yalnız AGS)' }];
    }
    case 'Yds': return [{ value: 'YDS_EN', label: 'YDS puanı' }];
    case 'Yokdil': { const field = value(answers, 'yokdilField') as string | null; return field ? [{ value: field, label: 'YÖKDİL puanı' }] : []; }
    default: return [];
  }
}
export function scoreMax(type: string | null): number { return type === 'LGS' ? 500 : ['TYT', 'SAY', 'EA', 'SOZ', 'DIL'].includes(type ?? '') ? 560 : 100; }

function sessionChoices(answers: Answers, context: FlowContext): Choice[] {
  const purpose = purposeOf(answers);
  if (!purpose) return [];
  const sessions = context.sessions.filter(x => x.examCode === EXAM_CODES[purpose]);
  if (sessions.length) return sessions.map(x => ({ value: `session:${x.id}:${x.date.slice(0, 4)}`, label: `${x.label} · ${day(x.date)}` }));
  // No session in the catalog yet: the year is enough.
  return [1, 2, 3].map(offset => context.academicYearStart + offset).map(year => ({ value: `year:${year}`, label: String(year) }));
}
function day(iso: string) { return iso.split('-').reverse().join('.'); }

/** Every question that applies to the answers so far, in order; the first unanswered one is asked next. */
export function questions(answers: Answers, context: FlowContext): Question[] {
  const list: Question[] = [];
  const add = (question: Question) => list.push(question);
  const purpose = purposeOf(answers);
  const stage = stageOfAnswers(answers);

  add({ id: 'birthDate', step: 1, kind: 'date', prompt: 'Merhaba! Önce seni tanıyalım. Doğum tarihin ne?', reason: 'Profilinde tutulur; öğretmenin de görebilir.' });
  add({ id: 'city', step: 1, kind: 'combobox', prompt: 'Hangi şehirde yaşıyorsun?', reason: 'Okulunu listeden bulabilmen için. Profilinde tutulur.' });
  if (answers.city) add({ id: 'district', step: 1, kind: 'combobox', prompt: `${answers.city.label} içinde hangi ilçedesin?`, reason: 'Okul listelerinde ilçendeki okulları önce gösteririz. Profilinde tutulur.' });
  add({ id: 'stage', step: 1, kind: 'buttons', choices: STAGES, prompt: 'Şu an durumun ne?', reason: 'Hazırlanabileceğin sınavlar ve diploma notu sorusu buna göre belirlenir; öğretmenin de görür.' });
  if (isStudentStage(stage))
    add({ id: 'education', step: 1, kind: 'buttons', choices: gradeChoices(stage), prompt: 'Kaçıncı sınıftasın?', reason: 'Çalışma ayarların buna göre belirlenir; öğretmenin de görür.' });
  // The own-school list is the high-school catalog: asked of lise students only.
  if (stage === 'high' && answers.education)
    add({ id: 'ownSchool', step: 1, kind: 'combobox', skipLabel: 'Şimdilik geçeyim', prompt: 'Okulunu listeden seçmek ister misin?', reason: 'İsteğe bağlı. Profilinde tutulur; öğretmenin de görür.' });

  add({ id: 'purpose', step: 2, kind: 'buttons', choices: purposeChoices(context), prompt: 'Hangi sınava hazırlanıyorsun?', reason: 'Planın, derslerin ve puan hesabın bu sınava göre hazırlanır.' });
  if (purpose === 'Yks') {
    add({ id: 'yksField', step: 2, kind: 'buttons', choices: YKS_FIELDS, prompt: 'Hangi puan türüyle yerleşmek istiyorsun?', reason: 'Çalışacağın AYT dersleri ve puan hesabın buna göre seçilir.' });
    if (value(answers, 'yksField') === 'DIL')
      add({ id: 'language', step: 2, kind: 'buttons', choices: [{ value: 'English', label: 'İngilizce' }], prompt: 'YDT\'de hangi dile gireceksin?', reason: 'Şimdilik yalnız İngilizce destekleniyor.' });
  }
  if (purpose === 'Ags')
    add({ id: 'oabtField', step: 2, kind: 'combobox', prompt: 'ÖABT\'de hangi alandasın?', reason: 'Alanına göre ÖABT derslerin ve P2 puanın hesaplanır.' });
  if (purpose === 'Yokdil')
    add({ id: 'yokdilField', step: 2, kind: 'buttons', choices: Object.entries(YOKDIL_FIELDS).map(([code, label]) => ({ value: code, label })),
      prompt: 'YÖKDİL\'de hangi alana gireceksin?', reason: 'Sorular alana göre değişir; çalışma planın buna göre hazırlanır.' });
  if (purpose) {
    const choices = sessionChoices(answers, context);
    add({ id: 'session', step: 2, kind: 'buttons', choices,
      prompt: choices[0]?.value.startsWith('session:') ? 'Hangi oturuma hazırlanıyorsun?' : 'Hangi yıl sınava gireceksin?',
      reason: 'Sınava kalan günleri bu tarihe göre gösteririz.' });
  }
  if (asksDiploma(answers))
    add({ id: 'diploma', step: 2, kind: 'diploma', min: 50, max: 100, skipLabel: 'Sonra gireceğim', prompt: 'Diploma notun kaç?',
      reason: 'OBP (diploma notu × 5 × 0,12) YKS puanına eklenir; hedefinle karşılaştırmada kullanılır.' });

  if (purpose) addTarget(answers, add, purpose);

  add({ id: 'invite', step: 4, kind: 'invite', prompt: 'Seni davet eden bir öğretmenin ya da kurumun var mı?', reason: 'Seni e-postanla davet ettilerse onlara buradan bağlanırsın.' });
  add({ id: 'parent', step: 5, kind: 'parent', prompt: 'Velinle bağlantı kurmak ister misin?', reason: 'İstersen velin çalışmanı görebilir. Zorunlu değil, sonra da yapabilirsin.' });
  add({ id: 'notifications', step: 6, kind: 'notifications', prompt: 'Hangi e-postaları almak istersin?', reason: 'Hatırlatmaları sen seçersin; sonra da değiştirebilirsin.' });
  return list;
}

function addTarget(answers: Answers, add: (question: Question) => void, purpose: ExamLearningPurpose) {
  const reason = 'Hedefine ne kadar kaldığını ana sayfada gösterebilmemiz için.';
  if (answers.targetSaved) { add({ id: 'targetSaved', step: 3, kind: 'buttons', prompt: 'Hedefin ne?', reason }); return; }
  const modes: Choice[] = purpose === 'Lgs' ? [{ value: 'school', label: 'Okul seçeceğim' }, { value: 'score', label: 'Hedef puan gireceğim' }]
    : purpose === 'Yks' ? [{ value: 'program', label: 'Bölüm seçeceğim' }, { value: 'score', label: 'Yalnız puan gireceğim' }]
    : [{ value: 'score', label: 'Hedef puan gireceğim' }];
  add({ id: 'targetMode', step: 3, kind: 'buttons', choices: [...modes, { value: 'none', label: 'Henüz karar vermedim' }], prompt: 'Hedefin ne?', reason });
  const mode = value(answers, 'targetMode');
  if (mode === 'school') {
    add({ id: 'schoolCity', step: 3, kind: 'combobox', prompt: 'Hangi şehirde bir okul istiyorsun?', reason: 'Okul listesini daraltmak için.' });
    if (answers.schoolCity) add({ id: 'schoolType', step: 3, kind: 'buttons', choices: [], prompt: 'Hangi tür okul?', reason: 'Listeyi daha da kısaltmak için.' });
    if (answers.schoolType) add({ id: 'targetSchool', step: 3, kind: 'combobox', prompt: 'Hangi okul?', reason: 'Taban puanını hedefinle karşılaştırırız.' });
  }
  if (mode === 'program') {
    const tytOnly = value(answers, 'yksField') === 'TYT';
    add({ id: 'level', step: 3, kind: 'buttons', prompt: 'Lisans mı, önlisans mı?', reason: 'Önlisans programları TYT puanıyla öğrenci alır.',
      choices: tytOnly ? [{ value: 'Associate', label: 'Önlisans' }] : [{ value: 'Bachelor', label: 'Lisans' }, { value: 'Associate', label: 'Önlisans' }] });
    const level = value(answers, 'level');
    if (level === 'Bachelor') {
      const field = YKS_FIELDS.find(x => x.value === value(answers, 'yksField'));
      add({ id: 'programScoreType', step: 3, kind: 'buttons', prompt: 'Hangi puan türü?', reason: 'Puan türünü sınav sorusunda seçtiğin alandan alırız. Değiştirmek için oradaki cevabını değiştir.',
        choices: field ? [{ value: field.value, label: field.label }] : [] });
    }
    if (level === 'Associate' || answers.programScoreType)
      add({ id: 'programCity', step: 3, kind: 'combobox', prompt: 'Hangi şehirde okumak istiyorsun?', reason: 'Üniversite listesini daraltmak için.' });
    if (answers.programCity) add({ id: 'university', step: 3, kind: 'combobox', prompt: 'Hangi üniversite?', reason: 'Bölüm listesini daraltmak için.' });
    if (answers.university) add({ id: 'program', step: 3, kind: 'combobox', prompt: 'Hangi bölüm?', reason: 'Taban puanını ve başarı sırasını hedefinle karşılaştırırız.' });
    if (answers.program && answers.program.data?.['minimumScore'] == null)
      add({ id: 'ownScore', step: 3, kind: 'number', min: 0, max: 560, prompt: 'Bu bölümün taban puanı yok. Hedef puanın kaç?', reason: "Farkı bu puana göre hesaplarız. 0'dan büyük, en çok 560." });
  }
  if (mode === 'score' && purpose === 'Yks') {
    // Karar 4 (user): a YKS student may aim at both the TYT and the field's score; either may be left out, not both.
    const field = scoreTargetTypes(answers).find(x => x.value !== 'TYT');
    add({ id: 'tytScore', step: 3, kind: 'number', min: 0, max: 560, skipLabel: field ? 'TYT hedefim yok' : undefined,
      prompt: 'TYT hedef puanın kaç?', reason: "0'dan büyük, en çok 560 olan bir puan yaz." });
    if (field && answers.tytScore)
      add({ id: 'fieldScore', step: 3, kind: 'number', min: 0, max: 560, skipLabel: typeof value(answers, 'tytScore') === 'number' ? 'Bu puanı şimdi girmeyeceğim' : undefined,
        prompt: `${field.label.replace(' puanı', '')} hedef puanın kaç?`, reason: "0'dan büyük, en çok 560 olan bir puan yaz." });
  } else if (mode === 'score') {
    const types = scoreTargetTypes(answers);
    add({ id: 'targetScoreType', step: 3, kind: 'buttons', choices: types, prompt: 'Hangi puanı hedefliyorsun?', reason: 'Puanın bu türde hesaplanır.' });
    const type = value(answers, 'targetScoreType') as string | null;
    if (type === 'YDS_EN')
      add({ id: 'ydsLevel', step: 3, kind: 'buttons', choices: YDS_LEVELS, skipLabel: 'Puanı kendim yazacağım', prompt: 'Hangi seviyeyi hedefliyorsun?', reason: 'İstersen seviyeni seç, puanı biz dolduralım.' });
    if (type && (type !== 'YDS_EN' || answers.ydsLevel))
      add({ id: 'targetScore', step: 3, kind: 'number', min: 0, max: scoreMax(type), prompt: 'Hedef puanın kaç?', reason: `0'dan büyük, en çok ${scoreMax(type)} olan bir puan yaz.` });
  }
}

/** Answers that are set without asking (a single possible answer). */
export function autoAnswer(question: Question): Answer | null {
  if ((question.id === 'targetScoreType' || question.id === 'programScoreType' || question.id === 'level' || question.id === 'language') && question.choices?.length === 1)
    return { value: question.choices[0].value, label: question.choices[0].label };
  return null;
}

export function isAnswered(answers: Answers, id: QuestionId): boolean { return answers[id] !== undefined; }
export function nextQuestion(answers: Answers, context: FlowContext): Question | null {
  return questions(answers, context).find(x => !isAnswered(answers, x.id)) ?? null;
}
export function stepQuestions(answers: Answers, context: FlowContext, step: StepNumber): Question[] {
  return questions(answers, context).filter(x => x.step === step);
}
export function stepComplete(answers: Answers, context: FlowContext, step: StepNumber): boolean {
  return stepQuestions(answers, context, step).every(x => isAnswered(answers, x.id));
}
/** The step being worked on: the step of the next question, else the summary. */
export function currentStep(answers: Answers, context: FlowContext): StepNumber { return nextQuestion(answers, context)?.step ?? 7; }
/** What a step sends; a saved step with the same signature is not sent again. */
export function signature(answers: Answers, context: FlowContext, step: StepNumber): string {
  return JSON.stringify(stepQuestions(answers, context, step).map(x => [x.id, answers[x.id]?.value ?? null, answers[x.id]?.data ?? null]));
}

// ---------- Requests ----------

export function personalRequest(answers: Answers): OnboardingPersonalStep {
  const education = educationRequest(stageOfAnswers(answers) as EducationStage, gradeOf(answers));
  return {
    birthDate: String(value(answers, 'birthDate')), provinceId: String(value(answers, 'city')), districtId: String(value(answers, 'district')),
    ...education,
    // Not asked here: the server keeps the saved graduation year, and the saved school unless a new one is picked.
    graduationYear: null,
    schoolId: education.educationStatus === 'Student' && typeof value(answers, 'ownSchool') === 'string' ? value(answers, 'ownSchool') as string : null
  };
}

export function examRequest(answers: Answers): OnboardingExamStep {
  const purpose = purposeOf(answers)!;
  const session = String(value(answers, 'session') ?? '');
  const [kind, id, year] = session.split(':');
  const field = purpose === 'Yks' ? value(answers, 'yksField') : purpose === 'Ags' ? value(answers, 'oabtField') : purpose === 'Yokdil' ? value(answers, 'yokdilField') : null;
  return {
    purpose, primaryScoreType: field === null || field === 'none' ? null : String(field),
    foreignLanguage: purpose === 'Yds' || purpose === 'Yokdil' || field === 'DIL' ? 'English' : null,
    targetExamSessionId: kind === 'session' ? id : null, targetExamYear: Number(kind === 'session' ? year : id) || null,
    diplomaGrade: asksDiploma(answers) && typeof value(answers, 'diploma') === 'number' ? value(answers, 'diploma') as number : null,
    diplomaGradeEstimated: !!answers.diploma?.data?.['estimated']
  };
}

export function targetRequest(answers: Answers): OnboardingTargetStep {
  const saved = answers.targetSaved?.data;
  if (saved) return { kind: saved['kind'] as OnboardingTargetStep['kind'], targetId: (saved['targetId'] as string) ?? null,
    scores: (saved['scores'] as OnboardingTargetStep['scores']) ?? null };
  const num = (id: QuestionId) => typeof value(answers, id) === 'number' ? value(answers, id) as number : null;
  switch (value(answers, 'targetMode')) {
    case 'school': return { kind: 'school', targetId: String(value(answers, 'targetSchool')), scores: null };
    case 'program': return { kind: 'program', targetId: String(value(answers, 'program')),
      scores: num('ownScore') !== null ? [{ scoreType: '', score: num('ownScore')! }] : null };
    case 'score': {
      if (purposeOf(answers) === 'Yks') {
        const field = scoreTargetTypes(answers).find(x => x.value !== 'TYT')?.value;
        const scores = [{ scoreType: 'TYT', score: num('tytScore') }, { scoreType: field ?? '', score: num('fieldScore') }]
          .filter((x): x is { scoreType: string; score: number } => x.score !== null && x.scoreType !== '');
        return { kind: 'score', targetId: null, scores };
      }
      return { kind: 'score', targetId: null, scores: [{ scoreType: String(value(answers, 'targetScoreType')), score: num('targetScore')! }] };
    }
    default: return { kind: 'none', targetId: null, scores: null };
  }
}

// ---------- From the server ----------

const decimal = new Intl.NumberFormat('tr-TR', { maximumFractionDigits: 2 });
export function formatScore(value: number) { return decimal.format(value); }

/** The answers the server already has; a saved step counts as answered. */
export function prefill(view: OnboardingView): Answers {
  const a = view.answers; const saved = new Set(view.savedSteps); const answers: Answers = {};
  if (a.birthDate) answers.birthDate = { value: a.birthDate, label: a.birthDate.split('-').reverse().join('.') };
  if (a.homeProvinceId && a.homeCity) answers.city = { value: a.homeProvinceId, label: a.homeCity };
  if (a.homeDistrictId && a.homeDistrict) answers.district = { value: a.homeDistrictId, label: a.homeDistrict };
  const stage = stageOf(a.educationStatus, a.gradeLevel);
  if (stage) answers.stage = { value: stage, label: STAGES.find(x => x.value === stage)!.label };
  if (isStudentStage(stage) && a.gradeLevel) answers.education = { value: String(a.gradeLevel), label: `${a.gradeLevel}. sınıf` };
  // The saved school stays (value null keeps it on the server); a newly picked one sends its catalog id.
  if (saved.has(1) && stage === 'high') answers.ownSchool = { value: null, label: a.schoolName ?? 'Şimdilik geçeyim' };
  if (a.purpose) {
    answers.purpose = { value: a.purpose, label: PURPOSES.find(x => x.value === a.purpose)?.label ?? a.purpose };
    if (a.purpose === 'Yks' && a.primaryScoreType) answers.yksField = { value: a.primaryScoreType, label: YKS_FIELDS.find(x => x.value === a.primaryScoreType)?.label ?? a.primaryScoreType };
    if (a.purpose === 'Yks' && a.primaryScoreType === 'DIL') answers.language = { value: 'English', label: 'İngilizce' };
    if (a.purpose === 'Ags') answers.oabtField = a.primaryScoreType ? { value: a.primaryScoreType, label: OABT_FIELDS[a.primaryScoreType] ?? a.primaryScoreType } : { value: 'none', label: 'ÖABT\'ye girmeyeceğim' };
    if (a.purpose === 'Yokdil' && a.primaryScoreType) answers.yokdilField = { value: a.primaryScoreType, label: YOKDIL_FIELDS[a.primaryScoreType] ?? a.primaryScoreType };
    const session = view.examSessions.find(x => x.id === a.targetExamSessionId);
    if (session) answers.session = { value: `session:${session.id}:${session.date.slice(0, 4)}`, label: `${session.label} · ${day(session.date)}` };
    else if (a.targetExamYear && !a.targetExamSessionId && saved.has(2)) answers.session = { value: `year:${a.targetExamYear}`, label: String(a.targetExamYear) };
    if (saved.has(2) && asksDiploma(answers))
      answers.diploma = a.diplomaGrade !== null ? { value: a.diplomaGrade, label: `${formatScore(a.diplomaGrade)}${a.diplomaGradeEstimated ? ' (tahmini)' : ''}`, data: { estimated: a.diplomaGradeEstimated } }
        : { value: null, label: 'Sonra gireceğim' };
  }
  if (a.targetLocked) {
    // A teacher's goal: the wizard does not change it ("none" keeps it as it is).
    answers.targetSaved = { value: 'locked', label: `Hedefini öğretmenin belirledi${a.target ? ': ' + targetLabel(a.target) : ''}`, data: { kind: 'none', locked: true } };
  } else if (saved.has(3) && !(a.target?.kind === 'score' && a.target.scores.some(x => !x.scoreType))) {
    // An older score target without a score type cannot be sent again as it is: the target question is asked again.
    const t = a.target;
    answers.targetSaved = t ? { value: t.kind, label: targetLabel(t), data: { kind: t.kind, targetId: t.targetId,
      scores: t.kind === 'school' || !t.scores.length || t.scores.some(x => !x.scoreType) && t.kind === 'score' ? null : t.scores } }
      : { value: 'none', label: 'Henüz karar vermedim', data: { kind: 'none' } };
  }
  // E4 #18: the real answers, not "Tamam". The invitations come with the view; the parent code and the e-mail choices live in
  // Identity and Notification, so the component replaces these two labels once it has read them (see RESUMING).
  if (saved.has(4)) answers.invite = { value: 'passed', label: inviteAnswer(view.acceptedInvitations ?? []) };
  if (saved.has(5)) answers.parent = { value: 'passed', label: RESUMING };
  if (saved.has(6)) answers.notifications = { value: 'passed', label: RESUMING };
  return withoutUnfitPurpose(answers, view.allowedPurposes);
}

/** The label of a saved step 5 or 6 until its answer is read from Identity or Notification. */
export const RESUMING = 'Kaydedildi';

/** E4 #18: the invite step's answer from the accepted invitations; with invitations still waiting the student chose "Sonra bakarım". */
export function inviteAnswer(accepted: OnboardingInvitation[], pending = 0): string {
  if (!accepted.length) return pending ? 'Sonra bakarım' : 'Davetim yok';
  return `Daveti kabul ettim: ${accepted.map(x => x.inviterEmail
    ? `${x.inviterEmail}${x.type === 'StudentToInstitution' ? ' (kurum)' : ''}` : x.type === 'StudentToInstitution' ? 'bir kurum' : 'bir öğretmen').join(', ')}`;
}
/** E4 #18: the parent step's answer: an open code with its expiry, else the parents linked or waiting, else an expired code, else skipped. */
export function parentAnswer(code: { expiresAt: string; expired?: boolean } | null, links: { parentName: string; status: string }[]): string {
  if (code && !code.expired) return `Veli kodu oluşturdum (${coachingDate(code.expiresAt)} tarihine kadar geçerli)`;
  if (links.length) return `Velimle bağlantım var: ${links.map(x => x.status === 'Pending' ? `${x.parentName} (onayımı bekliyor)` : x.parentName).join(', ')}`;
  if (code) return `Veli kodumun süresi doldu (${coachingDate(code.expiresAt)})`;
  return 'Şimdilik geç';
}
/** E4 #18: the e-mail step's answer: the e-mails the student gets (the wizard's own categories). */
export function emailAnswer(prefs: { label: string; email: boolean }[]): string {
  return prefs.filter(x => x.email).map(x => x.label).join(', ') || 'E-posta istemiyorum';
}
/** As on Profilim: until when a parent code works and that it works once. */
export function parentCodeNote(expiresAt: string): string { return `${coachingDate(expiresAt)} tarihine kadar, bir kez kullanılabilir.`; }

export function targetLabel(t: { kind: string; name: string; minimumScore: number | null; scores: { scoreType: string; score: number }[] }): string {
  if (t.kind === 'score') return t.scores.map(x => `${SCORE_NAMES[x.scoreType] ?? t.name} ${formatScore(x.score)}`).join(', ');
  const own = t.scores[0]?.score;
  const taban = t.minimumScore !== null ? ` · taban ${formatScore(t.minimumScore)}` : own !== undefined ? ` · hedef puanın ${formatScore(own)}` : '';
  return `${t.name}${taban}`;
}
const SCORE_NAMES: Record<string, string> = { LGS: 'LGS', TYT: 'TYT', SAY: 'Sayısal', EA: 'Eşit Ağırlık', SOZ: 'Sözel', DIL: 'Dil', P1: 'P1', P2_16: 'P2-16', P2_17: 'P2-17',
  YDS_EN: 'YDS', YOKDIL_FEN: 'YÖKDİL', YOKDIL_SAG: 'YÖKDİL', YOKDIL_SOS: 'YÖKDİL' };

/** The summary: one line per answered question, grouped by step. */
export function summary(answers: Answers, context: FlowContext): { step: StepNumber; title: string; lines: string[] }[] {
  const all = questions(answers, context);
  return ([1, 2, 3, 4, 5, 6] as StepNumber[]).map(step => ({
    step, title: STEP_TITLES[step],
    lines: all.filter(q => q.step === step && answers[q.id]).map(q => `${SUMMARY_LABELS[q.id]}: ${answers[q.id]!.label}`)
  }));
}
const SUMMARY_LABELS: Record<QuestionId, string> = {
  birthDate: 'Doğum tarihi', city: 'Şehir', district: 'İlçe', stage: 'Eğitim durumu', education: 'Sınıf', ownSchool: 'Okul',
  purpose: 'Sınav', yksField: 'Puan türü', language: 'Yabancı dil', oabtField: 'ÖABT alanı', yokdilField: 'YÖKDİL alanı', session: 'Oturum', diploma: 'Diploma notu',
  targetSaved: 'Hedef', targetMode: 'Hedef', schoolCity: 'Okulun şehri', schoolType: 'Okul türü', targetSchool: 'Okul', level: 'Düzey', programScoreType: 'Puan türü',
  programCity: 'Şehir', university: 'Üniversite', program: 'Bölüm', ownScore: 'Hedef puan', targetScoreType: 'Puan', ydsLevel: 'Seviye', targetScore: 'Hedef puan',
  tytScore: 'TYT hedef puanı', fieldScore: 'Alan hedef puanı',
  invite: 'Davet', parent: 'Veli bağlantısı', notifications: 'E-postalar'
};
export function examTypeLabel(purpose: ExamLearningPurpose): string { return EXAM_TYPES[purpose]; }
