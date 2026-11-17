/**
 * Live test 16.11.2026 (#53), user decision 17.11.2026: "Seviye" is only the program's level. An exercise's or a test's
 * difficulty is named as a difficulty, so "Seviye 1 (Kolay)" beside a level-5 program does not read as two levels.
 */
export function difficultyLabel(level: number | null | undefined): string {
  switch (level) {
    case 1: return 'Kolay';
    case 2: return 'Normal';
    case 3: return 'Zor';
    case 4: return 'İleri';
    case 5: return 'Usta';
    default: return level ? `${level}. düzey` : 'Belirtilmedi';
  }
}
