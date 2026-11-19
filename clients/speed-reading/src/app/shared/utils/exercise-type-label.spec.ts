import { exerciseDisplayName, exerciseTitleLabel, exerciseTypeLabel } from './exercise-type-label';

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

describe('exercise names (live test 19.11.2026)', () => {
  const codes = ['Chunking', 'Comprehension', 'ErrorAnalysis', 'ExamSimulation', 'EyeTracking', 'Fixation', 'Focus', 'FreeReading',
    'ParagrafSimulasyonu', 'PeripheralVision', 'RSVP', 'RapidSerial', 'RegressionReduction', 'Saccade', 'Scanning', 'SchulteTable',
    'ScrollingText', 'Skimming', 'SpeedReading', 'SubvocalizationReduction', 'Tachistoscope', 'TargetHunt', 'TextFading',
    'VerticalReading', 'VisualExpansion', 'Visualization', 'VisualizationExercise', 'Vocabulary', 'VocabularyBuilder', 'WordGroups'];

  it('names every exercise type the platform sends in Turkish', () => {
    for (const code of codes) expect(exerciseTypeLabel(code)).not.toBe(code);
    expect(exerciseTypeLabel('TargetHunt')).toBe('Hedef Avı');
    expect(exerciseTypeLabel('FreeReading')).toBe('Serbest Okuma');
  });

  it('drops the level suffix from exercise names', () => {
    expect(exerciseDisplayName('Hedef Avı: "kar" kelimesi (Seviye 5)')).toBe('Hedef Avı: "kar" kelimesi');
    expect(exerciseDisplayName('Bağımsız Okuma Pratiği — Seviye 5')).toBe('Bağımsız Okuma Pratiği');
    expect(exerciseDisplayName('Hareketi Takip Et')).toBe('Hareketi Takip Et');
  });
});
