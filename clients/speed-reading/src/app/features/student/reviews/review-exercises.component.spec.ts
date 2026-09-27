import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { of } from 'rxjs';
import { ToasterService } from '../../../core/services/toaster.service';
import { ReviewService } from '../../../services/review.service';
import { ReviewExercisesComponent } from './review-exercises.component';

describe('ReviewExercisesComponent', () => {
  it('opens a due review in the registered universal player route', () => {
    const navigate = jasmine.createSpy('navigate');
    TestBed.configureTestingModule({
      providers: [
        { provide: Router, useValue: { navigate } },
        { provide: ReviewService, useValue: {
          dueReviews: () => [], statistics: () => null,
          getDueReviews: () => of([]), getStatistics: () => of(null)
        } },
        { provide: ToasterService, useValue: jasmine.createSpyObj('ToasterService', ['error', 'info']) }
      ]
    });

    const component = TestBed.runInInjectionContext(() => new ReviewExercisesComponent());
    component.startReview({ exerciseId: 'exercise-1', reviewItemId: 'review-1' } as never);

    expect(navigate).toHaveBeenCalledWith(
      ['/student/exercises/universal-player', 'exercise-1'],
      { queryParams: { reviewItemId: 'review-1', mode: 'review' } }
    );
  });

  it('shows the actual scores returned for an exercise review history', () => {
    TestBed.configureTestingModule({
      imports: [ReviewExercisesComponent],
      providers: [
        { provide: Router, useValue: { navigate: jasmine.createSpy('navigate') } },
        { provide: ReviewService, useValue: {
          dueReviews: () => [], statistics: () => null,
          getDueReviews: () => of([]), getStatistics: () => of(null),
          getReviewHistory: () => of([
            { reviewedAt: new Date('2026-09-26T10:00:00Z'), score: 82, intervalDays: 6, reviewNumber: 1 }
          ])
        } },
        { provide: ToasterService, useValue: jasmine.createSpyObj('ToasterService', ['error', 'info']) }
      ]
    });

    const fixture = TestBed.createComponent(ReviewExercisesComponent);
    fixture.detectChanges();
    fixture.componentInstance.viewHistory({ exerciseId: 'exercise-1' } as never);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('82%');
  });
});
