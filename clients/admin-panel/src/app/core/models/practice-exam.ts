export interface PracticeSection { code: string; name: string; questionCount: number }
export interface PracticeExamProfile { code: string; name: string; wrongAnswerDivisor: number; sections: PracticeSection[] }
export interface PracticeSectionAnswers { sectionCode: string; correct: number; wrong: number; empty: number }
export interface PracticeExamInput { examCode: string; sections: PracticeSectionAnswers[] }
export interface PracticeExamSummary {
  examCode: string; name: string; correct: number; wrong: number; empty: number; net: number;
  sections: (PracticeSectionAnswers & { name: string; questionCount: number; net: number })[];
}

export function practiceTotals(input: PracticeExamInput | null, profiles: PracticeExamProfile[]) {
  if (!input) return null;
  const profile = profiles.find(x => x.code === input.examCode);
  if (!profile || input.sections.length !== profile.sections.length
    || new Set(input.sections.map(x => x.sectionCode)).size !== input.sections.length) return null;
  if (profile.sections.some(section => {
    const row = input.sections.find(x => x.sectionCode === section.code);
    return !row || [row.correct, row.wrong, row.empty].some(x => !Number.isInteger(x) || x < 0)
      || row.correct + row.wrong + row.empty !== section.questionCount;
  })) return null;
  const correct = input.sections.reduce((sum, row) => sum + row.correct, 0);
  const wrong = input.sections.reduce((sum, row) => sum + row.wrong, 0);
  // Roadmap D2: like the server, the total net is the sum of the section nets rounded to two decimals.
  const cents = input.sections.reduce((sum, row) => sum + roundCents(row.correct - row.wrong / profile.wrongAnswerDivisor), 0);
  return { correct, wrong, empty: input.sections.reduce((sum, row) => sum + row.empty, 0), net: cents / 100 };
}

export function practiceExamName(code: string | null | undefined): string {
  return ({LGS:'LGS', TYT:'TYT', AYT_SAY:'AYT Sayısal', AYT_EA:'AYT Eşit Ağırlık',
    AYT_SOZ:'AYT Sözel', YDT_EN:'YDT İngilizce'} as Record<string,string>)[code ?? ''] ?? code ?? 'Sınav kapsamı belirtilmemiş';
}

/** Hundredths of a net, rounded half away from zero like decimal.Round(..., 2, AwayFromZero) on the server. */
function roundCents(net: number): number {
  return Math.sign(net) * Math.round(Math.abs(net) * 100 + 1e-9);
}
