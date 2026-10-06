import { ExercisePlayerComponent } from './exercise-player.component';

describe('visualization result normalization', () => {
  it('keeps preview and withheld assessment answers unmeasured', () => {
    const player = Object.create(ExercisePlayerComponent.prototype) as any;
    player.engine = { engineType: 'visualization' }; player.questionAnswers = [];
    expect(player.isMeasuredClientResult({ score: 0, accuracy: 0,
      details: { measurementStatus: 'NotMeasured', answers: [{ isCorrect: null }] } })).toBeFalse();
  });
});
