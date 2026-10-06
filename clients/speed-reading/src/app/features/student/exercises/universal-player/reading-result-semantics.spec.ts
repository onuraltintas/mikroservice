import { ExercisePlayerComponent } from './exercise-player.component';

describe('reading result semantics', () => {
  it('keeps grouping completion separate from accuracy and flags partial daily progress', () => {
    const player = Object.create(ExercisePlayerComponent.prototype) as any;
    player.engine = { engineType: 'word_highlight', getWpm: () => 200 };
    player.questionAnswers = [];
    const result = player.normalizeEngineResultForDisplay({ score: 50, accuracy: 50, totalTime: 1000,
      totalSteps: 4, completedSteps: 2, errors: 0, details: { timedOut: true } });
    expect(result.details.completionPercent).toBe(50);
    expect(result.details.incomplete).toBeTrue();
    expect(result.accuracy).toBe(0);
    expect(result.details.comprehensionScore).toBeNull();
  });
  it('retains incomplete reading after questions without fabricating speed', () => {
    const player = Object.create(ExercisePlayerComponent.prototype) as any;
    player.engine = { engineType: 'reading_comprehension' };
    player.engineState = { timeElapsed: 5000 };
    player.readingIncomplete = true;
    player.readingWpm = 999;
    player.comprehensionQuestions = [{}];
    player.questionAnswers = [{ isCorrect: true, timeSpent: 1000, targetTime: 2000, selectedAnswer: 'A' }];
    player.stopQuestionTimer = () => undefined;
    player.getTargetWpm = () => 200;
    player.saveResult = () => undefined;
    player.cdr = { detectChanges: () => undefined };
    player.finishQuestionPhase();
    expect(player.result.details.wpm).toBeNull();
    expect(player.result.details.timedOut).toBeTrue();
    expect(player.result.details.comprehensionScore).toBe(100);
  });
  it('keeps question-phase completion separate from display pace', () => {
    const player = Object.create(ExercisePlayerComponent.prototype) as any;
    player.engine = { engineType: 'word_highlight', getWpm: () => 200 };
    player.engineState = { timeElapsed: 3000 };
    player.readingWpm = 999;
    player.comprehensionQuestions = [{}];
    player.questionAnswers = [{ isCorrect: true, timeSpent: 1000, targetTime: 2000, selectedAnswer: 'A' }];
    player.stopQuestionTimer = () => undefined;
    player.getTargetWpm = () => 200;
    player.saveResult = jasmine.createSpy('saveResult');
    player.cdr = { detectChanges: () => undefined };
    player.finishQuestionPhase();
    expect(player.result.details.wpm).toBeNull();
    expect(player.result.details.displayPaceWpm).toBe(200);
    expect(player.result.details.comprehensionScore).toBe(100);
  });

  it('identifies paced reading for the question header', () => {
    const player = Object.create(ExercisePlayerComponent.prototype) as any;
    player.engine = { engineType: 'word_highlight' };
    expect(player.isPacedReadingEngine()).toBeTrue();
    player.engine = { engineType: 'reading_comprehension' };
    expect(player.isPacedReadingEngine()).toBeFalse();
  });
  it('explains unmeasured comprehension without claiming letter-answer validation', () => {
    const player = Object.create(ExercisePlayerComponent.prototype) as any;
    player.result = { details: { displayPaceWpm: 200, comprehensionScore: null } };
    expect(player.getReadingMeasurementMessage()).toContain('Anlama ölçülmedi');
    expect(player.getReadingMeasurementMessage()).toContain('gösterim temposudur');
  });

  it('describes measured comprehension separately from display tempo', () => {
    const player = Object.create(ExercisePlayerComponent.prototype) as any;
    player.result = { details: { displayPaceWpm: 200, comprehensionScore: 80 } };
    expect(player.getReadingMeasurementMessage()).toContain('sorularla ölçüldü');
    expect(player.getReadingMeasurementMessage()).not.toContain('Anlama ölçülmedi');
  });
  for (const type of ['word_highlight', 'text_fade', 'text_stream']) {
    it(`${type} reports tempo without inventing measured reading speed or comprehension`, () => {
      const player = Object.create(ExercisePlayerComponent.prototype) as any;
      player.engine = { engineType: type, getWpm: () => 200, getDisplayPaceWpm: () => 200, getCurrentDuration: () => 300, getMode: () => 'rsvp' };
      player.questionAnswers = [];
      const result = player.normalizeEngineResultForDisplay({ score: 100, accuracy: 100,
        details: { wpm: 200, comprehensionScore: 100 }, completedSteps: 10, totalSteps: 10, totalTime: 3000, errors: 0 });
      expect(result.details.wpm).toBeNull();
      expect(result.details.comprehensionScore).toBeNull();
      expect(result.details.measurementStatus).toBe('NotMeasured');
    });
  }

  it('keeps comprehension independent from imposed display pace', () => {
    const player = Object.create(ExercisePlayerComponent.prototype) as any;
    player.engine = { engineType: 'word_highlight', getWpm: () => 200 };
    player.questionAnswers = [{ isCorrect: true }];
    const result = player.normalizeEngineResultForDisplay({ score: 100, accuracy: 100,
      details: { wpm: 200, comprehensionScore: 100, totalQuestions: 1 }, completedSteps: 1, totalSteps: 1, totalTime: 3000, errors: 0 });
    expect(result.details.wpm).toBeNull();
    expect(result.details.comprehensionScore).toBe(100);
  });
});
