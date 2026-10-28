import { PracticeExamProfile, practiceTotals } from './practice-exam';

// Roadmap D2: the total net is the sum of the section nets rounded to two decimals, as the server stores it.
describe('practiceTotals', () => {
  const lgs: PracticeExamProfile = { code: 'LGS', name: 'LGS', wrongAnswerDivisor: 3, sections: ['TR', 'MAT', 'FEN', 'INK', 'DIN', 'ING']
    .map(code => ({ code, name: code, questionCount: code === 'TR' || code === 'MAT' || code === 'FEN' ? 20 : 10 })) };

  it('adds the rounded section nets', () => {
    const sections = lgs.sections.map(section => ({ sectionCode: section.code, correct: 0, wrong: 1, empty: section.questionCount - 1 }));
    expect(practiceTotals({ examCode: 'LGS', sections }, [lgs])?.net).toBe(-1.98);
  });

  it('keeps whole nets exact', () => {
    const sections = lgs.sections.map(section => ({ sectionCode: section.code, correct: 5, wrong: 3, empty: section.questionCount - 8 }));
    expect(practiceTotals({ examCode: 'LGS', sections }, [lgs])?.net).toBe(24);
  });
});
