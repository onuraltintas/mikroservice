import { exerciseTitleLabel, exerciseTypeLabel } from './exercise-type-label';

describe('exerciseTypeLabel (live test 16.11.2026, #51)', () => {
  it('names the exercise types in Turkish, whatever the spelling of the code', () => {
    expect(exerciseTypeLabel('EyeTracking')).toBe('Göz Takibi');
    expect(exerciseTypeLabel('TACHISTOSCOPE')).toBe('Hızlı Algı');
    expect(exerciseTypeLabel('visual_expansion')).toBe('Görsel Genişleme');
    expect(exerciseTypeLabel('SpeedReading')).toBe('Hızlı Okuma');
    expect(exerciseTypeLabel('Comprehension')).toBe('Okuduğunu Anlama');
    expect(exerciseTypeLabel('Vocabulary')).toBe('Kelime Bilgisi');
  });
  it('keeps an unknown name and gives nothing for nothing', () => {
    expect(exerciseTypeLabel('YeniTür')).toBe('YeniTür');
    expect(exerciseTypeLabel(null)).toBe('');
  });
  it('renames the type at the end of a title', () => {
    expect(exerciseTitleLabel('Seviye Tespit — Genç — Comprehension')).toBe('Seviye Tespit — Genç — Okuduğunu Anlama');
    expect(exerciseTitleLabel('Hareketi Takip Et')).toBe('Hareketi Takip Et');
  });
});
