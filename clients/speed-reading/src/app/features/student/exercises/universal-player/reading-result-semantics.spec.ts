import { ExercisePlayerComponent } from './exercise-player.component';
import { of } from 'rxjs';

describe('reading result semantics', () => {
  it('does not call a skimming inspection reading-speed measurement', () => {
    const player = Object.create(ExercisePlayerComponent.prototype) as any;
    player.engine = { engineType: 'skimming', getText: () => 'Ana fikir metni' };
    player.questionAnswers = [];
    const result = player.normalizeEngineResultForDisplay({ score: 0, accuracy: 0, totalTime: 6000, totalSteps: 1,
      completedSteps: 1, errors: 0, details: { wpm: 999 } });
    expect(result.details.wpm).toBeNull();
    expect(result.details.measurementStatus).toBe('NotMeasured');
    expect(player.getComprehensionText()).toBe('Ana fikir metni');
    expect(player.getCurrentReadingWpm()).toBe(0);
  });
  it('uses authoritative RSVP presentation metrics without marking partial daily progress complete', () => {
    const player = Object.create(ExercisePlayerComponent.prototype) as any;
    player.engine = { engineType: 'text_stream', getMode: () => 'rsvp' };
    player.engineState = { totalSteps: 4 };
    player.parsedConfig = {};
    player.questionAnswers = [];
    player.sessionId = 'session';
    player.exercise = { id: 'exercise', exerciseTypeName: 'RSVP' };
    player.isPreviewSession = () => false;
    player.showToast = () => undefined;
    player.cdr = { detectChanges: () => undefined };
    player.completeDailyProgress = jasmine.createSpy('daily');
    player.sessionService = { completeSession: () => of({ score: null, accuracy: null, rawWPM: null,
      detailedResults: { rsvpDisplayPaceWpm: 218.18, rsvpCompletionPercent: 50, rsvpPresentedWords: 2, readingIncomplete: true } }) };
    player.result = { score: 0, accuracy: 0, errors: 0, totalSteps: 4, completedSteps: 4,
      details: { displayPaceWpm: 999, completionPercent: 100, incomplete: false } };
    player.saveResult(player.result);
    expect(player.result.details.displayPaceWpm).toBe(218.18);
    expect(player.result.details.completionPercent).toBe(50);
    expect(player.result.details.rsvpPresentedWords).toBe(2);
    expect(player.result.details.incomplete).toBeTrue();
    expect(player.completeDailyProgress).not.toHaveBeenCalled();
  });
  it('uses authoritative fade partial results and does not advance daily progress', () => {
    const player = Object.create(ExercisePlayerComponent.prototype) as any;
    player.engine = { engineType: 'text_fade', getWpm: () => 200 };
    player.engineState = { totalSteps: 6 };
    player.parsedConfig = {};
    player.questionAnswers = [];
    player.sessionId = 'session';
    player.exercise = { id: 'exercise' };
    player.isPreviewSession = () => false;
    player.isTachistoscopeMode = () => false;
    player.showToast = () => undefined;
    player.cdr = { detectChanges: () => undefined };
    player.completeDailyProgress = jasmine.createSpy('daily');
    player.sessionService = { completeSession: () => of({ score: null, accuracy: null, rawWPM: null,
      detailedResults: { fadeDisplayPaceWpm: 300, fadeCompletionPercent: 33.33, readingIncomplete: true } }) };
    player.result = { score: 0, accuracy: 0, errors: 0, totalSteps: 6, completedSteps: 6,
      details: { displayPaceWpm: 200, completionPercent: 100, incomplete: false } };
    player.saveResult(player.result);
    expect(player.result.details.displayPaceWpm).toBe(300);
    expect(player.result.details.completionPercent).toBe(33.33);
    expect(player.result.details.incomplete).toBeTrue();
    expect(player.completeDailyProgress).not.toHaveBeenCalled();
  });
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
