import type { StudyRoutineAnswers } from '../../../core/services/coaching-study-routine.service';
import { defaultRoutineAnswers, minuteChoices, questionForMessage, questionIssue, routineQuestions, routineSummary, routineWindows, timeChoices, windowLines, withShift } from './study-routine';

// Paket H4: the client mirror of the server's StudyRoutine (H3), so the student meets the server's rules before saving.
describe('study routine questions (Paket H4)', () => {
  const morning: StudyRoutineAnswers = { ...withShift(defaultRoutineAnswers(true), 'Morning'), schoolStart: 480, schoolEnd: 840, afterSchoolStart: 900, latestEnd: 1320,
    dinnerStart: 1140, dinnerEnd: 1200, busyDays: ['Wednesday'], busyDaysOff: false, busyStart: 1200, weekdayMinutes: 180,
    weekend: 'Both', weekendStart: 600, weekendEnd: 1260, weekendMinutes: 240 };

  it('turns the answers into the windows the server writes: dinner and the weekend lunch cut out, busy days start later', () => {
    const { windows, budgets, daysWithoutWindows } = routineWindows(morning);
    expect(windowLines(windows)).toEqual([
      { day: 'Pazartesi', text: '15:00–19:00, 20:00–22:00' }, { day: 'Salı', text: '15:00–19:00, 20:00–22:00' },
      { day: 'Çarşamba', text: '20:00–22:00' }, { day: 'Perşembe', text: '15:00–19:00, 20:00–22:00' }, { day: 'Cuma', text: '15:00–19:00, 20:00–22:00' },
      { day: 'Cumartesi', text: '10:00–13:00, 14:00–19:00, 20:00–21:00' }, { day: 'Pazar', text: '10:00–13:00, 14:00–19:00, 20:00–21:00' }]);
    expect(budgets).toHaveLength(7);
    expect(budgets.find(x => x.day === 'Saturday')?.minutes).toBe(240);
    expect(daysWithoutWindows).toEqual([]);
  });

  it('gives "o günler çalışamam" days neither windows nor a daily time', () => {
    const { windows, budgets } = routineWindows({ ...morning, busyDaysOff: true });
    expect(windows.some(x => x.day === 'Wednesday')).toBe(false);
    expect(budgets.some(x => x.day === 'Wednesday')).toBe(false);
  });

  it('studies before an afternoon school until an hour and a half before it, and after school from an hour after it', () => {
    const a = { ...withShift(defaultRoutineAnswers(false), 'Afternoon'), morningStudy: true, morningStart: 540, busyDays: [], weekend: 'None' as const };
    expect(windowLines(routineWindows(a).windows)[0]).toEqual({ day: 'Pazartesi', text: '09:00–11:30, 20:00–22:00' });
  });

  it('keeps 12:30–14:00 free for a student who does not go to school', () => {
    const a = { ...withShift(defaultRoutineAnswers(true), 'NoSchool'), weekend: 'None' as const };
    expect(windowLines(routineWindows(a).windows)[0]).toEqual({ day: 'Pazartesi', text: '09:00–12:30, 14:00–19:00, 20:00–22:00' });
  });

  it('asks only the questions that apply, one per screen', () => {
    expect(routineQuestions(defaultRoutineAnswers(false))).toEqual(['shift', 'after', 'dinner', 'busy', 'weekday', 'weekend', 'weekendHours', 'weekendMinutes', 'review']);
    expect(routineQuestions(morning)).toEqual(['shift', 'school', 'after', 'dinner', 'busy', 'busyStart', 'weekday', 'weekend', 'weekendHours', 'weekendMinutes', 'review']);
    expect(routineQuestions({ ...withShift(morning, 'Afternoon'), busyDays: [], weekend: 'None' })).toEqual(['shift', 'school', 'morning', 'after', 'dinner', 'busy', 'weekday', 'weekend', 'review']);
    expect(routineQuestions(withShift(morning, 'NoSchool'))[1]).toBe('day');
  });

  it('checks each answer with the server\'s sentences', () => {
    expect(questionIssue('shift', defaultRoutineAnswers(false), 480)).toBe('Okul düzenini seç.');
    expect(questionIssue('school', { ...morning, schoolStart: 600 }, 480)).toBe('Sabahçıysan okulun 06:30 ile 09:00 arasında başlamalı.');
    expect(questionIssue('school', { ...morning, schoolEnd: 600 }, 480)).toBe('Okulun çıkış saati, giriş saatinden en az 3 saat sonra olmalı.');
    expect(questionIssue('after', { ...morning, afterSchoolStart: 840 }, 480)).toBe('Okuldan sonra çalışmaya, okulun bitişinden en az yarım saat sonra başlayabilirsin.');
    expect(questionIssue('after', { ...morning, latestEnd: 930 }, 480)).toBe('En geç çalışma saatin 18:00 ile 24:00 arasında olmalı.');
    expect(questionIssue('dinner', { ...morning, dinnerEnd: 1320 }, 480)).toBe('Akşam yemeğin en az yarım saat, en çok 2 saat sürmeli.');
    expect(questionIssue('busyStart', { ...morning, busyStart: null }, 480)).toBe('Kurs olan günlerde kaçtan sonra çalışabileceğini seç.');
    expect(questionIssue('weekday', { ...morning, weekdayMinutes: 540 }, 480)).toBe('Hafta içi günlük süren en çok 8 saat olabilir.');
    expect(questionIssue('weekday', { ...morning, weekdayMinutes: 540 }, 600)).toBeNull();
    expect(questionIssue('weekendHours', { ...morning, weekendStart: 780, weekendEnd: 840 }, 480)).toContain('Hafta sonu seçtiğin saatlerde');
    expect(questionIssue('review', morning, 480)).toBeNull();
  });

  it('offers only times inside the server\'s bounds', () => {
    expect(timeChoices('schoolStart', morning)[0]).toBe(390);
    expect(timeChoices('schoolStart', morning).at(-1)).toBe(540);
    expect(timeChoices('afterSchoolStart', withShift(morning, 'Afternoon'))[0]).toBe(1140);
    expect(timeChoices('dinnerEnd', morning)).toEqual([1170, 1200, 1230, 1260]);
    expect(minuteChoices(480).at(-1)).toBe(480);
    expect(minuteChoices(600).at(-1)).toBe(600);
  });

  it('finds the question a server message belongs to', () => {
    expect(questionForMessage('Hafta sonu başlama saatin 07:00 ya da daha geç olmalı.')).toBe('weekendHours');
    expect(questionForMessage('Hafta sonu günlük süren en çok 8 saat olabilir.')).toBe('weekendMinutes');
    expect(questionForMessage('Öğlenciysen okulun 11:00 ile 14:00 arasında başlamalı.')).toBe('school');
    expect(questionForMessage('Öğlenciysen okul çıkışından en az 1 saat sonrasını seç.')).toBe('after');
    expect(questionForMessage('Akşam yemeğin 17:00 ile 23:00 arasında olmalı.')).toBe('dinner');
    expect(questionForMessage('Bu cevaplarla en az 1 saatlik bir çalışma aralığı kalmıyor; …')).toBe('review');
  });

  it('summarises the answers plainly, in third person for a teacher', () => {
    expect(routineSummary(morning)).toEqual([
      { label: 'Okul', text: 'Sabahçı, 08:00–14:00' }, { label: 'Okuldan sonra', text: '15:00–22:00' }, { label: 'Akşam yemeği', text: '19:00–20:00' },
      { label: 'Kurs, etüt, antrenman', text: 'Çarşamba (20:00 sonrası)' }, { label: 'Hafta içi', text: 'günde 3 saat' },
      { label: 'Hafta sonu', text: 'iki gün de, 10:00–21:00, günde 4 saat' }]);
    expect(routineSummary({ ...morning, busyDaysOff: true, weekend: 'None' }, true).slice(-3)).toEqual([
      { label: 'Kurs, etüt, antrenman', text: 'Çarşamba (o günler çalışmıyor)' }, { label: 'Hafta içi', text: 'günde 3 saat' }, { label: 'Hafta sonu', text: 'çalışmıyor' }]);
  });
});
