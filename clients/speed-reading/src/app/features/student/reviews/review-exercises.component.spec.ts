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
});
