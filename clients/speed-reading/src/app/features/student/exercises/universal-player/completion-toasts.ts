/**
 * H18 (30.10.2026): after a daily task the student is told what actually changed. The weekly result can move the program
 * level up or down; a new support path has its own message.
 */
export interface CompletionLevelChange {
  difficultyIncreased?: boolean;
  difficultyDecreased?: boolean;
  newDifficultyLevel?: number | null;
  currentDifficultyLevel?: number | null;
}

export function completionLevelToast(response: CompletionLevelChange | null | undefined): string | null {
  const level = response?.newDifficultyLevel ?? response?.currentDifficultyLevel;
  if (!response || !level) return null;
  if (response.difficultyIncreased) return `Geçen haftaki başarına göre seviyen ${level} oldu.`;
  if (response.difficultyDecreased) return `Geçen haftaki sonuçlarına göre seviyen ${level} olarak ayarlandı.`;
  return null;
}
