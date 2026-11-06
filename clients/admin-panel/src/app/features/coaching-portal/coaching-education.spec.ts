import { EDUCATION_STAGES, educationDetail, educationLabel, educationRequest, gradesFor, isStudentStage, stageOf } from './coaching-education';

// E3 #14 (user decision 06.11.2026): one place for the education statuses shown by the wizard, Profilim and the staff pages.
describe('coaching education statuses', () => {
  it('lists the five statuses in the wizard order', () => {
    expect(EDUCATION_STAGES.map(x => [x.value, x.label])).toEqual([
      ['middle', 'Ortaokul öğrencisiyim'], ['high', 'Lise öğrencisiyim'], ['HighSchoolGraduate', 'Liseden mezunum'],
      ['UniversityStudent', 'Üniversite öğrencisiyim'], ['UniversityGraduate', 'Üniversiteden mezunum']]);
  });

  it('maps the server status and grade to a status choice and back', () => {
    expect(stageOf('Student', 5)).toBe('middle'); expect(stageOf('Student', 8)).toBe('middle');
    expect(stageOf('Student', 9)).toBe('high'); expect(stageOf('student', 12)).toBe('high');
    expect(stageOf('HighSchoolGraduate', null)).toBe('HighSchoolGraduate');
    expect(stageOf('UniversityGraduate', null)).toBe('UniversityGraduate');
    expect(stageOf('Unknown', null)).toBe(''); expect(stageOf('Student', null)).toBe('');
    // An older grade below 5 shows no status until the student chooses one.
    expect(stageOf('Student', 3)).toBe('');
    expect(educationRequest('middle', 7)).toEqual({ educationStatus: 'Student', gradeLevel: 7 });
    expect(educationRequest('high', 8)).toEqual({ educationStatus: 'Student', gradeLevel: null });
    expect(educationRequest('UniversityStudent', 11)).toEqual({ educationStatus: 'UniversityStudent', gradeLevel: null });
    expect(gradesFor('middle')).toEqual([5, 6, 7, 8]); expect(gradesFor('high')).toEqual([9, 10, 11, 12]); expect(gradesFor('UniversityGraduate')).toEqual([]);
    expect(isStudentStage('middle') && isStudentStage('high') && !isStudentStage('HighSchoolGraduate')).toBe(true);
  });

  it('labels a student for staff lists', () => {
    expect(educationLabel('Student', 10)).toBe('10. sınıf');
    expect(educationLabel(undefined, 10)).toBe('10. sınıf');
    expect(educationLabel('HighSchoolGraduate', null)).toBe('Lise mezunu');
    expect(educationLabel('UniversityStudent', null)).toBe('Üniversite öğrencisi');
    expect(educationLabel('UniversityGraduate', null)).toBe('Üniversite mezunu');
    expect(educationLabel('Graduate', null)).toBe('Lise mezunu');
    expect(educationLabel(2, null)).toBe('Lise mezunu');
    expect(educationLabel(4, null)).toBe('Üniversite mezunu');
    expect(educationLabel(undefined, null)).toBe('Sınıf bilgisi yok');
    expect(educationLabel('Unknown', null, 'Koçluk öğrencisi')).toBe('Koçluk öğrencisi');
    expect(educationDetail('Student', 10)).toBe('Lise öğrencisi, 10. sınıf');
    expect(educationDetail(1, 6)).toBe('Ortaokul öğrencisi, 6. sınıf');
    expect(educationDetail('UniversityGraduate', null)).toBe('Üniversite mezunu');
    expect(educationDetail('Unknown', null)).toBe('Belirtilmedi');
  });
});
