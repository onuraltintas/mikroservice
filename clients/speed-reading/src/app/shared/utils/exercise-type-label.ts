/**
 * Live test 16.11.2026 (#51): the student saw the exercise types by their code names ("EYETRACKİNG", "TACHİSTOSCOPE", …).
 * The names shown to a student are Turkish; an unknown type keeps its own name.
 */
const EXERCISE_TYPE_LABELS: Record<string, string> = {
  comprehension: 'Okuduğunu Anlama',
  tachistoscope: 'Hızlı Algı',
  visualexpansion: 'Görsel Genişleme',
  chunking: 'Öbek Okuma',
  speedreading: 'Hızlı Okuma',
  focus: 'Odaklanma',
  vocabulary: 'Kelime Bilgisi',
  schultetable: 'Schulte Tablosu',
  eyetracking: 'Göz Takibi',
  scanning: 'Tarama',
  fixation: 'Göz Sabitleme',
  regressionreduction: 'Geri Dönüşü Azaltma',
  skimming: 'Göz Gezdirme',
  subvocalizationreduction: 'İç Sesi Azaltma',
  rsvp: 'Akan Kelimeler',
  examsimulation: 'Sınav Provası',
  attention: 'Dikkat'
};

export function exerciseTypeLabel(typeName: string | null | undefined): string {
  if (!typeName) return '';
  return EXERCISE_TYPE_LABELS[typeName.replace(/[\s_-]/g, '').toLowerCase()] ?? typeName;
}

/** A title that ends with a type's code name ("Seviye Tespit — Genç — Comprehension") reads with the Turkish name. */
export function exerciseTitleLabel(title: string | null | undefined): string {
  return (title ?? '').split(' — ').map(part => exerciseTypeLabel(part)).join(' — ');
}
