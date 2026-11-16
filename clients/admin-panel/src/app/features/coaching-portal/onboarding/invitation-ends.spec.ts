import { invitationEndsText } from '../../../../../shared/invitation-ends';

describe('what joining an institution ends (live test 16.11.2026, #41 and #44)', () => {
  it('names the teacher and what closes', () => {
    expect(invitationEndsText({ teachers: ['Ayşe Kaya'], sessions: 2, assignments: 1 }))
      .toBe('Bir kurumun öğrencisi olunca bağımsız öğretmeninle çalışamazsın. Kuruma katılırsan Ayşe Kaya ile bağın biter; onunla 2 seans ve 1 açık ödev kapanır. Geçmiş çalışmaların silinmez.');
  });
  it('says nothing about work when nothing is open', () => {
    expect(invitationEndsText({ teachers: [], sessions: 0, assignments: 0 }))
      .toBe('Bir kurumun öğrencisi olunca bağımsız öğretmeninle çalışamazsın. Kuruma katılırsan şimdiki öğretmenin ile bağın biter. Geçmiş çalışmaların silinmez.');
  });
});
