import { TestBed } from '@angular/core/testing';
import { PracticeExamEditorComponent } from './practice-exam-editor.component';
import { PracticeExamProfile, examTypeForProfile, practiceExamName, practiceNet, practiceTotals, requiresSections } from '../../../core/models/practice-exam';

// KM1 (01.11.2026): AGS/ÖABT keep the 4-wrong-1-right rule; YDS and YÖKDİL count correct answers only (divisor 0).
const profiles: PracticeExamProfile[] = [
  { code: 'LGS', name: 'LGS', wrongAnswerDivisor: 3, sections: [{ code: 'TUR', name: 'Türkçe', questionCount: 20 }] },
  { code: 'TYT', name: 'TYT', wrongAnswerDivisor: 4, sections: [{ code: 'MAT', name: 'Temel Matematik', questionCount: 40 }] },
  { code: 'AGS', name: 'AGS', wrongAnswerDivisor: 4, sections: [{ code: 'SOZ', name: 'Sözel Yetenek', questionCount: 15 }, { code: 'EGT', name: 'Eğitim Bilimleri ve TMES', questionCount: 30 }] },
  { code: 'OABT_OZEL', name: 'ÖABT · Özel Eğitim', wrongAnswerDivisor: 4, sections: [{ code: 'ALAN', name: 'Alan Bilgisi ve Alan Eğitimi', questionCount: 50 }] },
  { code: 'YDS_EN', name: 'YDS · İngilizce', wrongAnswerDivisor: 0, sections: [{ code: 'ENG', name: 'İngilizce', questionCount: 80 }] },
  { code: 'YOKDIL_SAG', name: 'YÖKDİL · Sağlık Bilimleri · İngilizce', wrongAnswerDivisor: 0, sections: [{ code: 'ENG', name: 'İngilizce', questionCount: 80 }] }
];

describe('practice exam rules for AGS, YDS and YÖKDİL', () => {
  it('maps every scope to its exam type and counts no penalty when the divisor is 0', () => {
    expect(['LGS', 'TYT', 'AYT_SAY', 'YDT_EN', 'AGS', 'OABT_TUR', 'YDS_EN', 'YOKDIL_FEN'].map(examTypeForProfile))
      .toEqual(['LGS', 'YKS', 'YKS', 'YKS', 'AGS', 'AGS', 'YDS', 'YOKDIL']);
    expect(['Mock', 'LGS', 'YKS', 'AGS', 'YDS', 'YOKDIL', 'Quiz'].map(requiresSections)).toEqual([false, true, true, true, true, true, false]);
    expect(practiceNet(10, 4, 0)).toBe(10);
    expect(practiceNet(10, 4, 4)).toBe(9);
    expect(practiceTotals({ examCode: 'YDS_EN', sections: [{ sectionCode: 'ENG', correct: 60, wrong: 15, empty: 5 }] }, profiles)?.net).toBe(60);
    expect(practiceExamName('YOKDIL_SOS')).toBe('YÖKDİL Sosyal Bilimler');
    expect(practiceExamName('OABT_OZEL')).toBe('ÖABT Özel Eğitim');
    expect(practiceExamName('AGS')).toBe('AGS');
    expect(practiceExamName('YDS_EN')).toBe('YDS İngilizce');
  });

  it('offers only the scopes of the chosen exam type and explains the no-penalty rule', async () => {
    TestBed.configureTestingModule({ imports: [PracticeExamEditorComponent] });
    const fixture = TestBed.createComponent(PracticeExamEditorComponent);
    const component = fixture.componentInstance; component.profiles = profiles;
    component.valueChange.subscribe(value => fixture.componentRef.setInput('value', value));
    fixture.componentRef.setInput('examType', 'AGS');
    expect(component.availableProfiles().map(x => x.code)).toEqual(['AGS', 'OABT_OZEL']);
    fixture.componentRef.setInput('examType', 'YKS');
    expect(component.availableProfiles().map(x => x.code)).toEqual(['TYT']);
    fixture.componentRef.setInput('examType', 'YDS');
    expect(component.availableProfiles().map(x => x.code)).toEqual(['YDS_EN']);
    fixture.detectChanges(); await fixture.whenStable();
    component.choose('YDS_EN'); fixture.detectChanges(); await fixture.whenStable();
    component.edit('ENG', 'correct', 50); fixture.detectChanges(); await fixture.whenStable();
    component.edit('ENG', 'wrong', 20); fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Yanlışlar doğruları götürmez');
    expect(fixture.nativeElement.querySelector('td.net').textContent.trim()).toBe('50.00');
    expect(component.totals()?.net).toBe(50);
  });
});
