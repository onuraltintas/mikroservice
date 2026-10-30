import { completionLevelToast } from './completion-toasts';

describe('completionLevelToast', () => {
  it('names the new level after a step up or down', () => {
    expect(completionLevelToast({ difficultyIncreased: true, newDifficultyLevel: 4 })).toBe('Geçen haftaki başarına göre seviyen 4 oldu.');
    expect(completionLevelToast({ difficultyDecreased: true, currentDifficultyLevel: 2 }))
      .toBe('Geçen haftaki sonuçlarına göre seviyen 2 olarak ayarlandı.');
  });

  it('says nothing when the level did not change', () => {
    expect(completionLevelToast({ newDifficultyLevel: 3 })).toBeNull();
    expect(completionLevelToast(null)).toBeNull();
  });
});
