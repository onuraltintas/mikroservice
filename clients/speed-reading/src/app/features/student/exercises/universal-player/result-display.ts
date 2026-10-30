/**
 * H21 (result audit 30.10.2026): the result screen shows what the server measured. Paced exercises (RSVP, text fading,
 * guided reading…) show a display pace, not a measured reading speed, so they are rated on comprehension only.
 */
const PACED_ENGINES = new Set(['rsvp', 'text_stream', 'text_fade', 'word_highlight', 'regression_reduction',
  'subvocalization_reduction', 'skimming', 'exam_simulation']);

/** Engines whose numbers on the result screen come only from the server (hidden feedback or server scoring). */
const SERVER_SCORED_ENGINES = new Set(['reading_comprehension', 'exam_simulation', 'grid_interaction', 'focus',
  'attention_training']);

export function resultPerformanceLevel(input: { engineType: string; rawWpm: number | null | undefined;
  comprehension: number | null | undefined; learning?: boolean }): string {
  const { engineType, rawWpm, comprehension } = input;
  if (engineType === 'reading_comprehension') return input.learning ? 'Anlama çalışması' : 'Anlama değerlendirmesi';
  if (PACED_ENGINES.has(engineType) || !rawWpm || rawWpm <= 0) {
    if (comprehension == null) return 'Tamamlandı';
    if (comprehension >= 90) return 'Mükemmel anlama';
    if (comprehension >= 70) return 'Çok iyi anlama';
    if (comprehension >= 50) return 'Gelişiyor';
    return 'Pratik gerekli';
  }
  const understood = comprehension ?? 0;
  if (understood >= 80 && rawWpm >= 300) return 'Mükemmel!';
  if (understood >= 70 && rawWpm >= 250) return 'Çok İyi!';
  if (understood >= 60 && rawWpm >= 200) return 'İyi!';
  if (understood >= 50) return 'Gelişiyor';
  return 'Pratik Gerekli';
}

export function awaitsServerResult(input: { engineType: string | undefined; isAssessment: boolean; saveStatus: string }): boolean {
  if (['saved', 'preview'].includes(input.saveStatus)) return false;
  return input.isAssessment || SERVER_SCORED_ENGINES.has(input.engineType || '');
}

export function serverErrorCount(result: { incorrectCount?: number | null; measurementStatus?: string }, fallback: number): number {
  return result.measurementStatus === 'Measured' && result.incorrectCount != null ? result.incorrectCount : fallback;
}

export function examCorrectAnswer(answer: { correctAnswer?: string | null }, fallback: string): string {
  return answer.correctAnswer || fallback || '—';
}

export function focusSummary(detailedResults: any): { hits: number; misses: number; falseAlarms: number } | null {
  const summary = detailedResults?.focusSummary;
  if (!summary) return null;
  return { hits: summary.hits ?? 0, misses: summary.misses ?? 0, falseAlarms: summary.falseAlarms ?? 0 };
}

export function scanningResultMessage(input: { timedOut: boolean; found: number; total: number }): string {
  return input.timedOut
    ? `Süre doldu. ${input.total} hedeften ${input.found} tanesini buldun; sonuç bulduğun hedeflere göre puanlandı.`
    : 'Tüm hedefleri buldun.';
}
