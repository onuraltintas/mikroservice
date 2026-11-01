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
  const cents = input.sections.reduce((sum, row) => sum + roundCents(practiceNet(row.correct, row.wrong, profile.wrongAnswerDivisor)), 0);
  return { correct, wrong, empty: input.sections.reduce((sum, row) => sum + row.empty, 0), net: cents / 100 };
}

/** KM1 (01.11.2026): the 17 ÖABT fields of the 2026-MEB-AGS guide (section 22), by practice scope code. */
export const OABT_FIELDS: Readonly<Record<string, string>> = {
  OABT_TUR: 'Türkçe', OABT_IMAT: 'İlköğretim Matematik', OABT_FEN: 'Fen Bilimleri', OABT_SOS: 'Sosyal Bilgiler',
  OABT_TDE: 'Türk Dili ve Edebiyatı', OABT_TAR: 'Tarih', OABT_COG: 'Coğrafya', OABT_MAT: 'Matematik', OABT_FIZ: 'Fizik',
  OABT_KIM: 'Kimya/Kimya Teknolojisi', OABT_BIO: 'Biyoloji', OABT_DIN: 'Din Kültürü ve Ahlâk Bilgisi / İmam-Hatip Lisesi Meslek Dersleri',
  OABT_REH: 'Rehberlik', OABT_SINIF: 'Sınıf Öğretmenliği', OABT_OKUL: 'Okul Öncesi', OABT_BED: 'Beden Eğitimi', OABT_OZEL: 'Özel Eğitim'
};
export const YOKDIL_FIELDS: Readonly<Record<string, string>> = { YOKDIL_FEN: 'Fen Bilimleri', YOKDIL_SAG: 'Sağlık Bilimleri', YOKDIL_SOS: 'Sosyal Bilimler' };
/** Every practice scope the server knows, in its order (PracticeExamRules.Profiles). */
export const PRACTICE_SCOPE_CODES: readonly string[] = ['LGS', 'TYT', 'AYT_SAY', 'AYT_EA', 'AYT_SOZ', 'YDT_EN', 'AGS',
  ...Object.keys(OABT_FIELDS), 'YDS_EN', ...Object.keys(YOKDIL_FIELDS)];

export function practiceExamName(code: string | null | undefined): string {
  const key = code ?? '';
  if (OABT_FIELDS[key]) return `ÖABT ${OABT_FIELDS[key]}`;
  if (YOKDIL_FIELDS[key]) return `YÖKDİL ${YOKDIL_FIELDS[key]}`;
  return ({LGS:'LGS', TYT:'TYT', AYT_SAY:'AYT Sayısal', AYT_EA:'AYT Eşit Ağırlık',
    AYT_SOZ:'AYT Sözel', YDT_EN:'YDT İngilizce', AGS:'AGS', YDS_EN:'YDS İngilizce'} as Record<string,string>)[key] ?? code ?? 'Sınav kapsamı belirtilmemiş';
}

/** KM1: the exam type a practice scope belongs to, as the server's PracticeExamRules.ExamTypeFor. */
export function examTypeForProfile(code: string): 'LGS' | 'YKS' | 'AGS' | 'YDS' | 'YOKDIL' {
  if (code === 'LGS') return 'LGS';
  if (code === 'AGS' || code.startsWith('OABT_')) return 'AGS';
  if (code === 'YDS_EN') return 'YDS';
  if (code.startsWith('YOKDIL_')) return 'YOKDIL';
  return 'YKS';
}

/** Exam types whose results are entered section by section. */
export function requiresSections(examType: string): boolean { return ['LGS', 'YKS', 'AGS', 'YDS', 'YOKDIL'].includes(examType); }

/** correct − wrong / divisor; a divisor of 0 means wrong answers cost nothing (YDS, YÖKDİL). */
export function practiceNet(correct: number, wrong: number, divisor: number): number { return divisor ? correct - wrong / divisor : correct; }

/** Hundredths of a net, rounded half away from zero like decimal.Round(..., 2, AwayFromZero) on the server. */
function roundCents(net: number): number {
  return Math.sign(net) * Math.round(Math.abs(net) * 100 + 1e-9);
}
