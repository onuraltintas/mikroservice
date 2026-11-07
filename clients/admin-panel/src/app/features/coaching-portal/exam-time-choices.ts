import { ExamLearningPurpose, LearningExamSession } from '../../core/services/coaching-learning-setup.service';
import { EducationStage } from './coaching-education';

/**
 * G2 (#40, user decision 07.11.2026): "Hangi oturuma hazırlanıyorsun?" in the first-login wizard and in Çalışma tercihlerim ›
 * Sınavım. One list for both: the upcoming catalog sessions of the exam, then a year-only choice for each following year up to
 * four years ahead that has no session yet ("2028 · tarih henüz açıklanmadı"). The year the grade points to is marked
 * "önerilen" (YKS: 12th grade this school year's exam, 11th the next, …; LGS: 8th grade this year's, …); anyone else gets the
 * nearest one marked. The server accepts a year without a session and estimates its date where one is needed.
 */
export const EXAM_CATALOG_CODES: Record<ExamLearningPurpose, string> = { Lgs: 'LGS', Yks: 'TYT', Ags: 'AGS', Yds: 'YDS', Yokdil: 'YOKDIL' };
/** The furthest year offered: the school year's exam plus three (a 9th grader's YKS, a 5th grader's LGS). */
export const EXAM_YEARS_AHEAD = 4;
export const RECOMMENDED_SUFFIX = ' · önerilen';
export const YEAR_ONLY_SUFFIX = ' · tarih henüz açıklanmadı';

/** value: `session:<id>:<year>` or `year:<year>` (the wizard's answer format). */
export interface ExamTimeChoice { value: string; label: string; year: number; sessionId: string | null; recommended: boolean }
export interface ExamTimeEducation { stage: EducationStage | ''; grade: number | null }

export function examDay(iso: string): string { return iso.split('-').reverse().join('.'); }
const defaultLabel = (session: LearningExamSession) => `${session.label} · ${examDay(session.date)}`;

/**
 * The exam year the grade points to; null when the education does not say (a graduate, a university student, an unknown grade).
 * `academicYearStart` is the school year that began on 1 September (Türkiye time); its exams are in the following calendar year.
 */
export function recommendedExamYear(purpose: ExamLearningPurpose, education: ExamTimeEducation, academicYearStart: number): number | null {
  const thisYear = academicYearStart + 1; const grade = education.grade;
  if (grade === null) return null;
  if (purpose === 'Yks' && education.stage === 'high' && grade >= 9 && grade <= 12) return thisYear + 12 - grade;
  if (purpose === 'Lgs' && education.stage === 'middle' && grade >= 5 && grade <= 8) return thisYear + 8 - grade;
  return null;
}

export function examTimeChoices(purpose: ExamLearningPurpose | '' | null, sessions: readonly LearningExamSession[], academicYearStart: number,
  education: ExamTimeEducation, sessionLabel: (session: LearningExamSession) => string = defaultLabel): ExamTimeChoice[] {
  if (!purpose) return [];
  const code = EXAM_CATALOG_CODES[purpose];
  const own = sessions.filter(x => x.examCode === code).slice().sort((a, b) => a.date.localeCompare(b.date));
  const sessionYears = new Set(own.map(x => Number(x.date.slice(0, 4))));
  const choices: ExamTimeChoice[] = own.map(x => {
    const year = Number(x.date.slice(0, 4));
    return { value: `session:${x.id}:${year}`, label: sessionLabel(x), year, sessionId: x.id, recommended: false };
  });
  for (let year = academicYearStart + 1; year <= academicYearStart + EXAM_YEARS_AHEAD; year++)
    if (!sessionYears.has(year)) choices.push({ value: `year:${year}`, label: `${year}${YEAR_ONLY_SUFFIX}`, year, sessionId: null, recommended: false });
  choices.sort((a, b) => a.year - b.year);
  const target = recommendedExamYear(purpose, education, academicYearStart);
  const pick = target === null ? choices[0] : choices.find(x => x.year === target);
  if (pick) { pick.recommended = true; pick.label += RECOMMENDED_SUFFIX; }
  return choices;
}

/** The question above the list: only years when the catalog has no session for the exam yet. */
export function examTimeQuestion(choices: readonly ExamTimeChoice[]): string {
  return choices.some(x => x.sessionId) ? 'Hangi oturuma hazırlanıyorsun?' : 'Hangi yıl sınava gireceksin?';
}
