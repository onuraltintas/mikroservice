import { ToasterService } from '../../../core/services/toaster.service';
import { TestBed } from '@angular/core/testing';
import { PracticeExamEditorComponent } from './practice-exam-editor.component';
import { PracticeExamProfile, practiceTotals } from '../../../core/models/practice-exam';

const practiceProfiles: PracticeExamProfile[] = [
  { code: 'LGS', name: 'LGS', wrongAnswerDivisor: 3, sections: [{ code: 'TUR', name: 'Türkçe', questionCount: 20 }] },
  { code: 'TYT', name: 'TYT', wrongAnswerDivisor: 4, sections: [{ code: 'MAT', name: 'Matematik', questionCount: 40 }] }
];

describe('PracticeExamEditorComponent', () => {
  it('uses the server profile, fills blanks, validates totals, preserves negative nets and emits immutable changes', async () => {
    TestBed.configureTestingModule({ imports: [PracticeExamEditorComponent] });
    const fixture = TestBed.createComponent(PracticeExamEditorComponent);
    const component = fixture.componentInstance; component.profiles = practiceProfiles;
    component.valueChange.subscribe(value => fixture.componentRef.setInput('value', value));
    fixture.detectChanges(); await fixture.whenStable();
    const select = fixture.nativeElement.querySelector('select') as HTMLSelectElement;
    select.value = 'LGS'; select.dispatchEvent(new Event('change')); fixture.detectChanges(); await fixture.whenStable();
    expect(component.totals()).toMatchObject({ correct: 0, empty: 20, net: 0 });
    expect(fixture.nativeElement.querySelectorAll('tbody input').length).toBe(2);
    const original = component.value!;
    component.edit('TUR', 'wrong', 3); fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges(); await fixture.whenStable();
    expect(original.sections[0].wrong).toBe(0);
    expect(component.value?.sections[0].empty).toBe(17);
    expect(component.totals()?.net).toBe(-1);
    expect(fixture.nativeElement.querySelector('[role=alert]')).toBeNull();
    component.edit('TUR', 'correct', 18); fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges(); await fixture.whenStable();
    expect(component.totals()).toBeNull();
    expect(fixture.nativeElement.querySelector('[role=alert]')).not.toBeNull();
    component.disabled = true; component.edit('TUR', 'correct', 10);
    expect(component.value?.sections[0].correct).toBe(18);
  });

  it('restricts teacher profiles to the exam type and locks previously saved scope', async () => {
    const component = new PracticeExamEditorComponent(); component.profiles = practiceProfiles;
    component.examType = 'YKS'; expect(component.availableProfiles().map(x => x.code)).toEqual(['TYT']);
    const emit = vi.spyOn(component.valueChange, 'emit'); component.locked = true; await component.choose('TYT');
    expect(emit).not.toHaveBeenCalled();
  });

  it('rejects fractions, missing values, duplicates and missing sections before submitting', () => {
    const valid = { examCode: 'LGS', sections: [{ sectionCode: 'TUR', correct: 15, wrong: 3, empty: 2 }] };
    expect(practiceTotals(valid, practiceProfiles)?.net).toBe(14);
    expect(practiceTotals({ ...valid, sections: [] }, practiceProfiles)).toBeNull();
    expect(practiceTotals({ ...valid, sections: [...valid.sections, ...valid.sections] }, practiceProfiles)).toBeNull();
    expect(practiceTotals({ ...valid, sections: [{ ...valid.sections[0], correct: 15.5 }] }, practiceProfiles)).toBeNull();
    expect(practiceTotals({ ...valid, sections: [{ ...valid.sections[0], correct: null as unknown as number }] }, practiceProfiles)).toBeNull();
  });
});

beforeEach(() => { TestBed.overrideProvider(ToasterService, {useValue:{confirm:vi.fn().mockResolvedValue(true),success:vi.fn(),error:vi.fn(),info:vi.fn(),warning:vi.fn()}}); });
