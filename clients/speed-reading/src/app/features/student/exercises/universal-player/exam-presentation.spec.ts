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

  it('starts the question timer only after server approval', async () => {
    const player = Object.create(ExercisePlayerComponent.prototype) as any;
    player.engine = { engineType: 'exam_simulation' };
    player.comprehensionQuestions = [{ questionId: 'question' }]; player.currentQuestionIndex = 0;
    player.backendSessionConfig = { examQuestionTimeSeconds: 30 };
    player.isPreviewSession = () => false;
    player.stopQuestionTimer = () => undefined; player.cdr = { detectChanges: () => undefined };
    player.enqueueAction = jasmine.createSpy().and.callFake(async (_: any, apply: any) => apply({ isValid: true }));
    await player.startQuestionTimer();
    expect(player.enqueueAction).toHaveBeenCalled();
    expect(player.questionTimeRemaining).toBe(30);
    clearInterval(player.questionTimerInterval);
  });
});
