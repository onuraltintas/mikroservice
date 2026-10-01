import { TestBed } from '@angular/core/testing';
import { DailyExercisesComponent } from './daily-exercises.component';
import { AuthService } from '../../../core/services/auth.service';
import { provideRouter } from '@angular/router';
import { ExerciseProgramService } from '../../../core/services/exercise-program.service';
import { ExerciseTypeService } from '../../../core/services/exercise-type.service';

describe('Staff training completion copy', () => {
  it('does not tell staff to wait for the next calendar day', () => {
    TestBed.configureTestingModule({ providers: [provideRouter([]),
      { provide: AuthService, useValue: { hasRole: (role: string) => role === 'Teacher' } },
      { provide: ExerciseProgramService, useValue: {} }, { provide: ExerciseTypeService, useValue: {} }
    ] });
    // Inspect the rendered template with the existing daily page's data loaders disabled.
    const fixture = TestBed.createComponent(DailyExercisesComponent);
    spyOn(fixture.componentInstance, 'loadData');
    spyOn(fixture.componentInstance, 'loadExerciseTypes');
    fixture.componentInstance.loading.set(false);
    fixture.componentInstance.exercises.set([{ exerciseId: 'exercise', exerciseTypeName: 'Fixation',
      title: 'Egzersiz', isCompleted: true }] as any);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.de-celebration__desc')?.textContent).toContain('hemen');
  });
});
