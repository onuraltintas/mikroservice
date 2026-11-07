import { LearningExamSession } from '../../core/services/coaching-learning-setup.service';
import { examTimeChoices, examTimeQuestion, recommendedExamYear } from './exam-time-choices';

// G2 (#40, user decision 07.11.2026): the session list of the wizard and Çalışma tercihlerim › Sınavım.
describe('exam time choices (G2 #40)', () => {
  const session = (id: string, examCode: string, date: string, label: string): LearningExamSession =>
    ({ id, examCode, examName: examCode, date, label, applicationStart: null, applicationEnd: null });
  const yks27 = session('tyt-27', 'TYT', '2027-06-19', '2027');
  const lgs27 = session('lgs-27', 'LGS', '2027-06-13', '2027');
  const high = (grade: number) => ({ stage: 'high' as const, grade });
  const middle = (grade: number) => ({ stage: 'middle' as const, grade });
  const recommended = (choices: { value: string; recommended: boolean }[]) => choices.filter(x => x.recommended).map(x => x.value);

  it('lists the catalog session and then each later year up to four years ahead with no date yet', () => {
    const choices = examTimeChoices('Yks', [yks27, lgs27], 2026, high(11));
    expect(choices.map(x => [x.value, x.label])).toEqual([
      ['session:tyt-27:2027', '2027 · 19.06.2027'],
      ['year:2028', '2028 · tarih henüz açıklanmadı · önerilen'],
      ['year:2029', '2029 · tarih henüz açıklanmadı'],
      ['year:2030', '2030 · tarih henüz açıklanmadı']]);
    expect(choices.map(x => x.sessionId)).toEqual(['tyt-27', null, null, null]);
    expect(examTimeQuestion(choices)).toBe('Hangi oturuma hazırlanıyorsun?');
  });

  it('marks the year the YKS grade points to: 12th this school year, 11th +1, 10th +2, 9th +3', () => {
    expect([12, 11, 10, 9].map(grade => recommended(examTimeChoices('Yks', [yks27], 2026, high(grade)))))
      .toEqual([['session:tyt-27:2027'], ['year:2028'], ['year:2029'], ['year:2030']]);
    expect([12, 11, 10, 9].map(grade => recommendedExamYear('Yks', high(grade), 2026))).toEqual([2027, 2028, 2029, 2030]);
  });

  it('marks the year the LGS grade points to: 8th this school year, 7th +1, 6th +2, 5th +3', () => {
    expect([8, 7, 6, 5].map(grade => recommended(examTimeChoices('Lgs', [lgs27], 2026, middle(grade)))))
      .toEqual([['session:lgs-27:2027'], ['year:2028'], ['year:2029'], ['year:2030']]);
  });

  it('marks the nearest choice for a graduate, a university student and an unknown grade', () => {
    const yds = [session('yds-2', 'YDS', '2027-11-21', '2027/2'), session('yds-1', 'YDS', '2027-03-28', '2027/1')];
    for (const education of [{ stage: 'UniversityGraduate' as const, grade: null }, { stage: 'UniversityStudent' as const, grade: null }, { stage: '' as const, grade: null }])
      expect(recommended(examTimeChoices('Yds', yds, 2026, education))).toEqual(['session:yds-1:2027']);
    expect(recommended(examTimeChoices('Yks', [yks27], 2026, { stage: 'HighSchoolGraduate', grade: null }))).toEqual(['session:tyt-27:2027']);
    // Several sessions in a year keep their date order; the years after them follow.
    expect(examTimeChoices('Yds', yds, 2026, { stage: 'UniversityGraduate', grade: null }).map(x => x.value))
      .toEqual(['session:yds-1:2027', 'session:yds-2:2027', 'year:2028', 'year:2029', 'year:2030']);
  });

  it('offers only years when the catalog has no session for the exam, and nothing without an exam', () => {
    const choices = examTimeChoices('Ags', [yks27], 2026, { stage: 'UniversityGraduate', grade: null });
    expect(choices.map(x => x.value)).toEqual(['year:2027', 'year:2028', 'year:2029', 'year:2030']);
    expect(examTimeQuestion(choices)).toBe('Hangi yıl sınava gireceksin?');
    expect(examTimeChoices('', [yks27], 2026, high(11))).toEqual([]);
  });
});
