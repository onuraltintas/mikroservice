/**
 * E3 #14 (user decision 06.11.2026): "Şu an durumun ne?". The server keeps an ortaokul or lise student as `Student` with the
 * grade (5–8, 9–12); a lise graduate, a university student and a university graduate are statuses of their own, with no grade and
 * no school. One place for the choices of the wizard, Profilim and the institution editors, and for the staff list labels.
 */
export type EducationStatus = 'Unknown' | 'Student' | 'HighSchoolGraduate' | 'UniversityStudent' | 'UniversityGraduate';
export type EducationStage = 'middle' | 'high' | 'HighSchoolGraduate' | 'UniversityStudent' | 'UniversityGraduate';

export const EDUCATION_STAGES: { value: EducationStage; label: string; staffLabel: string }[] = [
  { value: 'middle', label: 'Ortaokul öğrencisiyim', staffLabel: 'Ortaokul öğrencisi' },
  { value: 'high', label: 'Lise öğrencisiyim', staffLabel: 'Lise öğrencisi' },
  { value: 'HighSchoolGraduate', label: 'Liseden mezunum', staffLabel: 'Lise mezunu' },
  { value: 'UniversityStudent', label: 'Üniversite öğrencisiyim', staffLabel: 'Üniversite öğrencisi' },
  { value: 'UniversityGraduate', label: 'Üniversiteden mezunum', staffLabel: 'Üniversite mezunu' }];

export const MIDDLE_GRADES = [5, 6, 7, 8];
export const HIGH_GRADES = [9, 10, 11, 12];

export function isStudentStage(stage: EducationStage | '' | null | undefined): stage is 'middle' | 'high' {
  return stage === 'middle' || stage === 'high';
}

/** The grades a status choice asks (none for a non-student). */
export function gradesFor(stage: EducationStage | '' | null | undefined): number[] {
  return stage === 'middle' ? MIDDLE_GRADES : stage === 'high' ? HIGH_GRADES : [];
}

/** The server status and grade as a status choice; '' when there is none yet (or an older grade below 5). */
export function stageOf(status: string | null | undefined, grade: number | null | undefined): EducationStage | '' {
  switch ((status ?? '').toLowerCase()) {
    case 'student': return typeof grade === 'number' && grade >= 5 && grade <= 8 ? 'middle' : typeof grade === 'number' && grade >= 9 && grade <= 12 ? 'high' : '';
    case 'highschoolgraduate': case 'graduate': return 'HighSchoolGraduate';
    case 'universitystudent': return 'UniversityStudent';
    case 'universitygraduate': return 'UniversityGraduate';
    default: return '';
  }
}

/** What the server is sent for a status choice: a student with a grade that fits the choice, anyone else without a grade. */
export function educationRequest(stage: EducationStage, grade: number | null | undefined): { educationStatus: EducationStatus; gradeLevel: number | null } {
  if (!isStudentStage(stage)) return { educationStatus: stage, gradeLevel: null };
  const value = Number(grade);
  return { educationStatus: 'Student', gradeLevel: gradesFor(stage).includes(value) ? value : null };
}

/**
 * F2 review: the school year a new exam choice is saved in. As the first-login wizard (the onboarding service's
 * AcademicYearStart): it starts on 1 September, Türkiye time.
 */
export function currentAcademicYear(now = new Date()): number {
  const [year, month] = new Intl.DateTimeFormat('en-CA', { timeZone: 'Europe/Istanbul', year: 'numeric', month: '2-digit' }).format(now).split('-').map(Number);
  return month >= 9 ? year : year - 1;
}

const STATUS_NUMBERS: Record<number, string> = { 1: 'Student', 2: 'HighSchoolGraduate', 3: 'UniversityStudent', 4: 'UniversityGraduate' };

/** A staff list's label: "10. sınıf", "Lise mezunu", "Üniversite öğrencisi", "Üniversite mezunu", or the fallback. */
export function educationLabel(status: string | number | null | undefined, grade: number | null | undefined, fallback = 'Sınıf bilgisi yok'): string {
  if (typeof grade === 'number' && grade > 0) return `${grade}. sınıf`;
  const name = typeof status === 'number' ? STATUS_NUMBERS[status] : status;
  const stage = stageOf(name, null);
  return stage && !isStudentStage(stage) ? EDUCATION_STAGES.find(x => x.value === stage)!.staffLabel : fallback;
}

/** A staff page's longer line: "Lise öğrencisi, 10. sınıf", "Üniversite mezunu", or the fallback. */
export function educationDetail(status: string | number | null | undefined, grade: number | null | undefined, fallback = 'Belirtilmedi'): string {
  const name = typeof status === 'number' ? STATUS_NUMBERS[status] : status;
  const stage = stageOf(name, grade);
  if (!stage) return typeof grade === 'number' && grade > 0 ? `${grade}. sınıf` : fallback;
  const label = EDUCATION_STAGES.find(x => x.value === stage)!.staffLabel;
  return isStudentStage(stage) ? `${label}, ${grade}. sınıf` : label;
}
