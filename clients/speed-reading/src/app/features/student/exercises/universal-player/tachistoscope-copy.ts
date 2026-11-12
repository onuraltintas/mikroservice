/**
 * Student-facing Tachistoscope copy, kept in one place so the form of address ("sen"/"siz") and the
 * wording can be changed together. Used by the exercise player (students and the staff preview).
 */
export type TachistoscopeContentType = 'number' | 'letter' | 'word' | 'phrase';

/** What the student types, in the accusative ("... yaz"). */
const ANSWER_NOUN: Record<TachistoscopeContentType, string> = {
  number: 'sayıyı', letter: 'harfleri', word: 'kelimeyi', phrase: 'kelime grubunu'
};

export const TACHISTOSCOPE_COPY = {
  instruction: (type: TachistoscopeContentType): string => {
    const instruction = `Ekranda kısa süre görünen ${ANSWER_NOUN[type]} yaz.`;
    return type === 'word' || type === 'phrase' ? `${instruction} Büyük-küçük harf fark etmez.` : instruction;
  },
  answerPrompt: (type: TachistoscopeContentType): string => `Gördüğün ${ANSWER_NOUN[type]} yaz`,
  contentLabel: { number: 'Sayı', letter: 'Harf', word: 'Kelime', phrase: 'Kelime grubu (iki kelime)' } as Record<TachistoscopeContentType, string>
} as const;
