import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { NEVER, of } from 'rxjs';
import { CoachingAgreementService } from '../../../core/services/coaching-agreement.service';
import { CoachingService } from '../../../core/services/coaching.service';
import { ToasterService } from '../../../core/services/toaster.service';
import { StudentCoachingComponent } from './student-coaching.component';

describe('StudentCoachingComponent agreement boundary', () => {
  let fixture: ComponentFixture<StudentCoachingComponent>;
  let coaching: jasmine.SpyObj<CoachingService>;

  beforeEach(async () => {
    coaching = jasmine.createSpyObj<CoachingService>('CoachingService', [
      'getRelationships',
      'getGoals',
      'getCoachingSessions',
      'getAssignments',
      'getExamResults',
      'getStudySessions',
      'getSubjects'
    ]);
    coaching.getRelationships.and.returnValue(of({ items: [], total: 0, page: 1, pageSize: 1 }));
    coaching.getGoals.and.returnValue(of({ items: [], total: 0, page: 1, pageSize: 50 }));
    coaching.getCoachingSessions.and.returnValue(of({ items: [], total: 0, page: 1, pageSize: 20 }));
    coaching.getAssignments.and.returnValue(of({ items: [], total: 0, page: 1, pageSize: 20 }));
    coaching.getExamResults.and.returnValue(of({ items: [], total: 0, page: 1, pageSize: 20 }));
    coaching.getStudySessions.and.returnValue(of({ items: [], total: 0, page: 1, pageSize: 30 }));
    coaching.getSubjects.and.returnValue(of([]));

    await TestBed.configureTestingModule({
      imports: [StudentCoachingComponent],
      providers: [
        { provide: CoachingService, useValue: coaching },
        { provide: CoachingAgreementService, useValue: { getCurrent: () => NEVER } },
        { provide: MatDialog, useValue: jasmine.createSpyObj('MatDialog', ['open']) },
        { provide: ToasterService, useValue: jasmine.createSpyObj('ToasterService', ['success', 'error']) }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(StudentCoachingComponent);
  });

  it('does not request coaching data while agreement status is unresolved', () => {
    fixture.detectChanges();

    expect(coaching.getRelationships).not.toHaveBeenCalled();
    expect(coaching.getGoals).not.toHaveBeenCalled();
  });

  it('loads coaching data exactly once after the agreement gate is ready', () => {
    fixture.detectChanges();

    fixture.componentInstance.unlockCoaching();
    fixture.componentInstance.unlockCoaching();

    expect(coaching.getRelationships).toHaveBeenCalledTimes(1);
    expect(coaching.getGoals).toHaveBeenCalledTimes(1);
  });
});
