import { CoachingLabelPipe, coachingDate, coachingLabel } from './coaching-labels';

// Roadmap Y6: code values from the API are shown in Turkish.
describe('coachingLabel', () => {
  it('translates session, assignment, attachment and exam codes', () => {
    expect(coachingLabel('OneOnOne')).toBe('Birebir');
    expect(coachingLabel('Scheduled')).toBe('Planlandı');
    expect(coachingLabel('Book')).toBe('Kitap');
    expect(coachingLabel('Clean')).toBe('Hazır');
    expect(coachingLabel('Excused')).toBe('Mazeretli');
    expect(coachingLabel('Submitted')).toBe('Teslim edildi');
    expect(coachingLabel('ExamPreparation')).toBe('Sınav hazırlığı');
    expect(coachingLabel('BankTransfer')).toBe('Havale/EFT');
  });

  it('uses the kind when the same code means different things', () => {
    expect(coachingLabel('Monthly', 'billingPeriod')).toBe('Aylık');
    expect(coachingLabel('Monthly', 'examType')).toBe('Aylık değerlendirme');
    expect(coachingLabel('Annual', 'billingPeriod')).toBe('Yıllık');
  });

  it('keeps unknown values and shows a dash for empty ones', () => {
    expect(coachingLabel('MEB')).toBe('MEB');
    expect(coachingLabel(null)).toBe('—');
    expect(coachingLabel(undefined)).toBe('—');
    expect(new CoachingLabelPipe().transform('Cancelled')).toBe('İptal edildi');
  });

  it('formats calendar dates and timestamps the Turkish way', () => {
    expect(coachingDate('2026-11-03')).toBe('03.11.2026');
    expect(coachingDate('2026-11-03T21:30:00Z')).toBe('04.11.2026');
    expect(coachingDate(null)).toBe('—');
  });
});
