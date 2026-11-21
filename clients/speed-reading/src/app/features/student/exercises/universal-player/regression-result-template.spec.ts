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
    expect(labels).toEqual(['Cevabın: A', 'Cevabın: B', 'Cevabın: Yanıt verilmedi', 'Cevabın: Yanıt verilmedi']);
    expect(fixture.nativeElement.textContent).not.toContain('Cüre Doldu');
  });

  it('keeps assessment answers hidden', () => {
    const fixture = render(true);
    expect(fixture.nativeElement.querySelector('.answers-summary')).toBeNull();
  });

  // Feedback fix 21 (approved 21.11.2026): after a wrong answer the correct option is marked and named.
  function renderAnswering(feedback: any) {
    spyOn(ExercisePlayerComponent.prototype, 'getRegressionPhase').and.returnValue('answering');
    spyOn(ExercisePlayerComponent.prototype, 'getRegressionCurrentQuestion').and.returnValue(
      { QuestionText: 'Metin neyi anlatıyor?', OptionA: 'Deniz', OptionB: 'Orman', OptionC: 'Dağ', OptionD: 'Çöl' });
    spyOn(ExercisePlayerComponent.prototype, 'getRegressionCurrentQuestionIndex').and.returnValue(0);
    spyOn(ExercisePlayerComponent.prototype, 'getRegressionQuestionCount').and.returnValue(3);
    spyOn(ExercisePlayerComponent.prototype, 'getRegressionBackwardClickCount').and.returnValue(0);
    const fixture = TestBed.createComponent(ExercisePlayerComponent);
    const player = fixture.componentInstance;
    player.isLoading = false;
    player.engine = { engineType: 'regression_reduction', destroy: () => undefined } as any;
    player.engineState = { ...player.engineState, isRunning: true, isCompleted: false };
    player.selectedAnswer = 'A';
    player.questionFeedback = feedback;
    fixture.detectChanges();
    return fixture;
  }

  it('marks the correct option and names it after a wrong answer', () => {
    const fixture = renderAnswering({ isCorrect: false, correctAnswer: 'C' });
    const correct = fixture.nativeElement.querySelector('.option-button.correct .option-label');
    expect(correct?.textContent.trim()).toBe('C');
    expect(fixture.nativeElement.querySelector('.option-button.wrong .option-label')?.textContent.trim()).toBe('A');
    expect(fixture.nativeElement.querySelector('.answer-feedback')?.textContent).toContain('Doğrusu: C');
  });

  it('shows no verdict when the answer is hidden (assessment or preview)', () => {
    const fixture = renderAnswering({ isCorrect: null });
    expect(fixture.nativeElement.querySelector('.option-button.correct')).toBeNull();
    expect(fixture.nativeElement.querySelector('.answer-feedback')).toBeNull();
  });

  // Feedback fix 20: questions after a speed exercise name the correct option after a wrong answer.
  it('names the correct option for questions after a speed exercise', () => {
    const fixture = TestBed.createComponent(ExercisePlayerComponent);
    const player = fixture.componentInstance as any;
    player.isLoading = false;
    player.engine = { engineType: 'word_highlight', destroy: () => undefined };
    player.engineState = { ...player.engineState, isRunning: false, isCompleted: false };
    player.exercisePhase = 'questions';
    player.comprehensionQuestions = [{ QuestionId: 'q1', QuestionText: 'Ana fikir nedir?', OptionA: 'Bir', OptionB: 'İki',
      OptionC: 'Üç', OptionD: 'Dört' }];
    player.currentQuestionIndex = 0;
    player.selectedAnswer = 'B';
    player.questionFeedback = { isCorrect: false, correctAnswer: 'C' };
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.answer-option.correct .option-letter')?.textContent.trim()).toBe('C');
    expect(fixture.nativeElement.querySelector('.answer-feedback')?.textContent).toContain('Doğrusu: C');
  });
});
