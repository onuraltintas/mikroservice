/**
 * Student-facing Tachistoscope copy, kept in one place so the form of address and the wording can be changed
 * together. Speed-reading copy addresses the student with the formal "siz", like the rest of the player.
 * Used by the exercise player (students and the staff preview).
 */
export type TachistoscopeContentType = 'number' | 'letter' | 'word' | 'phrase';

/** What the student types, in the accusative ("... yazın"). */
const ANSWER_NOUN: Record<TachistoscopeContentType, string> = {
  number: 'sayıyı', letter: 'harfleri', word: 'kelimeyi', phrase: 'kelime grubunu'
};

export const TACHISTOSCOPE_COPY = {
  instruction: (type: TachistoscopeContentType): string => {
    const instruction = `Ekranda kısa süre görünen ${ANSWER_NOUN[type]} yazın.`;
    return type === 'word' || type === 'phrase' ? `${instruction} Büyük-küçük harf fark etmez.` : instruction;
  },
  answerPrompt: (type: TachistoscopeContentType): string => `Gördüğünüz ${ANSWER_NOUN[type]} yazın`,
  contentLabel: { number: 'Sayı', letter: 'Harf', word: 'Kelime', phrase: 'Kelime grubu (iki kelime)' } as Record<TachistoscopeContentType, string>
} as const;
