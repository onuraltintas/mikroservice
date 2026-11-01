import { coachingExamTypeOptions, coachingExamTypes } from './coaching-enum-contract';
import { institutionExamScopes } from './coaching-institution-exams.component';

// KM1 (01.11.2026): staff record and import AGS/ÖABT, YDS and YÖKDİL practice exams; the old numeric values stay.
describe('KM1 exam types in the staff portal', () => {
  it('knows the new exam types without moving the old values', () => {
    expect(coachingExamTypes).toEqual({ 1: 'Mock', 2: 'Weekly', 3: 'Monthly', 4: 'LGS', 5: 'YKS', 6: 'MidTerm', 7: 'Final', 8: 'Quiz', 9: 'AGS', 10: 'YDS', 11: 'YOKDIL' });
    expect(coachingExamTypeOptions.map(x => `${x.value}:${x.key}:${x.label}`)).toEqual(['1:Mock:Deneme', '2:Weekly:Haftalık test', '3:Monthly:Aylık değerlendirme',
      '4:LGS:LGS', '5:YKS:YKS', '9:AGS:AGS / ÖABT', '10:YDS:YDS', '11:YOKDIL:YÖKDİL', '6:MidTerm:Ara sınav', '7:Final:Final', '8:Quiz:Kısa sınav']);
  });

  it('offers every official scope for institution exams', () => {
    const codes = institutionExamScopes.map(x => x.code);
    expect(codes.length).toBe(6 + 1 + 17 + 1 + 3);
    expect(codes.slice(0, 7)).toEqual(['LGS', 'TYT', 'AYT_SAY', 'AYT_EA', 'AYT_SOZ', 'YDT_EN', 'AGS']);
    expect(codes).toContain('OABT_OZEL');
    expect(codes.slice(-4)).toEqual(['YDS_EN', 'YOKDIL_FEN', 'YOKDIL_SAG', 'YOKDIL_SOS']);
    expect(institutionExamScopes.find(x => x.code === 'OABT_SINIF')?.name).toBe('ÖABT Sınıf Öğretmenliği');
  });
});
