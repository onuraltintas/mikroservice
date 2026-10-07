import { ExercisePlayerComponent } from './exercise-player.component';

describe('exam presentation', () => {
  it('shows the authoritative reading body beside a question without its own paragraph', () => {
    const player = Object.create(ExercisePlayerComponent.prototype) as any;
    player.engine = { engineType: 'exam_simulation', getText: () => 'Sunucunun sınav paragrafı.' };
    player.comprehensionQuestions = [{ questionText: 'Ana fikir nedir?' }];
    player.currentQuestionIndex = 0;
    expect(player.getQuestionContent()).toBe('Sunucunun sınav paragrafı.');
  });

  it('does not label combined exam activity as reading speed', () => {
    const player = Object.create(ExercisePlayerComponent.prototype) as any;
    player.engine = { engineType: 'exam_simulation', getWordCount: () => 100 };
    player.engineState = { timeElapsed: 1000 };
    expect(player.getCurrentReadingWpm()).toBe(0);
  });
});
