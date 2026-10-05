import { ExercisePlayerComponent } from './exercise-player.component';

describe('reading result semantics', () => {
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
