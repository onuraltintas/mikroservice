/** Live test 16.11.2026 (#41, #44): what accepting an institution invitation ends for a student with an independent teacher. */
export interface InvitationEnds { teachers: string[]; sessions: number; assignments: number; }
export function invitationEndsText(ends: InvitationEnds): string {
  const who = ends.teachers.length ? ends.teachers.join(', ') : 'şimdiki öğretmenin';
  const closed = [ends.sessions ? `${ends.sessions} seans` : '', ends.assignments ? `${ends.assignments} açık ödev` : ''].filter(Boolean).join(' ve ');
  return `Bir kurumun öğrencisi olunca bağımsız öğretmeninle çalışamazsın. Kuruma katılırsan ${who} ile bağın biter${closed ? `; onunla ${closed} kapanır` : ''}. Geçmiş çalışmaların silinmez.`;
}
