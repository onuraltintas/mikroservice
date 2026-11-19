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
  attention: 'Dikkat',
  targethunt: 'Hedef Avı',
  freereading: 'Serbest Okuma',
  saccade: 'Göz Sıçraması',
  textfading: 'Silinen Metin',
  erroranalysis: 'Hata Analizi',
  peripheralvision: 'Çevresel Görüş',
  rapidserial: 'Akan Kelimeler',
  scrollingtext: 'Kayan Metin',
  verticalreading: 'Dikey Okuma',
  visualization: 'Zihinde Canlandırma',
  visualizationexercise: 'Zihinde Canlandırma',
  vocabularybuilder: 'Kelime Bilgisi',
  wordgroups: 'Kelime Grupları',
  paragrafsimulasyonu: 'Paragraf Provası',
  guidedreading: 'Rehberli Okuma',
  grouping: 'Öbek Okuma'
};

/** Every code the platform can send; a code missing from the table must fail the unit test, not reach a student. */
export const KNOWN_EXERCISE_TYPE_CODES = Object.keys(EXERCISE_TYPE_LABELS);

export function exerciseTypeLabel(typeName: string | null | undefined): string {
  if (!typeName) return '';
  return EXERCISE_TYPE_LABELS[typeName.replace(/[\s_-]/g, '').toLowerCase()] ?? typeName;
}

/** A title that ends with a type's code name ("Seviye Tespit — Genç — Comprehension") reads with the Turkish name. */
export function exerciseTitleLabel(title: string | null | undefined): string {
  return (title ?? '').split(' — ').map(part => exerciseTypeLabel(part)).join(' — ');
}

/**
 * Exercise names shown to a student. The level already appears next to the title, so a content
 * suffix such as " (Seviye 5)" or " — Seviye 5" is dropped and code names read in Turkish.
 */
export function exerciseDisplayName(title: string | null | undefined): string {
  return exerciseTitleLabel(title)
    .replace(/\s*\(Seviye\s*\d+\)\s*$/i, '')
    .replace(/\s*[—-]\s*Seviye\s*\d+\s*$/i, '')
    .trim();
}
