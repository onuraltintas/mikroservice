import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { CoachingInstitutionExamRanksService } from '../../../core/services/coaching-institution-exam-ranks.service';
import { StudentInstitutionExamsComponent } from './student-institution-exams.component';

// Roadmap O4: the student sees their rank and top percent in institution exams.
describe('StudentInstitutionExamsComponent', () => {
  it('shows the student\'s rank among all participants', () => {
    TestBed.configureTestingModule({ imports: [StudentInstitutionExamsComponent], providers: [{ provide: CoachingInstitutionExamRanksService, useValue: { mine: () => of([
      { examId: 'e', title: 'Kasım LGS', examName: 'LGS', examDate: '2026-11-08T00:00:00Z', net: 72, score: 400, rank: 2, participants: 3, topPercent: 66.7 }]) } }] });
    const fixture = TestBed.createComponent(StudentInstitutionExamsComponent); fixture.detectChanges();
    const text = (fixture.nativeElement as HTMLElement).textContent!;
    expect(text).toContain('Kurumda 2. / 3 öğrenci'); expect(text).toContain('%66,7'); expect(text).toContain('72,00 net');
  });
  it('stays hidden without institution exams', () => {
    TestBed.configureTestingModule({ imports: [StudentInstitutionExamsComponent], providers: [{ provide: CoachingInstitutionExamRanksService, useValue: { mine: () => of([]) } }] });
    const fixture = TestBed.createComponent(StudentInstitutionExamsComponent); fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent!.trim()).toBe('');
  });
});
