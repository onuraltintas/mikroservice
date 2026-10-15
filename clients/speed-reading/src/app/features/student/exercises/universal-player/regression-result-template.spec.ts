import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from '../../../../core/services/auth.service';
import { ExerciseProgramService } from '../../../../core/services/exercise-program.service';
import { ExerciseService } from '../../../../core/services/exercise.service';
import { ExerciseSessionService } from '../../../../core/services/exercise-session.service';
import { LearningPathService } from '../../../../core/services/learning-path.service';
import { StudentProgramService } from '../../../../core/services/student-program.service';
import { ToasterService } from '../../../../core/services/toaster.service';
import { ReviewService } from '../../../../services/review.service';
import { ExercisePlayerComponent } from './exercise-player.component';

describe('regression result answer template', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [ExercisePlayerComponent],
      providers: [AuthService, ExerciseProgramService, ExerciseService, ExerciseSessionService,
        LearningPathService, StudentProgramService, ToasterService, ReviewService, Router, ActivatedRoute]
        .map(provide => ({ provide, useValue: {} }))
    });
    spyOn(ExercisePlayerComponent.prototype, 'ngOnInit');
  });

  function render(assessment = false) {
    const fixture = TestBed.createComponent(ExercisePlayerComponent);
    const player = fixture.componentInstance;
    player.isLoading = false;
    player.isAssessmentMode = assessment;
    player.engine = { engineType: 'regression_reduction', destroy: () => undefined } as any;
    player.engineState = { ...player.engineState, isCompleted: true, isRunning: false };
    player.resultSaveStatus = 'saved';
    player.result = { score: 50, accuracy: 50, totalTime: 1000, totalSteps: 4, completedSteps: 4, errors: 2,
      details: { correctAnswers: 2, totalQuestions: 4, answers: [
        { questionText: 'Regresyon cevabı', userAnswer: 'A', isCorrect: true },
        { questionText: 'Diğer motor cevabı', selectedAnswer: 'B', userAnswer: 'A', isCorrect: false },
        { questionText: 'Boş cevap', selectedAnswer: '', userAnswer: '', isCorrect: false },
        { questionText: 'Eksik cevap', isCorrect: false }
      ] } };
    fixture.detectChanges();
    return fixture;
  }

  it('renders engine answers, preserves selectedAnswer priority and labels unanswered items honestly', () => {
    const fixture = render();
    const labels = Array.from(fixture.nativeElement.querySelectorAll('.answers-summary .user-answer'))
      .map((element: any) => element.textContent.trim());
    expect(labels).toEqual(['Cevabınız: A', 'Cevabınız: B', 'Cevabınız: Yanıt verilmedi', 'Cevabınız: Yanıt verilmedi']);
    expect(fixture.nativeElement.textContent).not.toContain('Cüre Doldu');
  });

  it('keeps assessment answers hidden', () => {
    const fixture = render(true);
    expect(fixture.nativeElement.querySelector('.answers-summary')).toBeNull();
  });
});
